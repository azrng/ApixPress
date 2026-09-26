using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ApixPress.App.Messages;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels.Base;
using Irihi.Avalonia.Shared.Contracts;
using Ursa.Controls;

namespace ApixPress.App.ViewModels;

public partial class RequestCaseDeliveryViewModel : ViewModelBase, IDialogContext
{
    private const string ExportMode = "export";
    private const string ImportMode = "import";

    private readonly string _projectId;
    private readonly IFilePickerService _filePickerService;
    private readonly IProjectDataExportService _projectDataExportService;
    private readonly Func<ProjectWorkspaceItemViewModel> _getProject;
    private readonly IMessenger _messenger;
    private readonly IDialogService _dialogService;

    public RequestCaseDeliveryViewModel(
        string projectId,
        IFilePickerService filePickerService,
        IProjectDataExportService projectDataExportService,
        Func<ProjectWorkspaceItemViewModel> getProject,
        IMessenger messenger,
        IDialogService dialogService)
    {
        _projectId = projectId;
        _filePickerService = filePickerService;
        _projectDataExportService = projectDataExportService;
        _getProject = getProject;
        _messenger = messenger;
        _dialogService = dialogService;
    }

    public ObservableCollection<RequestCaseDeliveryItemViewModel> Interfaces { get; } = [];
    public ObservableCollection<RequestCaseDeliveryItemViewModel> RequestCases { get; } = [];
    public ObservableCollection<RequestCaseDeliveryPreviewItemViewModel> PreviewItems { get; } = [];
    public bool IsExportMode => Mode == ExportMode;
    public bool IsImportMode => Mode == ImportMode;
    public bool CanEditDialog => !IsBusy;
    public bool HasPreview => Preview is not null;
    public bool CanExport => !IsBusy && Interfaces.Any(item => item.IsSelected);
    public bool CanImport => !IsBusy && PreviewItems.Count > 0;
    public int SelectedInterfaceCount => Interfaces.Count(item => item.IsSelected);
    public int SelectedRequestCaseCount => RequestCases.Count(item => item.IsSelected);
    public string SelectedSummaryText => $"已选择 {SelectedInterfaceCount} 个接口、{SelectedRequestCaseCount} 个请求用例（含自动携带父接口）";
    public string FileName => string.IsNullOrWhiteSpace(FilePath) ? "尚未选择交付包" : Path.GetFileName(FilePath);
    public string DialogTitle => IsImportMode ? "导入请求用例交付包" : "导出请求用例交付包";
    public string BusyText => IsImportMode ? "正在处理请求用例交付包..." : "正在导出请求用例交付包...";
    public string DefaultStatus => IsImportMode ? "导入前会先预览接口、请求用例和环境冲突。" : "选择接口或请求用例；选择请求用例时会自动携带父接口。";
    public IReadOnlyList<RequestCaseDeliveryActionOption> DecisionActions => RequestCaseDeliveryPreviewItemViewModel.ActionOptions;

    [ObservableProperty]
    private string mode = ExportMode;

    [ObservableProperty]
    private string filePath = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusText = string.Empty;

    [ObservableProperty]
    private RequestCaseDeliveryPreviewDto? preview;

    public event EventHandler<object?>? RequestClose;

    public void Close() => RequestClose?.Invoke(this, DialogResult.None);

    [RelayCommand]
    private void CloseDialog() => Close();

    [RelayCommand]
    private async Task OpenExportAsync()
    {
        if (IsBusy)
        {
            return;
        }

        Mode = ExportMode;
        ResetState();
        var result = await _projectDataExportService.GetRequestCaseDeliveryOptionsAsync(_projectId, CancellationToken.None);
        if (!result.IsSuccess || result.Data is null)
        {
            PublishFailure(result.Message ?? "读取请求用例交付范围失败。");
            return;
        }

        LoadCandidates(result.Data);
        StatusText = $"当前项目包含 {result.Data.EnvironmentCount} 个环境、{result.Data.VariableCount} 个变量。";
        await _dialogService.ShowDialogAsync<Views.Controls.RequestCaseDeliveryDialogView>(this, DialogTitle);
    }

