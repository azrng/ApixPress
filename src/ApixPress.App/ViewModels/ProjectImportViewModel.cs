using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApixPress.App.Helpers;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels.Base;
using Azrng.Core.Results;

namespace ApixPress.App.ViewModels;

public partial class ProjectImportViewModel : ViewModelBase, IDisposable
{
    private static class ImportDataModes
    {
        public const string File = "file";
        public const string Url = "url";
    }

    private static class ImportStatusStates
    {
        public const string Info = "info";
        public const string Success = "success";
        public const string Error = "error";
    }

    private readonly string _projectId;
    private readonly IApiWorkspaceService _apiWorkspaceService;
    private readonly IFilePickerService _filePickerService;
    private readonly Func<IReadOnlyList<ApiEndpointDto>, Task> _syncImportedInterfacesAsync;
    private readonly Action<string> _setStatusMessage;
    private CancellationTokenSource? _importCancellationTokenSource;
    private PendingImportRequest? _pendingImportRequest;
    private bool _isDisposed;

    public ProjectImportViewModel(
        string projectId,
        IApiWorkspaceService apiWorkspaceService,
        IFilePickerService filePickerService,
        Func<IReadOnlyList<ApiEndpointDto>, Task> syncImportedInterfacesAsync,
        Action<string> setStatusMessage)
    {
        _projectId = projectId;
        _apiWorkspaceService = apiWorkspaceService;
        _filePickerService = filePickerService;
        _syncImportedInterfacesAsync = syncImportedInterfacesAsync;
        _setStatusMessage = setStatusMessage;

        ImportedApiDocuments.CollectionChanged += OnImportedApiDocumentsCollectionChanged;
    }

    public ObservableCollection<ProjectImportedDocumentItemViewModel> ImportedApiDocuments { get; } = [];

    public bool IsImportFileMode => SelectedImportDataMode == ImportDataModes.File;
    public bool IsImportUrlMode => SelectedImportDataMode == ImportDataModes.Url;
    public bool CanEditImportData => !IsImportDataBusy;
    public bool ShowProjectImportDialogStatus => ShowImportStatusInfo || ShowImportStatusSuccess || ShowImportStatusError;
    public bool HasSelectedImportFile => !string.IsNullOrWhiteSpace(SelectedImportFilePath);
    public string SelectedImportFileName => HasSelectedImportFile ? Path.GetFileName(SelectedImportFilePath) : ImportTexts.UnselectedFileName;
    public string SelectedImportFileSummary => HasSelectedImportFile
        ? SelectedImportFilePath
        : ImportTexts.UnselectedFileSummary;
    public bool HasImportedApiDocuments => ImportedApiDocuments.Count > 0;
    public bool ShowImportedApiDocumentsEmptyState => !IsImportDataBusy && !HasImportedApiDocuments;
    public bool ShowImportStatusInfo => ImportDataStatusState == ImportStatusStates.Info;
    public bool ShowImportStatusSuccess => ImportDataStatusState == ImportStatusStates.Success;
    public bool ShowImportStatusError => ImportDataStatusState == ImportStatusStates.Error;
    public string ImportedApiDocumentCountText => ImportedApiDocuments.Count.ToString();
    public bool HasPendingImportPreview => PendingImportPreview is not null;
    public string PendingImportOverwriteTitle => PendingImportPreview?.DocumentName ?? string.Empty;
    public string PendingImportOverwriteSummary
    {
        get
        {
            if (PendingImportPreview is null)
            {
                return string.Empty;
            }

            return ImportTexts.FormatPendingOverwriteSummary(
                PendingImportPreview.NewEndpointCount,
                PendingImportPreview.ConflictCount);
        }
    }

