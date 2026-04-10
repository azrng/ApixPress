using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels.Base;
using Azrng.Core.Results;

namespace ApixPress.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private static class WorkspaceSections
    {
        public const string Workbench = "workbench";
        public const string History = "history";
        public const string ProjectSettings = "project_settings";
    }

    private readonly IRequestExecutionService _requestExecutionService;
    private readonly IRequestCaseService _requestCaseService;
    private readonly IRequestHistoryService _requestHistoryService;
    private bool _initialized;
    private bool _isNavigatingProjectSelection;

    public MainWindowViewModel(
        IRequestExecutionService requestExecutionService,
        IRequestCaseService requestCaseService,
        IRequestHistoryService requestHistoryService,
        IEnvironmentVariableService environmentVariableService,
        IProjectWorkspaceService projectWorkspaceService)
    {
        _requestExecutionService = requestExecutionService;
        _requestCaseService = requestCaseService;
        _requestHistoryService = requestHistoryService;

        ConfigTab = new RequestConfigTabViewModel(null);
        ResponseSection = new ResponseSectionViewModel();
        ProjectPanel = new ProjectPanelViewModel(projectWorkspaceService);
        UseCasesPanel = new UseCasesPanelViewModel(requestCaseService);
        EnvironmentPanel = new EnvironmentPanelViewModel(environmentVariableService);
        HistoryPanel = new RequestHistoryPanelViewModel(requestHistoryService);

        ProjectPanel.ProjectCreated += OnProjectCreated;
        ProjectPanel.PropertyChanged += OnProjectPanelPropertyChanged;
        ProjectPanel.SelectedProjectChanged += OnSelectedProjectChanged;
        EnvironmentPanel.PropertyChanged += OnEnvironmentPanelPropertyChanged;
        EnvironmentPanel.SelectedEnvironmentChanged += OnSelectedEnvironmentChanged;
        UseCasesPanel.CaseApplied += OnCaseApplied;
    }

    public RequestConfigTabViewModel ConfigTab { get; }
    public ResponseSectionViewModel ResponseSection { get; }
    public ProjectPanelViewModel ProjectPanel { get; }
    public UseCasesPanelViewModel UseCasesPanel { get; }
    public EnvironmentPanelViewModel EnvironmentPanel { get; }
    public RequestHistoryPanelViewModel HistoryPanel { get; }

    public ObservableCollection<RequestHistoryItemViewModel> RequestHistory => HistoryPanel.HistoryItems;

    public bool HasEnvironmentContext => EnvironmentPanel.HasSelectedEnvironment;
    public bool HasSelectedProject => ProjectPanel.SelectedProject is not null;
    public bool IsWorkspaceMode => !IsProjectBrowserMode && HasSelectedProject;
    public bool ShowProjectListEmptyState => IsProjectBrowserMode && !ProjectPanel.HasProjects;
    public bool ShowWorkbenchSection => IsWorkspaceMode && CurrentWorkspaceSection == WorkspaceSections.Workbench;
    public bool ShowHistorySection => IsWorkspaceMode && CurrentWorkspaceSection == WorkspaceSections.History;
    public bool ShowProjectSettingsSection => IsWorkspaceMode && CurrentWorkspaceSection == WorkspaceSections.ProjectSettings;

    public string CurrentProjectName => ProjectPanel.SelectedProject?.Name ?? "项目列表";
    public string CurrentProjectSummary =>
        string.IsNullOrWhiteSpace(ProjectPanel.SelectedProject?.Description)
            ? "本地 API 工作区"
            : ProjectPanel.SelectedProject.Description;
    public string CurrentEnvironmentLabel => EnvironmentPanel.SelectedEnvironment?.Name ?? "未选择环境";
    public string BrowserStatusText => ProjectPanel.HasProjects
        ? "选择一个项目进入工作台，或继续创建新项目。"
        : "当前还没有项目，请先创建一个项目。";

    public IReadOnlyList<string> HttpMethods { get; } = ["GET", "POST", "PUT", "DELETE", "PATCH"];

    [ObservableProperty]
    private bool hasNoHistory = true;

    [ObservableProperty]
    private string historySearchText = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = "欢迎使用 API 协作平台。";

    [ObservableProperty]
    private string selectedMethod = "GET";

    [ObservableProperty]
    private string requestUrl = string.Empty;

    [ObservableProperty]
    private bool isProjectBrowserMode = true;

    [ObservableProperty]
    private bool isCreateProjectDialogOpen;

    [ObservableProperty]
    private string currentWorkspaceSection = WorkspaceSections.Workbench;

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        IsBusy = true;
        await ProjectPanel.LoadProjectsAsync(autoSelect: false);
        ClearWorkspaceContext();
        IsProjectBrowserMode = true;
        StatusMessage = BrowserStatusText;
        IsBusy = false;
        NotifyShellState();
    }

    [RelayCommand]
    private async Task OpenProjectWorkspaceAsync(ProjectWorkspaceItemViewModel? project)
    {
        if (project is null)
        {
            return;
        }

        IsBusy = true;
        _isNavigatingProjectSelection = true;
        ProjectPanel.SelectedProject = project;
        _isNavigatingProjectSelection = false;

        await LoadWorkspaceForProjectAsync(project.Id);
        IsProjectBrowserMode = false;
        CurrentWorkspaceSection = WorkspaceSections.Workbench;
        StatusMessage = $"已进入项目：{project.Name}。";
        IsBusy = false;
        NotifyShellState();
    }

    [RelayCommand]
    private void BackToProjectList()
    {
        IsProjectBrowserMode = true;
        CurrentWorkspaceSection = WorkspaceSections.Workbench;
        IsCreateProjectDialogOpen = false;
        StatusMessage = BrowserStatusText;
        NotifyShellState();
    }

    [RelayCommand]
    private void OpenCreateProjectDialog()
    {
        IsCreateProjectDialogOpen = true;
        StatusMessage = "填写项目名称和备注信息后即可创建项目。";
        NotifyShellState();
    }

    [RelayCommand]
    private void CloseCreateProjectDialog()
    {
        IsCreateProjectDialogOpen = false;
        StatusMessage = BrowserStatusText;
        NotifyShellState();
    }

    [RelayCommand]
    private void ShowWorkbench()
    {
        CurrentWorkspaceSection = WorkspaceSections.Workbench;
        NotifyShellState();
    }

    [RelayCommand]
    private void ShowHistory()
    {
        CurrentWorkspaceSection = WorkspaceSections.History;
        NotifyShellState();
    }

    [RelayCommand]
    private void ShowProjectSettings()
    {
        CurrentWorkspaceSection = WorkspaceSections.ProjectSettings;
        NotifyShellState();
    }

    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        await HistoryPanel.ClearHistoryAsync();
        await RefreshHistoryAsync();
        StatusMessage = "当前项目的请求历史已清空。";
    }

    [RelayCommand]
    private void LoadHistoryItem(RequestHistoryItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        SelectedMethod = item.Method;
        RequestUrl = item.Url;
        ConfigTab.ApplySnapshot(item.RequestSnapshot);

        if (item.ResponseSnapshot is not null)
        {
            ResponseSection.ApplyResult(
                ResultModel<ResponseSnapshotDto>.Success(item.ResponseSnapshot),
                item.RequestSnapshot);
        }

        CurrentWorkspaceSection = WorkspaceSections.Workbench;
        StatusMessage = $"已加载 {item.Timestamp.ToLocalTime():yyyy-MM-dd HH:mm} 的请求。";
        NotifyShellState();
    }

    [RelayCommand]
    private async Task SaveHistoryAsCaseAsync(RequestHistoryItemViewModel? item)
    {
        if (item is null || ProjectPanel.SelectedProject is null)
        {
            return;
        }

        var snapshot = item.RequestSnapshot;
        var caseDto = new RequestCaseDto
        {
            ProjectId = ProjectPanel.SelectedProject.Id,
            Name = $"{snapshot.Method} {snapshot.Url}",
            GroupName = "历史导入",
            Description = $"从 {item.Timestamp.ToLocalTime():yyyy-MM-dd HH:mm} 的历史记录创建",
            RequestSnapshot = snapshot,
            UpdatedAt = DateTime.UtcNow
        };

        await _requestCaseService.SaveAsync(caseDto, CancellationToken.None);
        await UseCasesPanel.LoadCasesAsync();
        StatusMessage = "已保存为当前项目用例。";
    }

    [RelayCommand]
    private async Task SendRequestAsync()
    {
        if (ProjectPanel.SelectedProject is null)
        {
            StatusMessage = "请先选择一个项目。";
            return;
        }

        var environment = EnvironmentPanel.GetSelectedEnvironmentDto();
        if (environment is null)
        {
            StatusMessage = "请先为当前项目创建并选择环境。";
            return;
        }

        if (string.IsNullOrWhiteSpace(RequestUrl))
        {
            StatusMessage = "请求地址不能为空。";
            return;
        }

        IsBusy = true;
        var snapshot = ConfigTab.BuildRequestSnapshot(
            string.Empty,
            SelectedMethod,
            RequestUrl);
        var result = await _requestExecutionService.SendAsync(snapshot, environment, CancellationToken.None);
        IsBusy = false;

        ResponseSection.ApplyResult(result, snapshot);
        StatusMessage = result.IsSuccess ? "请求发送完成。" : result.Message;

        if (result.IsSuccess || result.Data is not null)
        {
            await _requestHistoryService.AddAsync(ProjectPanel.SelectedProject.Id, snapshot, result.Data, CancellationToken.None);
            await RefreshHistoryAsync();
        }
    }

    [RelayCommand]
    private async Task SaveCaseAsync()
    {
        if (ProjectPanel.SelectedProject is null)
        {
            StatusMessage = "请先选择项目后再保存用例。";
            return;
        }

        var snapshot = ConfigTab.BuildRequestSnapshot(
            string.Empty,
            SelectedMethod,
            RequestUrl);
        await UseCasesPanel.SaveCaseAsync(snapshot);
        StatusMessage = "用例已保存。";
    }

    [RelayCommand]
    private async Task RefreshWorkspaceAsync()
    {
        IsBusy = true;
        if (IsProjectBrowserMode)
        {
            await ProjectPanel.LoadProjectsAsync(autoSelect: false);
            StatusMessage = BrowserStatusText;
        }
        else if (ProjectPanel.SelectedProject is not null)
        {
            await ProjectPanel.LoadProjectsAsync(ProjectPanel.SelectedProject.Id);
            await LoadWorkspaceForProjectAsync(ProjectPanel.SelectedProject.Id, EnvironmentPanel.SelectedEnvironment?.Id);
            StatusMessage = $"项目 {ProjectPanel.SelectedProject.Name} 已刷新。";
        }
        else
        {
            await ProjectPanel.LoadProjectsAsync(autoSelect: false);
            ClearWorkspaceContext();
            IsProjectBrowserMode = true;
            StatusMessage = BrowserStatusText;
        }

        IsBusy = false;
        NotifyShellState();
    }

    private async Task LoadWorkspaceForProjectAsync(string projectId, string? preferredEnvironmentId = null)
    {
        UseCasesPanel.SetProjectContext(projectId);
        HistoryPanel.SetProjectContext(projectId);
        await EnvironmentPanel.LoadProjectAsync(projectId, preferredEnvironmentId);
        await UseCasesPanel.LoadCasesAsync();
        await RefreshHistoryAsync();
        NotifyShellState();
    }

    private void ClearWorkspaceContext()
    {
        UseCasesPanel.ClearProjectContext();
        HistoryPanel.ClearProjectContext();
        EnvironmentPanel.ClearProjectContext();
        HasNoHistory = true;
        NotifyShellState();
    }

    private async Task RefreshHistoryAsync()
    {
        await HistoryPanel.LoadHistoryAsync();
        HasNoHistory = RequestHistory.Count == 0;
    }

    private void OnCaseApplied(RequestSnapshotDto snapshot)
    {
        SelectedMethod = snapshot.Method;
        RequestUrl = snapshot.Url;
        ConfigTab.ApplySnapshot(snapshot);
        CurrentWorkspaceSection = WorkspaceSections.Workbench;
        StatusMessage = "已加载选中用例。";
        NotifyShellState();
    }

    private void OnSelectedProjectChanged(ProjectWorkspaceItemViewModel? project)
    {
        if (_isNavigatingProjectSelection)
        {
            return;
        }

        _ = HandleProjectSelectionChangedAsync(project);
    }

    private async Task HandleProjectSelectionChangedAsync(ProjectWorkspaceItemViewModel? project)
    {
        if (IsProjectBrowserMode)
        {
            NotifyShellState();
            return;
        }

        IsBusy = true;
        if (project is null)
        {
            ClearWorkspaceContext();
            IsProjectBrowserMode = true;
            StatusMessage = BrowserStatusText;
        }
        else
        {
            await LoadWorkspaceForProjectAsync(project.Id);
            StatusMessage = $"已切换到项目：{project.Name}。";
        }

        IsBusy = false;
        NotifyShellState();
    }

    private void OnSelectedEnvironmentChanged(ProjectEnvironmentItemViewModel? environment)
    {
        NotifyShellState();
        if (environment is null)
        {
            StatusMessage = "当前项目尚未配置环境。";
            return;
        }

        StatusMessage = $"当前环境已切换为：{environment.Name}。";
    }

    private void OnProjectCreated()
    {
        IsCreateProjectDialogOpen = false;
        StatusMessage = "项目已创建，可点击卡片进入项目详情。";
        NotifyShellState();
    }

    private void OnProjectPanelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProjectPanelViewModel.SelectedProject)
            or nameof(ProjectPanelViewModel.HasProjects)
            or nameof(ProjectPanelViewModel.SearchText))
        {
            NotifyShellState();
        }
    }

    private void OnEnvironmentPanelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EnvironmentPanelViewModel.SelectedEnvironment)
            or nameof(EnvironmentPanelViewModel.HasSelectedEnvironment)
            or nameof(EnvironmentPanelViewModel.ActiveEnvironmentName))
        {
            NotifyShellState();
        }
    }

    partial void OnIsProjectBrowserModeChanged(bool value)
    {
        NotifyShellState();
    }

    partial void OnCurrentWorkspaceSectionChanged(string value)
    {
        NotifyShellState();
    }

    private void NotifyShellState()
    {
        OnPropertyChanged(nameof(HasEnvironmentContext));
        OnPropertyChanged(nameof(HasSelectedProject));
        OnPropertyChanged(nameof(IsWorkspaceMode));
        OnPropertyChanged(nameof(ShowProjectListEmptyState));
        OnPropertyChanged(nameof(ShowWorkbenchSection));
        OnPropertyChanged(nameof(ShowHistorySection));
        OnPropertyChanged(nameof(ShowProjectSettingsSection));
        OnPropertyChanged(nameof(CurrentProjectName));
        OnPropertyChanged(nameof(CurrentProjectSummary));
        OnPropertyChanged(nameof(CurrentEnvironmentLabel));
        OnPropertyChanged(nameof(BrowserStatusText));
    }
}