    [RelayCommand]
    private async Task OpenImportAsync()
    {
        if (IsBusy)
        {
            return;
        }

        var path = await _filePickerService.PickRequestCaseDeliveryFileAsync(CancellationToken.None);
        if (string.IsNullOrWhiteSpace(path))
        {
            _messenger.Send(new StatusMessageRequest("未选择请求用例交付包。"));
            return;
        }

        Mode = ImportMode;
        ResetState();
        FilePath = path;
        IsBusy = true;
        try
        {
            var result = await _projectDataExportService.PreviewRequestCaseDeliveryAsync(
                _projectId, path, CancellationToken.None);
            if (!result.IsSuccess || result.Data is null)
            {
                PublishFailure(result.Message ?? "请求用例交付包预览失败。");
                return;
            }

            Preview = result.Data;
            foreach (var item in result.Data.Items)
            {
                PreviewItems.Add(new RequestCaseDeliveryPreviewItemViewModel(item));
            }

            StatusText = $"来源项目：{result.Data.SourceProjectName}；接口 {result.Data.InterfaceCount} 个、请求用例 {result.Data.RequestCaseCount} 个、环境 {result.Data.EnvironmentCount} 个。";
            await _dialogService.ShowDialogAsync<Views.Controls.RequestCaseDeliveryDialogView>(this, DialogTitle);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (!CanExport)
        {
            StatusText = "请至少选择一个接口或请求用例。";
            return;
        }

        var project = _getProject();
        var path = await _filePickerService.SaveRequestCaseDeliveryFileAsync(
            BuildSuggestedFileName(project.Name), CancellationToken.None);
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusText = "已取消导出请求用例交付包。";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _projectDataExportService.ExportRequestCaseDeliveryAsync(
                new RequestCaseDeliveryRequestDto
                {
                    ProjectId = _projectId,
                    ProjectName = project.Name,
                    OutputFilePath = path,
                    SelectedInterfaceIds = Interfaces.Where(item => item.IsSelected).Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase),
                    SelectedRequestCaseIds = RequestCases.Where(item => item.IsSelected).Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase)
                }, CancellationToken.None);
            if (!result.IsSuccess || result.Data is null)
            {
                PublishFailure(result.Message ?? "请求用例交付包导出失败。");
                return;
            }

            StatusText = $"已导出 {result.Data.InterfaceCount} 个接口、{result.Data.RequestCaseCount} 个请求用例和 {result.Data.EnvironmentCount} 个环境。";
            _messenger.Send(new StatusMessageRequest(StatusText));
            RequestClose?.Invoke(this, DialogResult.OK);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        if (!CanImport || string.IsNullOrWhiteSpace(FilePath))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var decisions = PreviewItems.Select(item => new RequestCaseDeliveryDecisionDto
            {
                Kind = item.Kind,
                SourceId = item.SourceId,
                Action = item.SelectedAction.Key
            }).ToList();
            var result = await _projectDataExportService.ImportRequestCaseDeliveryAsync(
                _projectId, FilePath, decisions, CancellationToken.None);
            if (!result.IsSuccess || result.Data is null)
            {
                PublishFailure(result.Message ?? "请求用例交付包导入失败。");
                return;
            }

            StatusText = $"已导入 {result.Data.InterfaceCount} 个接口、{result.Data.RequestCaseCount} 个请求用例和 {result.Data.EnvironmentCount} 个环境。";
            _messenger.Send(new StatusMessageRequest(StatusText));
            RequestClose?.Invoke(this, DialogResult.OK);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectAllInterfaces()
    {
        foreach (var item in Interfaces)
        {
            item.IsSelected = true;
            item.IsAutoIncluded = false;
        }
        NotifySelectionChanged();
    }

