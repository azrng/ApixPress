using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels.Base;
using Azrng.Core.Results;

namespace ApixPress.App.ViewModels;

public partial class ProjectTabViewModel : ViewModelBase
{
    private static class WorkspaceSections
    {
        public const string InterfaceManagement = "interface-management";
        public const string RequestHistory = "request-history";
        public const string ProjectSettings = "project-settings";
    }

    private static class EditorModes
    {
        public const string None = "none";
        public const string QuickRequest = "quick-request";
        public const string HttpInterface = "http-interface";
    }

    private static class RequestEntryTypes
    {
        public const string QuickRequest = "quick-request";
        public const string HttpInterface = "http-interface";
        public const string HttpCase = "http-case";
    }

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
        EnvironmentPanel.Environments.CollectionChanged += (_, _) => NotifyShellState();
        UseCasesPanel.RequestCases.CollectionChanged += (_, _) =>
        {
            RebuildWorkspaceNavigation();
            NotifyShellState();
        };
        HistoryPanel.HistoryItems.CollectionChanged += (_, _) => NotifyShellState();
    }

    public ProjectWorkspaceItemViewModel Project { get; }
    public RequestConfigTabViewModel ConfigTab { get; }
    public ResponseSectionViewModel ResponseSection { get; }
    public EnvironmentPanelViewModel EnvironmentPanel { get; }
    public UseCasesPanelViewModel UseCasesPanel { get; }
    public RequestHistoryPanelViewModel HistoryPanel { get; }

    public ObservableCollection<ExplorerItemViewModel> InterfaceTreeItems { get; } = [];
    public ObservableCollection<ExplorerItemViewModel> QuickRequestTreeItems { get; } = [];
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
    public bool HasQuickRequestEntries => SavedRequests.Any(item => string.Equals(item.SourceCase.EntryType, RequestEntryTypes.QuickRequest, StringComparison.OrdinalIgnoreCase));
    public bool HasInterfaceEntries => SavedRequests.Any(item => string.Equals(item.SourceCase.EntryType, RequestEntryTypes.HttpInterface, StringComparison.OrdinalIgnoreCase));
    public bool ShowInterfaceEntriesEmptyState => !HasInterfaceEntries;
    public bool ShowSavedRequestsEmptyState => !HasQuickRequestEntries && !HasInterfaceEntries;
    public bool ShowHistoryEmptyState => !HasHistory;
    public bool IsInterfaceManagementSection => SelectedWorkspaceSection == WorkspaceSections.InterfaceManagement;
    public bool IsRequestHistorySection => SelectedWorkspaceSection == WorkspaceSections.RequestHistory;
    public bool IsProjectSettingsSection => SelectedWorkspaceSection == WorkspaceSections.ProjectSettings;
    public bool IsQuickRequestEditor => SelectedEditorMode == EditorModes.QuickRequest;
    public bool IsHttpInterfaceEditor => SelectedEditorMode == EditorModes.HttpInterface;
    public bool IsRequestEditorOpen => SelectedEditorMode != EditorModes.None;
    public bool ShowInterfaceManagementLanding => IsInterfaceManagementSection && !IsRequestEditorOpen;
    public bool ShowRequestEditorWorkspace => IsInterfaceManagementSection && IsRequestEditorOpen;
    public string SavedRequestCountText => SavedRequests.Count(item =>
        string.Equals(item.SourceCase.EntryType, RequestEntryTypes.QuickRequest, StringComparison.OrdinalIgnoreCase)
        || string.Equals(item.SourceCase.EntryType, RequestEntryTypes.HttpInterface, StringComparison.OrdinalIgnoreCase)).ToString();
    public string HistoryCountText => RequestHistory.Count.ToString();
    public string EnvironmentCountText => EnvironmentPanel.Environments.Count.ToString();
    public string ProjectSettingsDescription => string.IsNullOrWhiteSpace(Project.Description)
        ? "当前项目还没有补充备注，可在这里继续维护环境与工作区说明。"
        : Project.Description;
    public string InterfaceSectionHint => HasInterfaceEntries
        ? "默认模块 / 接口"
        : "默认模块下还没有保存的 HTTP 接口";
    public string QuickRequestSectionHint => HasQuickRequestEntries
        ? "保存到左侧快捷请求目录"
        : "左侧快捷请求目录还是空的";
    public string CurrentEditorTitle => IsHttpInterfaceEditor ? "HTTP 接口" : "快捷请求";
    public string CurrentEditorDescription => IsHttpInterfaceEditor
        ? "HTTP 接口会自动使用当前环境的 BaseUrl，请在右侧输入相对路径。"
        : "快捷请求不固定 BaseUrl，可直接输入完整地址或自由组合变量。";
    public string CurrentEditorPrimaryActionText => IsHttpInterfaceEditor ? "保存接口" : "保存快捷请求";
    public string CurrentEditorUrlWatermark => IsHttpInterfaceEditor ? "输入接口相对路径，例如 /users/{id}" : "输入完整地址或相对路径";
    public bool ShowEditorBaseUrlPrefix => IsHttpInterfaceEditor;
    public string CurrentEditorBaseUrlPrefix => IsHttpInterfaceEditor
        ? EnvironmentPanel.SelectedEnvironment?.BaseUrl ?? string.Empty
        : string.Empty;
    public bool ShowSaveHttpCaseAction => IsHttpInterfaceEditor;
    public string CurrentEditorBaseUrlCaption => string.IsNullOrWhiteSpace(CurrentEditorBaseUrlPrefix)
        ? "当前环境未配置 BaseUrl"
        : CurrentEditorBaseUrlPrefix;

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

    [ObservableProperty]
    private string selectedWorkspaceSection = WorkspaceSections.InterfaceManagement;

    [ObservableProperty]
    private string selectedEditorMode = EditorModes.None;

    [ObservableProperty]
    private string currentInterfaceFolderPath = "用户";

    [ObservableProperty]
    private string currentHttpCaseName = "成功";

    [ObservableProperty]
    private string currentEditingQuickRequestId = string.Empty;

    [ObservableProperty]
    private string currentEditingInterfaceId = string.Empty;

    [ObservableProperty]
    private string currentEditingCaseId = string.Empty;

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
        SelectedWorkspaceSection = WorkspaceSections.InterfaceManagement;
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
        StatusMessage = result.IsSuccess
            ? (IsHttpInterfaceEditor ? "HTTP 接口请求发送完成。" : "快捷请求发送完成。")
            : result.Message;
        NotifyShellState();
    }

    public async Task SaveCurrentEditorAsync()
    {
        SelectedWorkspaceSection = WorkspaceSections.InterfaceManagement;
        if (IsHttpInterfaceEditor)
        {
            await SaveHttpInterfaceAsync();
            return;
        }

        await SaveQuickRequestAsync();
    }

    public async Task SaveHistoryAsQuickRequestAsync(RequestHistoryItemViewModel item)
    {
        var snapshot = item.RequestSnapshot;
        var result = await _requestCaseService.SaveAsync(new RequestCaseDto
        {
            ProjectId = ProjectId,
            EntryType = RequestEntryTypes.QuickRequest,
            Name = $"{snapshot.Method} {snapshot.Url}",
            GroupName = "快捷请求",
            Description = $"从 {item.Timestamp.ToLocalTime():yyyy-MM-dd HH:mm} 的历史记录创建",
            RequestSnapshot = snapshot,
            UpdatedAt = DateTime.UtcNow
        }, CancellationToken.None);

        if (result.IsSuccess)
        {
            await ReloadSavedRequestsAsync();
            StatusMessage = "已从历史记录生成快捷请求。";
        }
        else
        {
            StatusMessage = result.Message;
        }

        NotifyShellState();
    }

    [RelayCommand]
    public async Task SaveHttpCaseAsync()
    {
        if (!IsHttpInterfaceEditor)
        {
            return;
        }

        SelectedWorkspaceSection = WorkspaceSections.InterfaceManagement;
        var interfaceId = await EnsureHttpInterfaceSavedAsync(reloadAfterSave: false);
        if (string.IsNullOrWhiteSpace(interfaceId))
        {
            return;
        }

        var snapshot = BuildCurrentSnapshot();
        var result = await _requestCaseService.SaveAsync(new RequestCaseDto
        {
            Id = CurrentEditingCaseId,
            ProjectId = ProjectId,
            EntryType = RequestEntryTypes.HttpCase,
            Name = BuildHttpCaseName(),
            GroupName = "用例",
            FolderPath = NormalizeFolderPath(CurrentInterfaceFolderPath),
            ParentId = interfaceId,
            Description = $"{BuildHttpInterfaceName()} 的请求用例",
            RequestSnapshot = snapshot,
            UpdatedAt = DateTime.UtcNow
        }, CancellationToken.None);

        if (result.IsSuccess && result.Data is not null)
        {
            CurrentEditingCaseId = result.Data.Id;
            await ReloadSavedRequestsAsync();
            StatusMessage = "HTTP 接口用例已保存。";
        }
        else
        {
            StatusMessage = result.Message;
        }

        NotifyShellState();
    }

    public void LoadWorkspaceItem(ExplorerItemViewModel? item)
    {
        if (item?.SourceCase is null)
        {
            return;
        }

        var source = item.SourceCase;
        SelectedWorkspaceSection = WorkspaceSections.InterfaceManagement;
        ApplySnapshot(source.RequestSnapshot);

        switch (source.EntryType)
        {
            case RequestEntryTypes.HttpInterface:
                SelectedEditorMode = EditorModes.HttpInterface;
                CurrentEditingInterfaceId = source.Id;
                CurrentEditingCaseId = string.Empty;
                CurrentEditingQuickRequestId = string.Empty;
                CurrentInterfaceFolderPath = source.FolderPath;
                CurrentHttpCaseName = ResolveLatestCaseName(source.Id);
                StatusMessage = $"已加载 HTTP 接口：{source.Name}";
                break;
            case RequestEntryTypes.HttpCase:
            {
                SelectedEditorMode = EditorModes.HttpInterface;
                CurrentEditingQuickRequestId = string.Empty;
                CurrentEditingCaseId = source.Id;
                CurrentHttpCaseName = source.Name;
                var parent = FindRequestById(source.ParentId);
                CurrentEditingInterfaceId = parent?.Id ?? source.ParentId;
                CurrentInterfaceFolderPath = parent?.FolderPath ?? source.FolderPath;
                if (parent is not null && string.IsNullOrWhiteSpace(ConfigTab.RequestName))
                {
                    ConfigTab.RequestName = parent.Name;
                }

                StatusMessage = $"已加载接口用例：{source.Name}";
                break;
            }
            default:
                SelectedEditorMode = EditorModes.QuickRequest;
                CurrentEditingQuickRequestId = source.Id;
                CurrentEditingInterfaceId = string.Empty;
                CurrentEditingCaseId = string.Empty;
                CurrentInterfaceFolderPath = "用户";
                CurrentHttpCaseName = "成功";
                StatusMessage = $"已加载快捷请求：{source.Name}";
                break;
        }

        NotifyShellState();
    }

    public void LoadHistoryRequest(RequestHistoryItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        SelectedWorkspaceSection = WorkspaceSections.RequestHistory;
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
        await ReloadSavedRequestsAsync();
        await HistoryPanel.LoadHistoryAsync();
        NotifyShellState();
    }

    [RelayCommand]
    private void ShowInterfaceManagement()
    {
        SelectedWorkspaceSection = WorkspaceSections.InterfaceManagement;
        SelectedEditorMode = EditorModes.None;
        StatusMessage = "接口管理已就绪，可新建 HTTP 接口或快捷请求。";
        NotifyShellState();
    }

    [RelayCommand]
    private void ShowRequestHistory()
    {
        SelectedWorkspaceSection = WorkspaceSections.RequestHistory;
        StatusMessage = HasHistory
            ? "这里展示当前项目的请求历史。"
            : "当前项目还没有请求历史。";
        NotifyShellState();
    }

    [RelayCommand]
    private void ShowProjectSettings()
    {
        SelectedWorkspaceSection = WorkspaceSections.ProjectSettings;
        StatusMessage = "这里可以查看项目说明并进入环境设置。";
        NotifyShellState();
    }

    [RelayCommand]
    private void OpenQuickRequestWorkspace()
    {
        SelectedWorkspaceSection = WorkspaceSections.InterfaceManagement;
        SelectedEditorMode = EditorModes.QuickRequest;
        ResetQuickRequestDraft();
        StatusMessage = "快捷请求编辑器已打开。";
        NotifyShellState();
    }

    [RelayCommand]
    private void CreateNewQuickRequestDraft()
    {
        SelectedWorkspaceSection = WorkspaceSections.InterfaceManagement;
        SelectedEditorMode = EditorModes.QuickRequest;
        ResetQuickRequestDraft();
        StatusMessage = "已新建快捷请求草稿。";
        NotifyShellState();
    }

    [RelayCommand]
    private void OpenHttpInterfaceWorkspace()
    {
        SelectedWorkspaceSection = WorkspaceSections.InterfaceManagement;
        SelectedEditorMode = EditorModes.HttpInterface;
        ResetHttpInterfaceDraft();
        StatusMessage = "HTTP 接口编辑器已打开。";
        NotifyShellState();
    }

    [RelayCommand]
    private void ReturnToInterfaceHome()
    {
        SelectedWorkspaceSection = WorkspaceSections.InterfaceManagement;
        SelectedEditorMode = EditorModes.None;
        StatusMessage = "已返回接口管理首页。";
        NotifyShellState();
    }

    private RequestSnapshotDto BuildCurrentSnapshot()
    {
        ConfigTab.RequestName = IsHttpInterfaceEditor ? BuildHttpInterfaceName() : BuildQuickRequestName();
        return ConfigTab.BuildRequestSnapshot(string.Empty, SelectedMethod, RequestUrl);
    }

    private void ResetQuickRequestDraft()
    {
        CurrentEditingQuickRequestId = string.Empty;
        CurrentEditingInterfaceId = string.Empty;
        CurrentEditingCaseId = string.Empty;
        CurrentInterfaceFolderPath = "用户";
        CurrentHttpCaseName = "成功";
        SelectedMethod = "GET";
        RequestUrl = string.Empty;
        ConfigTab.Reset();
        ResponseSection.Reset();
    }

    private void ResetHttpInterfaceDraft()
    {
        CurrentEditingQuickRequestId = string.Empty;
        CurrentEditingInterfaceId = string.Empty;
        CurrentEditingCaseId = string.Empty;
        CurrentInterfaceFolderPath = "用户";
        CurrentHttpCaseName = "成功";
        SelectedMethod = "GET";
        RequestUrl = string.Empty;
        ConfigTab.Reset();
        ResponseSection.Reset();
    }

    private string BuildQuickRequestName()
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

    private string BuildHttpInterfaceName()
    {
        if (!string.IsNullOrWhiteSpace(ConfigTab.RequestName))
        {
            return ConfigTab.RequestName.Trim();
        }

        var target = RequestUrl.Trim();
        return string.IsNullOrWhiteSpace(target)
            ? "新建 HTTP 接口"
            : $"{SelectedMethod} {target}";
    }

    private string BuildHttpCaseName()
    {
        return string.IsNullOrWhiteSpace(CurrentHttpCaseName)
            ? "成功"
            : CurrentHttpCaseName.Trim();
    }

    private void ApplySnapshot(RequestSnapshotDto snapshot)
    {
        SelectedMethod = snapshot.Method;
        RequestUrl = snapshot.Url;
        ConfigTab.ApplySnapshot(snapshot);
    }

    private async Task SaveQuickRequestAsync()
    {
        var snapshot = BuildCurrentSnapshot();
        var result = await _requestCaseService.SaveAsync(new RequestCaseDto
        {
            Id = CurrentEditingQuickRequestId,
            ProjectId = ProjectId,
            EntryType = RequestEntryTypes.QuickRequest,
            Name = BuildQuickRequestName(),
            GroupName = "快捷请求",
            Description = ConfigTab.RequestDescription,
            RequestSnapshot = snapshot,
            UpdatedAt = DateTime.UtcNow
        }, CancellationToken.None);

        if (result.IsSuccess && result.Data is not null)
        {
            CurrentEditingQuickRequestId = result.Data.Id;
            await ReloadSavedRequestsAsync();
            StatusMessage = "快捷请求已保存到左侧目录。";
        }
        else
        {
            StatusMessage = result.Message;
        }

        NotifyShellState();
    }

    private async Task SaveHttpInterfaceAsync()
    {
        var interfaceId = await EnsureHttpInterfaceSavedAsync(reloadAfterSave: true);
        if (!string.IsNullOrWhiteSpace(interfaceId))
        {
            StatusMessage = "HTTP 接口已保存到默认模块。";
            NotifyShellState();
        }
    }

    private async Task<string?> EnsureHttpInterfaceSavedAsync(bool reloadAfterSave)
    {
        var snapshot = BuildCurrentSnapshot();
        var result = await _requestCaseService.SaveAsync(new RequestCaseDto
        {
            Id = CurrentEditingInterfaceId,
            ProjectId = ProjectId,
            EntryType = RequestEntryTypes.HttpInterface,
            Name = BuildHttpInterfaceName(),
            GroupName = "接口",
            FolderPath = NormalizeFolderPath(CurrentInterfaceFolderPath),
            Description = ConfigTab.RequestDescription,
            RequestSnapshot = snapshot,
            UpdatedAt = DateTime.UtcNow
        }, CancellationToken.None);

        if (!result.IsSuccess || result.Data is null)
        {
            StatusMessage = result.Message;
            NotifyShellState();
            return null;
        }

        CurrentEditingInterfaceId = result.Data.Id;
        if (reloadAfterSave)
        {
            await ReloadSavedRequestsAsync();
        }

        return result.Data.Id;
    }

    private async Task ReloadSavedRequestsAsync()
    {
        await UseCasesPanel.LoadCasesAsync();
        RebuildWorkspaceNavigation();
    }

    private void RebuildWorkspaceNavigation()
    {
        InterfaceTreeItems.Clear();
        QuickRequestTreeItems.Clear();

        var httpInterfaces = SavedRequests
            .Where(item => string.Equals(item.SourceCase.EntryType, RequestEntryTypes.HttpInterface, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.SourceCase.FolderPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var httpCases = SavedRequests
            .Where(item => string.Equals(item.SourceCase.EntryType, RequestEntryTypes.HttpCase, StringComparison.OrdinalIgnoreCase))
            .GroupBy(item => item.SourceCase.ParentId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.UpdatedAt).ToList(), StringComparer.OrdinalIgnoreCase);
        var quickRequests = SavedRequests
            .Where(item => string.Equals(item.SourceCase.EntryType, RequestEntryTypes.QuickRequest, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.UpdatedAt)
            .ToList();

        var moduleNode = new ExplorerItemViewModel
        {
            Title = "默认模块",
            Subtitle = "接口目录",
            IsGroup = true,
            NodeType = "module"
        };
        var interfaceRoot = new ExplorerItemViewModel
        {
            Title = "接口",
            Subtitle = httpInterfaces.Count == 0 ? "暂无已保存接口" : $"共 {httpInterfaces.Count} 个接口",
            IsGroup = true,
            NodeType = "interface-root"
        };
        moduleNode.Children.Add(interfaceRoot);
        InterfaceTreeItems.Add(moduleNode);

        var folderNodes = new Dictionary<string, ExplorerItemViewModel>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in httpInterfaces)
        {
            var parentNode = interfaceRoot;
            var folderPath = NormalizeFolderPath(item.SourceCase.FolderPath);
            if (!string.IsNullOrWhiteSpace(folderPath))
            {
                var segments = folderPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var currentPath = string.Empty;
                foreach (var segment in segments)
                {
                    currentPath = string.IsNullOrWhiteSpace(currentPath) ? segment : $"{currentPath}/{segment}";
                    if (!folderNodes.TryGetValue(currentPath, out var folderNode))
                    {
                        folderNode = new ExplorerItemViewModel
                        {
                            Title = segment,
                            Subtitle = "文件夹",
                            IsGroup = true,
                            NodeType = "folder"
                        };
                        folderNodes[currentPath] = folderNode;
                        parentNode.Children.Add(folderNode);
                    }

                    parentNode = folderNode;
                }
            }

            var interfaceNode = new ExplorerItemViewModel
            {
                Title = item.Name,
                Subtitle = $"{item.SourceCase.RequestSnapshot.Method} {item.SourceCase.RequestSnapshot.Url}",
                NodeType = RequestEntryTypes.HttpInterface,
                CanLoad = true,
                SourceCase = item.SourceCase
            };
            parentNode.Children.Add(interfaceNode);

            if (httpCases.TryGetValue(item.Id, out var cases))
            {
                foreach (var caseItem in cases)
                {
                    interfaceNode.Children.Add(new ExplorerItemViewModel
                    {
                        Title = caseItem.Name,
                        Subtitle = $"用例 · {caseItem.UpdatedAtText}",
                        NodeType = RequestEntryTypes.HttpCase,
                        CanLoad = true,
                        SourceCase = caseItem.SourceCase
                    });
                }
            }
        }

        var quickRoot = new ExplorerItemViewModel
        {
            Title = "快捷请求",
            Subtitle = quickRequests.Count == 0 ? "暂无快捷请求" : $"共 {quickRequests.Count} 条请求",
            IsGroup = true,
            NodeType = "quick-root"
        };
        foreach (var item in quickRequests)
        {
            quickRoot.Children.Add(new ExplorerItemViewModel
            {
                Title = item.Name,
                Subtitle = $"{item.SourceCase.RequestSnapshot.Method} {item.SourceCase.RequestSnapshot.Url}",
                NodeType = RequestEntryTypes.QuickRequest,
                CanLoad = true,
                SourceCase = item.SourceCase
            });
        }

        QuickRequestTreeItems.Add(quickRoot);
    }

    private RequestCaseDto? FindRequestById(string id)
    {
        return SavedRequests
            .Select(item => item.SourceCase)
            .FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    private string ResolveLatestCaseName(string interfaceId)
    {
        return SavedRequests
            .Where(item => string.Equals(item.SourceCase.EntryType, RequestEntryTypes.HttpCase, StringComparison.OrdinalIgnoreCase))
            .Where(item => string.Equals(item.SourceCase.ParentId, interfaceId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.UpdatedAt)
            .Select(item => item.Name)
            .FirstOrDefault()
            ?? "成功";
    }

    private static string NormalizeFolderPath(string folderPath)
    {
        var normalized = folderPath.Replace('\\', '/').Trim('/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        return string.Join('/',
            normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private void OnSelectedEnvironmentChanged(ProjectEnvironmentItemViewModel? environment)
    {
        StatusMessage = environment is null
            ? "当前项目尚未配置环境。"
            : $"当前环境已切换为：{environment.Name}";
        NotifyShellState();
    }

    partial void OnSelectedWorkspaceSectionChanged(string value)
    {
        OnPropertyChanged(nameof(IsInterfaceManagementSection));
        OnPropertyChanged(nameof(IsRequestHistorySection));
        OnPropertyChanged(nameof(IsProjectSettingsSection));
        OnPropertyChanged(nameof(IsQuickRequestEditor));
        OnPropertyChanged(nameof(IsHttpInterfaceEditor));
        OnPropertyChanged(nameof(IsRequestEditorOpen));
        OnPropertyChanged(nameof(ShowInterfaceManagementLanding));
        OnPropertyChanged(nameof(ShowRequestEditorWorkspace));
    }

    partial void OnSelectedEditorModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsQuickRequestEditor));
        OnPropertyChanged(nameof(IsHttpInterfaceEditor));
        OnPropertyChanged(nameof(IsRequestEditorOpen));
        OnPropertyChanged(nameof(ShowInterfaceManagementLanding));
        OnPropertyChanged(nameof(ShowRequestEditorWorkspace));
        OnPropertyChanged(nameof(CurrentEditorTitle));
        OnPropertyChanged(nameof(CurrentEditorDescription));
        OnPropertyChanged(nameof(CurrentEditorPrimaryActionText));
        OnPropertyChanged(nameof(CurrentEditorUrlWatermark));
        OnPropertyChanged(nameof(ShowEditorBaseUrlPrefix));
        OnPropertyChanged(nameof(CurrentEditorBaseUrlPrefix));
        OnPropertyChanged(nameof(ShowSaveHttpCaseAction));
        OnPropertyChanged(nameof(CurrentEditorBaseUrlCaption));
    }

    private void NotifyShellState()
    {
        OnPropertyChanged(nameof(TabTitle));
        OnPropertyChanged(nameof(ProjectSummary));
        OnPropertyChanged(nameof(CurrentEnvironmentLabel));
        OnPropertyChanged(nameof(CurrentBaseUrlText));
        OnPropertyChanged(nameof(HasEnvironmentContext));
        OnPropertyChanged(nameof(HasSavedRequests));
        OnPropertyChanged(nameof(HasQuickRequestEntries));
        OnPropertyChanged(nameof(HasInterfaceEntries));
        OnPropertyChanged(nameof(ShowInterfaceEntriesEmptyState));
        OnPropertyChanged(nameof(HasHistory));
        OnPropertyChanged(nameof(ShowSavedRequestsEmptyState));
        OnPropertyChanged(nameof(ShowHistoryEmptyState));
        OnPropertyChanged(nameof(IsInterfaceManagementSection));
        OnPropertyChanged(nameof(IsRequestHistorySection));
        OnPropertyChanged(nameof(IsProjectSettingsSection));
        OnPropertyChanged(nameof(IsQuickRequestEditor));
        OnPropertyChanged(nameof(IsHttpInterfaceEditor));
        OnPropertyChanged(nameof(IsRequestEditorOpen));
        OnPropertyChanged(nameof(ShowInterfaceManagementLanding));
        OnPropertyChanged(nameof(ShowRequestEditorWorkspace));
        OnPropertyChanged(nameof(SavedRequestCountText));
        OnPropertyChanged(nameof(HistoryCountText));
        OnPropertyChanged(nameof(EnvironmentCountText));
        OnPropertyChanged(nameof(ProjectSettingsDescription));
        OnPropertyChanged(nameof(InterfaceSectionHint));
        OnPropertyChanged(nameof(QuickRequestSectionHint));
        OnPropertyChanged(nameof(CurrentEditorTitle));
        OnPropertyChanged(nameof(CurrentEditorDescription));
        OnPropertyChanged(nameof(CurrentEditorPrimaryActionText));
        OnPropertyChanged(nameof(CurrentEditorUrlWatermark));
        OnPropertyChanged(nameof(ShowEditorBaseUrlPrefix));
        OnPropertyChanged(nameof(CurrentEditorBaseUrlPrefix));
        OnPropertyChanged(nameof(ShowSaveHttpCaseAction));
        OnPropertyChanged(nameof(CurrentEditorBaseUrlCaption));
        ShellStateChanged?.Invoke(this);
    }
}
