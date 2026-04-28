using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApixPress.App.Helpers;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels.Base;
using Avalonia.Controls.Notifications;
using Azrng.Core.Results;

namespace ApixPress.App.ViewModels;

public partial class ProjectImportViewModel : ViewModelBase
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
    private readonly IAppNotificationService _appNotificationService;
    private readonly IProjectDataExportService _projectDataExportService;
    private readonly Func<ProjectWorkspaceItemViewModel> _getProject;
    private readonly Func<IReadOnlyList<ApiEndpointDto>, Task> _syncImportedInterfacesAsync;
    private readonly Action<string> _setStatusMessage;
    private CancellationTokenSource? _importCancellationTokenSource;
    private PendingImportRequest? _pendingImportRequest;
    private bool _hasLoadedImportedDocuments;
    private static readonly ImportOperationTextBundle SwaggerImportTextBundle = new(
        "Swagger 导入成功",
        "Swagger 导入失败",
        ImportTexts.PreviewFailureFallback,
        ImportTexts.ImportFailureFallback,
        ImportTexts.UnexpectedFailureFallback,
        ImportTexts.OverwritePendingShellStatus);
    private static readonly ImportOperationTextBundle ProjectPackageImportTextBundle = new(
        "项目数据包导入成功",
        "项目数据包导入失败",
        ImportTexts.PackagePreviewFailureFallback,
        ImportTexts.PackageImportFailureFallback,
        ImportTexts.PackageUnexpectedFailureFallback,
        ImportTexts.PackageOverwritePendingShellStatus);

    public ProjectImportViewModel(
        string projectId,
        IApiWorkspaceService apiWorkspaceService,
        IFilePickerService filePickerService,
        IAppNotificationService appNotificationService,
        IProjectDataExportService projectDataExportService,
        Func<ProjectWorkspaceItemViewModel> getProject,
        Func<IReadOnlyList<ApiEndpointDto>, Task> syncImportedInterfacesAsync,
        Action<string> setStatusMessage)
    {
        _projectId = projectId;
        _apiWorkspaceService = apiWorkspaceService;
        _filePickerService = filePickerService;
        _appNotificationService = appNotificationService;
        _projectDataExportService = projectDataExportService;
        _getProject = getProject;
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

    protected override void DisposeManaged()
    {
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

    public void ResetImportedDocuments()
    {
        ImportedApiDocuments.Clear();
        _hasLoadedImportedDocuments = false;
        SelectedImportFilePath = string.Empty;
        ImportUrl = string.Empty;
        SetImportDataStatus(ImportTexts.EmptyStateStatus, ImportStatusStates.Info);
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
            ImportTexts.ImportLocalBusyText,
            SwaggerImportTextBundle);
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
            ImportTexts.ImportUrlBusyText,
            SwaggerImportTextBundle);
    }

    [RelayCommand]
    private async Task ImportProjectDataPackageFileAsync()
    {
        if (IsImportDataBusy)
        {
            return;
        }

        var filePath = await _filePickerService.PickProjectDataPackageFileAsync(CancellationToken.None);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            SetImportDataStatus(ImportTexts.PickPackageCancelledStatus, ImportStatusStates.Info);
            _setStatusMessage(ImportTexts.PickPackageCancelledStatus);
            return;
        }

        await ImportSwaggerAsync(
            cancellationToken => _projectDataExportService.PreviewImportPackageAsync(_projectId, filePath, cancellationToken),
            cancellationToken => _projectDataExportService.ImportPackageAsync(_projectId, filePath, cancellationToken),
            document => ImportTexts.FormatPackageImportSuccess(document.Name),
            ImportTexts.PreviewPackageBusyText,
            ImportTexts.ImportPackageBusyText,
            ProjectPackageImportTextBundle);
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
                _pendingImportRequest.OperationTexts,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            HandleUnexpectedImportFailure(exception, _pendingImportRequest.OperationTexts);
        }
        finally
        {
            IsImportDataBusy = false;
        }
    }

    public async Task LoadImportedDocumentsAsync(bool manageBusyState = true)
    {
        if (IsDisposed)
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
            _hasLoadedImportedDocuments = true;

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

    [RelayCommand]
    private async Task ExportProjectDataAsync()
    {
        if (IsImportDataBusy)
        {
            return;
        }

        var project = _getProject();
        var suggestedFileName = BuildSuggestedExportFileName(project.Name);
        var filePath = await _filePickerService.SaveProjectDataExportFileAsync(suggestedFileName, CancellationToken.None);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            SetImportDataStatus(ImportTexts.ExportCancelledStatus, ImportStatusStates.Info);
            _setStatusMessage(ImportTexts.ExportCancelledStatus);
            return;
        }

        var cancellationToken = CancellationTokenSourceHelper.Refresh(ref _importCancellationTokenSource).Token;
        ImportDataBusyText = ImportTexts.ExportBusyProcessing;
        IsImportDataBusy = true;
        try
        {
            var exportResult = await _projectDataExportService.ExportAsync(new ProjectDataExportRequestDto
            {
                ProjectId = _projectId,
                ProjectName = project.Name,
                ProjectDescription = project.Description,
                OutputFilePath = filePath
            }, cancellationToken);

            if (!exportResult.IsSuccess || exportResult.Data is null)
            {
                var failureMessage = string.IsNullOrWhiteSpace(exportResult.Message)
                    ? ImportTexts.ExportFailureFallback
                    : exportResult.Message;
                SetImportDataStatus(failureMessage, ImportStatusStates.Error);
                _setStatusMessage(failureMessage);
                PublishGlobalNotification("项目数据导出失败", failureMessage, NotificationType.Error);
                return;
            }

            var successMessage = ImportTexts.FormatExportSuccess(
                exportResult.Data.InterfaceCount,
                exportResult.Data.TestCaseCount,
                Path.GetFileName(exportResult.Data.FilePath));
            SetImportDataStatus(successMessage, ImportStatusStates.Success);
            _setStatusMessage(successMessage);
            PublishGlobalNotification("项目数据导出成功", successMessage, NotificationType.Success);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            var failureMessage = string.IsNullOrWhiteSpace(exception.Message)
                ? ImportTexts.ExportFailureFallback
                : $"{ImportTexts.ExportFailureFallback} {exception.Message}";
            SetImportDataStatus(failureMessage, ImportStatusStates.Error);
            _setStatusMessage(failureMessage);
            PublishGlobalNotification("项目数据导出失败", failureMessage, NotificationType.Error);
        }
        finally
        {
            IsImportDataBusy = false;
        }
    }

    public async Task EnsureImportedDocumentsLoadedAsync()
    {
        if (_hasLoadedImportedDocuments)
        {
            return;
        }

        await LoadImportedDocumentsAsync();
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
        string importBusyText,
        ImportOperationTextBundle operationTexts)
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
                    ? operationTexts.PreviewFailureFallback
                    : previewResult.Message;
                SetImportDataStatus(failureMessage, ImportStatusStates.Error);
                _setStatusMessage(failureMessage);
                PublishGlobalNotification(operationTexts.FailureNotificationTitle, failureMessage, NotificationType.Error);
                return;
            }

            if (previewResult.Data.ConflictCount > 0)
            {
                _pendingImportRequest = new PendingImportRequest(importAction, buildSuccessMessage, importBusyText, operationTexts);
                PendingImportPreview = previewResult.Data;
                IsOverwriteConfirmDialogOpen = true;
                SetImportDataStatus(
                    ImportTexts.FormatOverwriteDetectedStatus(previewResult.Data.ConflictCount),
                    ImportStatusStates.Info);
                _setStatusMessage(operationTexts.OverwritePendingShellStatus);
                return;
            }

            await ExecuteImportAsync(importAction, buildSuccessMessage, importBusyText, operationTexts, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            HandleUnexpectedImportFailure(exception, operationTexts);
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
        ImportOperationTextBundle operationTexts,
        CancellationToken cancellationToken)
    {
        ImportDataBusyText = importBusyText;
        var result = await importAction(cancellationToken);
        if (!result.IsSuccess || result.Data is null)
        {
            var failureMessage = string.IsNullOrWhiteSpace(result.Message)
                ? operationTexts.ImportFailureFallback
                : result.Message;
            SetImportDataStatus(failureMessage, ImportStatusStates.Error);
            _setStatusMessage(failureMessage);
            PublishGlobalNotification(operationTexts.FailureNotificationTitle, failureMessage, NotificationType.Error);
            return;
        }

        ImportDataBusyText = ImportTexts.BusyRefreshResult;
        await LoadImportedDocumentsAsync(manageBusyState: false);
        var successMessage = buildSuccessMessage(result.Data);
        ClearPendingImportConfirmation();
        SetImportDataStatus(successMessage, ImportStatusStates.Success);
        IsDialogOpen = false;
        _setStatusMessage(successMessage);
        PublishGlobalNotification(operationTexts.SuccessNotificationTitle, successMessage, NotificationType.Success);
    }

    private void HandleUnexpectedImportFailure(Exception exception, ImportOperationTextBundle operationTexts)
    {
        ClearPendingImportConfirmation();

        var failureMessage = string.IsNullOrWhiteSpace(exception.Message)
            ? operationTexts.UnexpectedFailureFallback
            : $"{operationTexts.UnexpectedFailureFallback} {exception.Message}";

        SetImportDataStatus(failureMessage, ImportStatusStates.Error);
        _setStatusMessage(failureMessage);
        PublishGlobalNotification(operationTexts.FailureNotificationTitle, failureMessage, NotificationType.Error);
    }

    private void HandleUnexpectedImportFailure(Exception exception, string fallbackMessage)
    {
        ClearPendingImportConfirmation();

        var failureMessage = string.IsNullOrWhiteSpace(exception.Message)
            ? fallbackMessage
            : $"{fallbackMessage} {exception.Message}";

        SetImportDataStatus(failureMessage, ImportStatusStates.Error);
        _setStatusMessage(failureMessage);
        PublishGlobalNotification("Swagger 导入失败", failureMessage, NotificationType.Error);
    }

    private void PublishGlobalNotification(string title, string content, NotificationType type)
    {
        if (type == NotificationType.Success)
        {
            _appNotificationService.ShowSuccess(title, content);
            return;
        }

        if (type == NotificationType.Error)
        {
            _appNotificationService.ShowError(title, content);
            return;
        }

        _appNotificationService.Show(title, content, type);
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
        if (string.Equals(sourceType, "APIXPKG", StringComparison.OrdinalIgnoreCase))
        {
            return ImportTexts.SourceTypeProjectPackage;
        }

        return string.Equals(sourceType, "URL", StringComparison.OrdinalIgnoreCase)
            ? ImportTexts.SourceTypeUrl
            : ImportTexts.SourceTypeFile;
    }

    private static string BuildSuggestedExportFileName(string projectName)
    {
        var fallbackName = string.IsNullOrWhiteSpace(projectName) ? "project-data" : projectName.Trim();
        var invalidChars = Path.GetInvalidFileNameChars();
        var safeName = new string(fallbackName.Select(character => invalidChars.Contains(character) ? '-' : character).ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "project-data";
        }

        return $"{safeName}-{DateTime.Now:yyyyMMdd-HHmmss}.apixpkg.json";
    }

    private sealed class PendingImportRequest
    {
        public PendingImportRequest(
            Func<CancellationToken, Task<IResultModel<ApiDocumentDto>>> importAction,
            Func<ApiDocumentDto, string> buildSuccessMessage,
            string importBusyText,
            ImportOperationTextBundle operationTexts)
        {
            ImportAction = importAction;
            BuildSuccessMessage = buildSuccessMessage;
            ImportBusyText = importBusyText;
            OperationTexts = operationTexts;
        }

        public Func<CancellationToken, Task<IResultModel<ApiDocumentDto>>> ImportAction { get; }

        public Func<ApiDocumentDto, string> BuildSuccessMessage { get; }

        public string ImportBusyText { get; }

        public ImportOperationTextBundle OperationTexts { get; }
    }

    private sealed class ImportOperationTextBundle
    {
        public ImportOperationTextBundle(
            string successNotificationTitle,
            string failureNotificationTitle,
            string previewFailureFallback,
            string importFailureFallback,
            string unexpectedFailureFallback,
            string overwritePendingShellStatus)
        {
            SuccessNotificationTitle = successNotificationTitle;
            FailureNotificationTitle = failureNotificationTitle;
            PreviewFailureFallback = previewFailureFallback;
            ImportFailureFallback = importFailureFallback;
            UnexpectedFailureFallback = unexpectedFailureFallback;
            OverwritePendingShellStatus = overwritePendingShellStatus;
        }

        public string SuccessNotificationTitle { get; }

        public string FailureNotificationTitle { get; }

        public string PreviewFailureFallback { get; }

        public string ImportFailureFallback { get; }

        public string UnexpectedFailureFallback { get; }

        public string OverwritePendingShellStatus { get; }
    }
}
