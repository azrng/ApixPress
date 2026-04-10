using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels.Base;
using Azrng.Core.Results;

namespace ApixPress.App.ViewModels;

public partial class ProjectTabViewModel : ViewModelBase
{
    private readonly IRequestCaseService _requestCaseService;
    private readonly IRequestExecutionService _requestExecutionService;
    private readonly IRequestHistoryService _requestHistoryService;
    private bool _initialized;

    public event Action<ProjectTabViewModel>? ShellStateChanged;

    public ProjectTabViewModel(
        ProjectWorkspaceItemViewModel project,
        IRequestExecutionService requestExecutionService,
        IRequestCaseService requestCaseService,
        IRequestHistoryService requestHistoryService,
        IEnvironmentVariableService environmentVariableService)
    {
        Project = new ProjectWorkspaceItemViewModel
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            IsDefault = project.IsDefault
        };
        _requestExecutionService = requestExecutionService;
        _requestCaseService = requestCaseService;
        _requestHistoryService = requestHistoryService;

        ConfigTab = new RequestConfigTabViewModel(null);
        ResponseSection = new ResponseSectionViewModel();
        EnvironmentPanel = new EnvironmentPanelViewModel(environmentVariableService);
        UseCasesPanel = new UseCasesPanelViewModel(requestCaseService);
        HistoryPanel = new RequestHistoryPanelViewModel(requestHistoryService);