    public string PendingImportOverwriteDetailText
    {
        get
        {
            if (PendingImportPreview is null || PendingImportPreview.ConflictItems.Count == 0)
            {
                return string.Empty;
            }

            var displayedConflicts = PendingImportPreview.ConflictItems
                .Take(5)
                .Select(item => $"{item.Method} {item.Path} 现有：{item.ExistingDocumentName} / {item.ExistingEndpointName} -> 导入：{item.ImportedEndpointName}")
                .ToList();
            var lines = new List<string>
            {
                ImportTexts.OverwriteDetailPrefix
            };
            lines.AddRange(displayedConflicts);
            if (PendingImportPreview.ConflictItems.Count > displayedConflicts.Count)
            {
                lines.Add(ImportTexts.FormatAdditionalConflictItems(PendingImportPreview.ConflictItems.Count - displayedConflicts.Count));
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    public string DialogTitle => ImportTexts.DialogTitle;
    public string DialogFormatLabel => ImportTexts.DataSourceFormatLabel;
    public string DialogFormatName => ImportTexts.DataSourceFormatName;
    public string DialogNoticeText => ImportTexts.DialogNotice;
    public string FileModeTitle => ImportTexts.FileModeTitle;
    public string UrlModeTitle => ImportTexts.UrlModeTitle;
    public string PickFileButtonText => ImportTexts.PickFileButton;
    public string StartImportButtonText => ImportTexts.StartImportButton;
    public string UrlDescription => ImportTexts.UrlDescription;
    public string UrlWatermark => ImportTexts.UrlWatermark;
    public string ImportUrlButtonText => ImportTexts.ImportUrlButton;
    public string BusyOverlayDescription => ImportTexts.BusyOverlayDescription;

    [ObservableProperty]
    private string selectedImportDataMode = ImportDataModes.File;

    [ObservableProperty]
    private string selectedImportFilePath = string.Empty;

    [ObservableProperty]
    private string importUrl = string.Empty;

    [ObservableProperty]
    private bool isImportDataBusy;

    [ObservableProperty]
    private string importDataBusyText = ImportTexts.BusyProcessing;

    [ObservableProperty]
    private string importDataStatusText = ImportTexts.DefaultStatus;

    [ObservableProperty]
    private string importDataStatusState = ImportStatusStates.Info;

    [ObservableProperty]
    private bool isDialogOpen;

    [ObservableProperty]
    private bool isOverwriteConfirmDialogOpen;

    [ObservableProperty]
    private ApiImportPreviewDto? pendingImportPreview;

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        ImportedApiDocuments.CollectionChanged -= OnImportedApiDocumentsCollectionChanged;
        CancellationTokenSourceHelper.CancelAndDispose(ref _importCancellationTokenSource);
        ClearPendingImportConfirmation();
    }

    [RelayCommand]
    private void OpenDialog()
    {
        SelectedImportDataMode = ImportDataModes.File;
        ClearPendingImportConfirmation();
        IsDialogOpen = true;
        _setStatusMessage(ImportTexts.OpenDialogStatus);
    }

    [RelayCommand]
    private void CloseDialog()
    {
        DismissDialog();
        _setStatusMessage(ImportTexts.CloseDialogStatus);
    }

    public void DismissDialog()
    {
        IsDialogOpen = false;
        ClearPendingImportConfirmation();
    }

    [RelayCommand]
    private void ShowImportFileMode()
    {
        SelectedImportDataMode = ImportDataModes.File;
    }

    [RelayCommand]
    private void ShowImportUrlMode()
    {
        SelectedImportDataMode = ImportDataModes.Url;
    }

    [RelayCommand]
    private async Task PickSwaggerImportFileAsync()
    {
        if (IsImportDataBusy)
        {
            return;
        }

        var filePath = await _filePickerService.PickSwaggerJsonFileAsync(CancellationToken.None);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            SetImportDataStatus(ImportTexts.PickFileCancelledStatus, ImportStatusStates.Info);
            return;
        }

        SelectedImportFilePath = filePath;
        var selectedFileStatus = ImportTexts.FormatSelectedFileStatus(Path.GetFileName(filePath));
        SetImportDataStatus(selectedFileStatus, ImportStatusStates.Info);
        _setStatusMessage(selectedFileStatus);
    }

    [RelayCommand]
    private async Task ImportSwaggerFileAsync()
    {
        if (!HasSelectedImportFile)
        {
            SetImportDataStatus(ImportTexts.MissingFileStatus, ImportStatusStates.Error);
            _setStatusMessage(ImportTexts.MissingFileShellStatus);
            return;
        }

        await ImportSwaggerAsync(
            cancellationToken => _apiWorkspaceService.PreviewImportFromFileAsync(_projectId, SelectedImportFilePath.Trim(), cancellationToken),
            cancellationToken => _apiWorkspaceService.ImportFromFileAsync(_projectId, SelectedImportFilePath.Trim(), cancellationToken),
            document => ImportTexts.FormatFileImportSuccess(document.Name),
            ImportTexts.PreviewLocalBusyText,
            ImportTexts.ImportLocalBusyText);
    }

    [RelayCommand]
    private async Task ImportSwaggerUrlAsync()
    {
        var importTargetUrl = ImportUrl.Trim();
        if (string.IsNullOrWhiteSpace(importTargetUrl))
        {
            SetImportDataStatus(ImportTexts.MissingUrlStatus, ImportStatusStates.Error);
            _setStatusMessage(ImportTexts.MissingUrlShellStatus);
            return;
        }

        await ImportSwaggerAsync(
            cancellationToken => _apiWorkspaceService.PreviewImportFromUrlAsync(_projectId, importTargetUrl, cancellationToken),
            cancellationToken => _apiWorkspaceService.ImportFromUrlAsync(_projectId, importTargetUrl, cancellationToken),
            document => ImportTexts.FormatUrlImportSuccess(document.Name),
            ImportTexts.PreviewUrlBusyText,
            ImportTexts.ImportUrlBusyText);
    }

    [RelayCommand]
    private async Task RefreshImportedApiDocumentsAsync()
    {
        await LoadImportedDocumentsAsync();
        _setStatusMessage(HasImportedApiDocuments
            ? ImportTexts.FormatRefreshImportedDocumentsSuccess(ImportedApiDocuments.Count)
            : ImportTexts.EmptyRefreshStatus);
    }

    [RelayCommand]
    private void CancelImportOverwriteConfirm()
    {
        ClearPendingImportConfirmation();
        SetImportDataStatus(ImportTexts.OverwriteCancelled, ImportStatusStates.Info);
        _setStatusMessage(ImportTexts.OverwriteCancelled);
    }

    [RelayCommand]
    private async Task ConfirmImportOverwriteAsync()
    {
        if (_pendingImportRequest is null || IsImportDataBusy)
        {
            return;
        }

        var cancellationToken = CancellationTokenSourceHelper.Refresh(ref _importCancellationTokenSource).Token;
        ImportDataBusyText = _pendingImportRequest.ImportBusyText;
        IsImportDataBusy = true;
        try
        {
            await ExecuteImportAsync(
                _pendingImportRequest.ImportAction,
                _pendingImportRequest.BuildSuccessMessage,
                _pendingImportRequest.ImportBusyText,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            HandleUnexpectedImportFailure(exception, ImportTexts.UnexpectedFailureFallback);
        }
        finally
        {
            IsImportDataBusy = false;
        }
    }

    public async Task LoadImportedDocumentsAsync(bool manageBusyState = true)
    {
        if (_isDisposed)
        {
            return;
        }

        if (manageBusyState)
        {
            IsImportDataBusy = true;
        }

        var cancellationToken = CancellationTokenSourceHelper.Refresh(ref _importCancellationTokenSource).Token;
        try
        {
            var documentsTask = _apiWorkspaceService.GetDocumentsAsync(_projectId, cancellationToken);
            var endpointsTask = _apiWorkspaceService.GetProjectEndpointsAsync(_projectId, cancellationToken);
            await Task.WhenAll(documentsTask, endpointsTask);

            var documents = await documentsTask;
            var endpoints = await endpointsTask;
            var endpointCountByDocumentId = endpoints
                .GroupBy(item => item.DocumentId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

            await _syncImportedInterfacesAsync(endpoints);
            ImportedApiDocuments.ReplaceWith(documents.Select(document => new ProjectImportedDocumentItemViewModel
            {
                Id = document.Id,
                Name = document.Name,
                SourceTypeText = ResolveImportSourceTypeText(document.SourceType),
                SourceValueText = string.IsNullOrWhiteSpace(document.SourceValue) ? "-" : document.SourceValue,
                BaseUrlText = string.IsNullOrWhiteSpace(document.BaseUrl) ? ImportTexts.BaseUrlFallback : document.BaseUrl,
                ImportedAtText = document.ImportedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                EndpointCount = endpointCountByDocumentId.TryGetValue(document.Id, out var endpointCount)
                    ? endpointCount
                    : 0
            }));

            if (!HasImportedApiDocuments && ImportDataStatusState == ImportStatusStates.Info)
            {
                SetImportDataStatus(ImportTexts.EmptyStateStatus, ImportStatusStates.Info);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            HandleUnexpectedImportFailure(exception, ImportTexts.RefreshAfterImportFailure);
        }
        finally
        {
            if (manageBusyState)
            {
                IsImportDataBusy = false;
            }
        }
    }

    partial void OnSelectedImportDataModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsImportFileMode));
        OnPropertyChanged(nameof(IsImportUrlMode));
    }

    partial void OnIsImportDataBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEditImportData));
        OnPropertyChanged(nameof(ShowImportedApiDocumentsEmptyState));
    }

    partial void OnSelectedImportFilePathChanged(string value)
    {
        OnPropertyChanged(nameof(HasSelectedImportFile));
        OnPropertyChanged(nameof(SelectedImportFileName));
        OnPropertyChanged(nameof(SelectedImportFileSummary));
    }

    partial void OnImportDataStatusStateChanged(string value)
    {
        OnPropertyChanged(nameof(ShowImportStatusInfo));
        OnPropertyChanged(nameof(ShowImportStatusSuccess));
        OnPropertyChanged(nameof(ShowImportStatusError));
        OnPropertyChanged(nameof(ShowProjectImportDialogStatus));
    }

    partial void OnPendingImportPreviewChanged(ApiImportPreviewDto? value)
    {
        OnPropertyChanged(nameof(HasPendingImportPreview));
        OnPropertyChanged(nameof(PendingImportOverwriteTitle));
        OnPropertyChanged(nameof(PendingImportOverwriteSummary));
        OnPropertyChanged(nameof(PendingImportOverwriteDetailText));
    }

    private void OnImportedApiDocumentsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasImportedApiDocuments));
        OnPropertyChanged(nameof(ShowImportedApiDocumentsEmptyState));
        OnPropertyChanged(nameof(ImportedApiDocumentCountText));
    }

    private async Task ImportSwaggerAsync(
        Func<CancellationToken, Task<IResultModel<ApiImportPreviewDto>>> previewAction,
        Func<CancellationToken, Task<IResultModel<ApiDocumentDto>>> importAction,
        Func<ApiDocumentDto, string> buildSuccessMessage,
        string previewBusyText,
        string importBusyText)
    {
        if (IsImportDataBusy)
        {
            return;
        }

        var cancellationToken = CancellationTokenSourceHelper.Refresh(ref _importCancellationTokenSource).Token;
        ImportDataBusyText = previewBusyText;
        IsImportDataBusy = true;
        try
        {
            var previewResult = await previewAction(cancellationToken);
            if (!previewResult.IsSuccess || previewResult.Data is null)
            {
                var failureMessage = string.IsNullOrWhiteSpace(previewResult.Message)
                    ? ImportTexts.PreviewFailureFallback
                    : previewResult.Message;
                SetImportDataStatus(failureMessage, ImportStatusStates.Error);
                _setStatusMessage(failureMessage);
                return;
            }

            if (previewResult.Data.ConflictCount > 0)
            {
                _pendingImportRequest = new PendingImportRequest(importAction, buildSuccessMessage, importBusyText);
                PendingImportPreview = previewResult.Data;
                IsOverwriteConfirmDialogOpen = true;
                SetImportDataStatus(
                    ImportTexts.FormatOverwriteDetectedStatus(previewResult.Data.ConflictCount),
                    ImportStatusStates.Info);
                _setStatusMessage(ImportTexts.OverwritePendingShellStatus);
                return;
            }

            await ExecuteImportAsync(importAction, buildSuccessMessage, importBusyText, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            HandleUnexpectedImportFailure(exception, ImportTexts.UnexpectedFailureFallback);
        }
        finally
        {
            IsImportDataBusy = false;
        }
    }

    private async Task ExecuteImportAsync(
        Func<CancellationToken, Task<IResultModel<ApiDocumentDto>>> importAction,
        Func<ApiDocumentDto, string> buildSuccessMessage,
        string importBusyText,
        CancellationToken cancellationToken)
    {
        ImportDataBusyText = importBusyText;
        var result = await importAction(cancellationToken);
        if (!result.IsSuccess || result.Data is null)
        {
            var failureMessage = string.IsNullOrWhiteSpace(result.Message)
                ? ImportTexts.ImportFailureFallback
                : result.Message;
            SetImportDataStatus(failureMessage, ImportStatusStates.Error);
            _setStatusMessage(failureMessage);
            return;
        }

        ImportDataBusyText = ImportTexts.BusyRefreshResult;
        await LoadImportedDocumentsAsync(manageBusyState: false);
        var successMessage = buildSuccessMessage(result.Data);
        ClearPendingImportConfirmation();
        SetImportDataStatus(successMessage, ImportStatusStates.Success);
        IsDialogOpen = false;
        _setStatusMessage(successMessage);
    }

    private void HandleUnexpectedImportFailure(Exception exception, string fallbackMessage)
    {
        ClearPendingImportConfirmation();

        var failureMessage = string.IsNullOrWhiteSpace(exception.Message)
            ? fallbackMessage
            : $"{fallbackMessage} {exception.Message}";

        SetImportDataStatus(failureMessage, ImportStatusStates.Error);
        _setStatusMessage(failureMessage);
    }

    private void SetImportDataStatus(string message, string statusState)
    {
        ImportDataStatusText = message;
        ImportDataStatusState = statusState;
    }

    private void ClearPendingImportConfirmation()
    {
        _pendingImportRequest = null;
        PendingImportPreview = null;
        IsOverwriteConfirmDialogOpen = false;
    }

    private static string ResolveImportSourceTypeText(string sourceType)
    {
        return string.Equals(sourceType, "URL", StringComparison.OrdinalIgnoreCase)
            ? ImportTexts.SourceTypeUrl
            : ImportTexts.SourceTypeFile;
    }

    private sealed class PendingImportRequest
    {
        public PendingImportRequest(
            Func<CancellationToken, Task<IResultModel<ApiDocumentDto>>> importAction,
            Func<ApiDocumentDto, string> buildSuccessMessage,
            string importBusyText)
        {
            ImportAction = importAction;
            BuildSuccessMessage = buildSuccessMessage;
            ImportBusyText = importBusyText;
        }

        public Func<CancellationToken, Task<IResultModel<ApiDocumentDto>>> ImportAction { get; }

        public Func<ApiDocumentDto, string> BuildSuccessMessage { get; }

        public string ImportBusyText { get; }
    }
}
