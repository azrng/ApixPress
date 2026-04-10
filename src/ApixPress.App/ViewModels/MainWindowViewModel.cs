using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels.Base;
using Azrng.Core.Results;

namespace ApixPress.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IRequestExecutionService _requestExecutionService;
    private readonly IRequestCaseService _requestCaseService;
    private readonly IRequestHistoryService _requestHistoryService;
    private bool _initialized;

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

        ProjectPanel.SelectedProjectChanged += OnSelectedProjectChanged;
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

    public bool HasProjectContext => ProjectPanel.SelectedProject is not null;

    public bool HasEnvironmentContext => EnvironmentPanel.HasSelectedEnvironment;

    public bool ShowProjectEmptyState => !HasProjectContext;

    [ObservableProperty]
    private bool hasNoHistory = true;

    [ObservableProperty]
    private string historySearchText = string.Empty;

    public IReadOnlyList<string> HttpMethods { get; } = ["GET", "POST", "PUT", "DELETE", "PATCH"];

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = "请先创建项目。";

    [ObservableProperty]
    private string selectedMethod = "GET";

    [ObservableProperty]
    private string requestUrl = string.Empty;

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        IsBusy = true;
        await ProjectPanel.LoadProjectsAsync();
        if (ProjectPanel.SelectedProject is not null)
        {
            await LoadWorkspaceForProjectAsync(ProjectPanel.SelectedProject.Id);
            StatusMessage = $"当前项目：{ProjectPanel.SelectedProject.Name}。";
        }
        else
        {
            ClearWorkspaceContext();
            StatusMessage = "请先创建项目。";
        }

        IsBusy = false;
        NotifyWorkspaceState();
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

        StatusMessage = $"已加载 {item.Timestamp.ToLocalTime():yyyy-MM-dd HH:mm} 的请求。";
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
            StatusMessage = "请先创建或选择一个项目。";
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
        await ProjectPanel.LoadProjectsAsync(ProjectPanel.SelectedProject?.Id);
        if (ProjectPanel.SelectedProject is not null)
        {
            await LoadWorkspaceForProjectAsync(ProjectPanel.SelectedProject.Id, EnvironmentPanel.SelectedEnvironment?.Id);
            StatusMessage = $"项目 {ProjectPanel.SelectedProject.Name} 已刷新。";
        }
        else
        {
            ClearWorkspaceContext();
            StatusMessage = "当前没有可用项目。";
        }

        IsBusy = false;
        NotifyWorkspaceState();
    }

    private async Task LoadWorkspaceForProjectAsync(string projectId, string? preferredEnvironmentId = null)
    {
        UseCasesPanel.SetProjectContext(projectId);
        HistoryPanel.SetProjectContext(projectId);
        await EnvironmentPanel.LoadProjectAsync(projectId, preferredEnvironmentId);
        await UseCasesPanel.LoadCasesAsync();
        await RefreshHistoryAsync();
        NotifyWorkspaceState();
    }

    private void ClearWorkspaceContext()
    {
        UseCasesPanel.ClearProjectContext();
        HistoryPanel.ClearProjectContext();
        EnvironmentPanel.ClearProjectContext();
        HasNoHistory = true;
        NotifyWorkspaceState();
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
        StatusMessage = "已加载选中用例。";
    }

    private void OnSelectedProjectChanged(ProjectWorkspaceItemViewModel? project)
    {
        _ = HandleProjectSelectionChangedAsync(project);
    }

    private async Task HandleProjectSelectionChangedAsync(ProjectWorkspaceItemViewModel? project)
    {
        IsBusy = true;
        if (project is null)
        {
            ClearWorkspaceContext();
            StatusMessage = "请先创建项目。";
        }
        else
        {
            await LoadWorkspaceForProjectAsync(project.Id);
            StatusMessage = $"已切换到项目：{project.Name}。";
        }

        IsBusy = false;
        NotifyWorkspaceState();
    }

    private void OnSelectedEnvironmentChanged(ProjectEnvironmentItemViewModel? environment)
    {
        NotifyWorkspaceState();
        if (environment is null)
        {
            StatusMessage = "当前项目尚未配置环境。";
            return;
        }

        StatusMessage = $"当前环境已切换为：{environment.Name}。";
    }

    private void NotifyWorkspaceState()
    {
        OnPropertyChanged(nameof(HasProjectContext));
        OnPropertyChanged(nameof(HasEnvironmentContext));
        OnPropertyChanged(nameof(ShowProjectEmptyState));
    }
}