        Project.PropertyChanged += (_, _) => NotifyShellState();
        EnvironmentPanel.SelectedEnvironmentChanged += OnSelectedEnvironmentChanged;
        UseCasesPanel.CaseApplied += OnCaseApplied;
        UseCasesPanel.RequestCases.CollectionChanged += (_, _) => NotifyShellState();
        HistoryPanel.HistoryItems.CollectionChanged += (_, _) => NotifyShellState();
    }

    public ProjectWorkspaceItemViewModel Project { get; }
    public RequestConfigTabViewModel ConfigTab { get; }
    public ResponseSectionViewModel ResponseSection { get; }
    public EnvironmentPanelViewModel EnvironmentPanel { get; }
    public UseCasesPanelViewModel UseCasesPanel { get; }
    public RequestHistoryPanelViewModel HistoryPanel { get; }

    public ObservableCollection<RequestCaseItemViewModel> SavedRequests => UseCasesPanel.RequestCases;
    public ObservableCollection<RequestHistoryItemViewModel> RequestHistory => HistoryPanel.HistoryItems;

    public string ProjectId => Project.Id;
    public string TabTitle => Project.Name;
    public string ProjectSummary => string.IsNullOrWhiteSpace(Project.Description) ? "暂无项目备注" : Project.Description;
    public string CurrentEnvironmentLabel => EnvironmentPanel.SelectedEnvironment?.Name ?? "请选择环境";
    public string CurrentBaseUrlText => string.IsNullOrWhiteSpace(EnvironmentPanel.SelectedEnvironment?.BaseUrl)
        ? "当前环境暂未配置前置 URL"
        : EnvironmentPanel.SelectedEnvironment.BaseUrl;
    public bool HasEnvironmentContext => EnvironmentPanel.HasSelectedEnvironment;
    public bool HasSavedRequests => SavedRequests.Count > 0;
    public bool HasHistory => RequestHistory.Count > 0;
    public bool ShowSavedRequestsEmptyState => !HasSavedRequests;
    public bool ShowHistoryEmptyState => !HasHistory;

    [ObservableProperty]
    private bool isActive;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = "快捷请求已就绪。";

    [ObservableProperty]
    private string selectedMethod = "GET";

    [ObservableProperty]
    private string requestUrl = string.Empty;

    public IReadOnlyList<string> HttpMethods { get; } = ["GET", "POST", "PUT", "DELETE", "PATCH"];

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        await LoadWorkspaceAsync();
    }

    public async Task RefreshAsync()
    {
        await LoadWorkspaceAsync(EnvironmentPanel.SelectedEnvironment?.Id);
        StatusMessage = $"项目 {Project.Name} 已刷新。";
        NotifyShellState();
    }

    public async Task SaveCurrentEnvironmentAsync()
    {
        if (!HasEnvironmentContext)
        {
            StatusMessage = "请先选择环境后再保存。";
            NotifyShellState();
            return;
        }

        await EnvironmentPanel.SaveEnvironmentCommand.ExecuteAsync(null);
        StatusMessage = $"环境 {CurrentEnvironmentLabel} 已保存。";
        NotifyShellState();
    }

    public async Task SendQuickRequestAsync()
    {
        var environment = EnvironmentPanel.GetSelectedEnvironmentDto();
        if (environment is null)
        {
            StatusMessage = "请先选择一个环境。";
            NotifyShellState();
            return;
        }

        if (string.IsNullOrWhiteSpace(RequestUrl))
        {
            StatusMessage = "请输入请求地址。";
            NotifyShellState();
            return;
        }

        IsBusy = true;
        var snapshot = BuildCurrentSnapshot();
        var result = await _requestExecutionService.SendAsync(snapshot, environment, CancellationToken.None);
        ResponseSection.ApplyResult(result, snapshot);

        if (result.IsSuccess || result.Data is not null)
        {
            await _requestHistoryService.AddAsync(ProjectId, snapshot, result.Data, CancellationToken.None);
            await HistoryPanel.LoadHistoryAsync();
        }

        IsBusy = false;
        StatusMessage = result.IsSuccess ? "快捷请求发送完成。" : result.Message;
        NotifyShellState();
    }

    public async Task SaveQuickRequestAsync(string groupName)
    {
        var snapshot = BuildCurrentSnapshot();
        var requestName = BuildRequestName();
        var result = await _requestCaseService.SaveAsync(new RequestCaseDto
        {
            ProjectId = ProjectId,
            Name = requestName,
            GroupName = groupName,
            Description = ConfigTab.RequestDescription,
            RequestSnapshot = snapshot,
            UpdatedAt = DateTime.UtcNow
        }, CancellationToken.None);

        if (result.IsSuccess)
        {
            await UseCasesPanel.LoadCasesAsync();
            StatusMessage = groupName == "接口草稿"
                ? "已保存为接口草稿。"
                : "快捷请求已保存。";
        }
        else
        {
            StatusMessage = result.Message;
        }

        NotifyShellState();
    }

    public void LoadSavedRequest(RequestCaseItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        ApplySnapshot(item.SourceCase.RequestSnapshot);
        StatusMessage = $"已加载保存请求：{item.Name}";
        NotifyShellState();
    }

    public void LoadHistoryRequest(RequestHistoryItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        ApplySnapshot(item.RequestSnapshot);
        if (item.ResponseSnapshot is not null)
        {
            ResponseSection.ApplyResult(ResultModel<ResponseSnapshotDto>.Success(item.ResponseSnapshot), item.RequestSnapshot);
        }

        StatusMessage = $"已加载历史请求：{item.Method} {item.Url}";
        NotifyShellState();
    }

    private async Task LoadWorkspaceAsync(string? preferredEnvironmentId = null)
    {
        UseCasesPanel.SetProjectContext(ProjectId);
        HistoryPanel.SetProjectContext(ProjectId);
        await EnvironmentPanel.LoadProjectAsync(ProjectId, preferredEnvironmentId);
        await UseCasesPanel.LoadCasesAsync();
        await HistoryPanel.LoadHistoryAsync();
        NotifyShellState();
    }

    private RequestSnapshotDto BuildCurrentSnapshot()
    {
        ConfigTab.RequestName = BuildRequestName();
        return ConfigTab.BuildRequestSnapshot(string.Empty, SelectedMethod, RequestUrl);
    }

    private string BuildRequestName()
    {
        if (!string.IsNullOrWhiteSpace(ConfigTab.RequestName))
        {
            return ConfigTab.RequestName.Trim();
        }

        var target = RequestUrl.Trim();
        if (string.IsNullOrWhiteSpace(target))
        {
            return "快捷请求";
        }

        return $"{SelectedMethod} {target}";
    }

    private void ApplySnapshot(RequestSnapshotDto snapshot)
    {
        SelectedMethod = snapshot.Method;
        RequestUrl = snapshot.Url;
        ConfigTab.ApplySnapshot(snapshot);
    }

    private void OnCaseApplied(RequestSnapshotDto snapshot)
    {
        ApplySnapshot(snapshot);
        StatusMessage = "已从保存请求恢复配置。";
        NotifyShellState();
    }

    private void OnSelectedEnvironmentChanged(ProjectEnvironmentItemViewModel? environment)
    {
        StatusMessage = environment is null
            ? "当前项目尚未配置环境。"
            : $"当前环境已切换为：{environment.Name}";
        NotifyShellState();
    }

    private void NotifyShellState()
    {
        OnPropertyChanged(nameof(TabTitle));
        OnPropertyChanged(nameof(ProjectSummary));
        OnPropertyChanged(nameof(CurrentEnvironmentLabel));
        OnPropertyChanged(nameof(CurrentBaseUrlText));
        OnPropertyChanged(nameof(HasEnvironmentContext));
        OnPropertyChanged(nameof(HasSavedRequests));
        OnPropertyChanged(nameof(HasHistory));
        OnPropertyChanged(nameof(ShowSavedRequestsEmptyState));
        OnPropertyChanged(nameof(ShowHistoryEmptyState));
        ShellStateChanged?.Invoke(this);
    }
}