    [RelayCommand]
    private void SelectAllRequestCases()
    {
        foreach (var item in RequestCases)
        {
            item.IsSelected = true;
        }
        SyncParentInterfaces();
        NotifySelectionChanged();
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var item in Interfaces.Concat(RequestCases))
        {
            item.IsSelected = false;
            item.IsAutoIncluded = false;
        }
        NotifySelectionChanged();
    }

    protected override void DisposeManaged()
    {
        foreach (var item in Interfaces.Concat(RequestCases))
        {
            item.PropertyChanged -= OnCandidatePropertyChanged;
        }
        base.DisposeManaged();
    }

    partial void OnModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsExportMode));
        OnPropertyChanged(nameof(IsImportMode));
        OnPropertyChanged(nameof(DialogTitle));
    }

    partial void OnFilePathChanged(string value) => OnPropertyChanged(nameof(FileName));

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEditDialog));
        OnPropertyChanged(nameof(CanExport));
        OnPropertyChanged(nameof(CanImport));
    }

    partial void OnPreviewChanged(RequestCaseDeliveryPreviewDto? value)
    {
        OnPropertyChanged(nameof(HasPreview));
        OnPropertyChanged(nameof(CanImport));
    }

    private void LoadCandidates(RequestCaseDeliveryOptionsDto options)
    {
        Interfaces.Clear();
        RequestCases.Clear();
        foreach (var item in options.Interfaces)
        {
            AddCandidate(Interfaces, item);
        }
        foreach (var item in options.RequestCases)
        {
            AddCandidate(RequestCases, item);
        }
        NotifySelectionChanged();
    }

    private void AddCandidate(Collection<RequestCaseDeliveryItemViewModel> target, RequestCaseDeliveryCandidateDto item)
    {
        var viewModel = new RequestCaseDeliveryItemViewModel
        {
            Id = item.Id,
            ParentId = item.ParentId,
            Kind = item.EntryType,
            Name = item.Name,
            Method = item.Method,
            Url = item.Url,
            FolderPath = item.FolderPath,
            IsSelected = item.IsSelected,
            IsAutoIncluded = item.IsAutoIncluded
        };
        viewModel.PropertyChanged += OnCandidatePropertyChanged;
        target.Add(viewModel);
    }

    private void OnCandidatePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(RequestCaseDeliveryItemViewModel.IsSelected))
        {
            SyncParentInterfaces();
            NotifySelectionChanged();
        }
    }

    private void SyncParentInterfaces()
    {
        foreach (var parent in Interfaces)
        {
            var hasSelectedCase = RequestCases.Any(item => item.ParentId == parent.Id && item.IsSelected);
            if (hasSelectedCase)
            {
                parent.IsSelected = true;
                parent.IsAutoIncluded = true;
            }
            else if (parent.IsAutoIncluded)
            {
                parent.IsSelected = false;
                parent.IsAutoIncluded = false;
            }
        }
    }

    private void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(CanExport));
        OnPropertyChanged(nameof(SelectedInterfaceCount));
        OnPropertyChanged(nameof(SelectedRequestCaseCount));
        OnPropertyChanged(nameof(SelectedSummaryText));
    }

    private void ResetState()
    {
        FilePath = string.Empty;
        Preview = null;
        PreviewItems.Clear();
        StatusText = DefaultStatus;
        ClearSelection();
    }

    private void PublishFailure(string message)
    {
        StatusText = message;
        _messenger.Send(new StatusMessageRequest(message));
    }

    private static string BuildSuggestedFileName(string projectName)
    {
        var fallback = string.IsNullOrWhiteSpace(projectName) ? "request-case-delivery" : projectName.Trim();
        var invalidChars = Path.GetInvalidFileNameChars();
        var safeName = new string(fallback.Select(character => invalidChars.Contains(character) ? '-' : character).ToArray()).Trim();
        return $"{(string.IsNullOrWhiteSpace(safeName) ? "request-case-delivery" : safeName)}-{DateTime.Now:yyyyMMdd-HHmmss}.apixdelivery.json";
    }
}
