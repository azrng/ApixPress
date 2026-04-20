using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels.Base;
using ApixPress.App.Helpers;

namespace ApixPress.App.ViewModels;

public partial class ProjectTabViewModel : ViewModelBase
{
    private const string ImportedEndpointKeyPrefix = "swagger-import:";

    private static class WorkspaceSections
    {
        public const string InterfaceManagement = "interface-management";
        public const string RequestHistory = "request-history";
        public const string ProjectSettings = "project-settings";
    }

    private static class ProjectSettingsSections
    {
        public const string Overview = "overview";
        public const string ImportData = "import-data";
    }

    private readonly IRequestCaseService _requestCaseService;
    private readonly IRequestExecutionService _requestExecutionService;
    private readonly IRequestHistoryService _requestHistoryService;
    private readonly IApiWorkspaceService _apiWorkspaceService;
    private readonly RequestWorkspaceTabViewModel _fallbackWorkspaceTab;
    private CancellationTokenSource? _sendRequestCancellationTokenSource;
    private bool _initialized;
    private int _workspaceNavigationRebuildSuspendCount;
    private bool _interfaceNavigationRebuildPending;
    private bool _quickRequestNavigationRebuildPending;

    public event Action<ProjectTabViewModel>? ShellStateChanged;

    public ProjectTabViewModel(
        ProjectWorkspaceItemViewModel project,
        IRequestExecutionService requestExecutionService,
        IRequestCaseService requestCaseService,
        IRequestHistoryService requestHistoryService,
        IEnvironmentVariableService environmentVariableService,
        IApiWorkspaceService apiWorkspaceService,
        IFilePickerService filePickerService)
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
        _apiWorkspaceService = apiWorkspaceService;

        _fallbackWorkspaceTab = new RequestWorkspaceTabViewModel();
        _fallbackWorkspaceTab.ConfigureAsLanding();

        EnvironmentPanel = new EnvironmentPanelViewModel(environmentVariableService);
        UseCasesPanel = new UseCasesPanelViewModel(requestCaseService);
        HistoryPanel = new RequestHistoryPanelViewModel(requestHistoryService);
        Workspace = new ProjectWorkspaceTabsViewModel(
            () => SelectedWorkspaceSection = WorkspaceSections.InterfaceManagement,
            message => StatusMessage = message);
        Editor = new ProjectRequestEditorViewModel(
            () => ActiveWorkspaceTab,
            () => _fallbackWorkspaceTab,
            () => EnvironmentPanel.SelectedEnvironment?.BaseUrl ?? string.Empty);
        Import = new ProjectImportViewModel(
            Project.Id,
            apiWorkspaceService,
            filePickerService,
            SyncImportedInterfacesAsync,
            message => StatusMessage = message);
        QuickRequestSave = new ProjectQuickRequestSaveViewModel(
            () => ActiveWorkspaceTab,
            SaveQuickRequestAsync,
            message =>
            {
                StatusMessage = message;
                NotifyShellState();
            });

        Project.PropertyChanged += (_, _) => NotifyShellState();
        EnvironmentPanel.SelectedEnvironmentChanged += OnSelectedEnvironmentChanged;
        EnvironmentPanel.Environments.CollectionChanged += (_, _) => NotifyShellState();
        UseCasesPanel.RequestCases.CollectionChanged += OnSavedRequestsCollectionChanged;
        HistoryPanel.HistoryItems.CollectionChanged += (_, _) => NotifyShellState();
        Workspace.PropertyChanged += OnWorkspacePropertyChanged;
        Workspace.StateChanged += NotifyShellState;
        Workspace.EditorStateChanged += NotifyWorkspaceEditorState;
        Workspace.ActiveWorkspaceTabChanged += OnWorkspaceActiveWorkspaceTabChanged;
        Editor.PropertyChanged += (_, _) => NotifyShellState();
        Import.PropertyChanged += (_, _) => NotifyShellState();
        QuickRequestSave.PropertyChanged += (_, _) => NotifyShellState();

        WorkspaceNavigationItems.Add(new ProjectWorkspaceNavItemViewModel(
            WorkspaceSections.InterfaceManagement,
            "接口管理",
            "M4,5 L20,5 L20,7 L4,7 Z M4,10 L20,10 L20,12 L4,12 Z M4,15 L20,15 L20,17 L4,17 Z",
            ShowInterfaceManagementCommand));
        WorkspaceNavigationItems.Add(new ProjectWorkspaceNavItemViewModel(
            WorkspaceSections.RequestHistory,
            "请求历史",
            "M12,4 A8,8 0 1 0 20,12 A8,8 0 1 0 12,4 M12,7 L12,12 L15.5,14",
            ShowRequestHistoryCommand));
        WorkspaceNavigationItems.Add(new ProjectWorkspaceNavItemViewModel(
            WorkspaceSections.ProjectSettings,
            "项目设置",
            "M12,8.5 A3.5,3.5 0 1 0 12,15.5 A3.5,3.5 0 1 0 12,8.5 M12,3 L13.2,3.3 L13.8,5 L15.5,5.5 L17,4.7 L18.3,6 L17.5,7.5 L18,9.2 L19.7,9.8 L20,11 L18.3,12.2 L18,13.8 L19.5,15 L18.3,16.3 L16.8,15.5 L15.2,16 L14.5,17.7 L13.3,18 L12,16.7 L10.7,18 L9.5,17.7 L8.8,16 L7.2,15.5 L5.7,16.3 L4.5,15 L6,13.8 L5.7,12.2 L4,11 L4.3,9.8 L6,9.2 L6.5,7.5 L5.7,6 L7,4.7 L8.5,5.5 L10.2,5 L10.8,3.3 Z",
            ShowProjectSettingsCommand));
        SyncWorkspaceNavigationSelection();

        Workspace.EnsureLandingWorkspaceTab();
    }

    public ProjectWorkspaceItemViewModel Project { get; }
    public EnvironmentPanelViewModel EnvironmentPanel { get; }
    public UseCasesPanelViewModel UseCasesPanel { get; }
    public RequestHistoryPanelViewModel HistoryPanel { get; }
    public ProjectWorkspaceTabsViewModel Workspace { get; }
    public ProjectRequestEditorViewModel Editor { get; }
    public ProjectImportViewModel Import { get; }
    public ProjectQuickRequestSaveViewModel QuickRequestSave { get; }

    public ObservableCollection<RequestWorkspaceTabViewModel> WorkspaceTabs => Workspace.WorkspaceTabs;
    public ObservableCollection<ExplorerItemViewModel> InterfaceTreeItems { get; } = [];
    public ObservableCollection<ExplorerItemViewModel> QuickRequestTreeItems { get; } = [];
    public ObservableCollection<ProjectWorkspaceNavItemViewModel> WorkspaceNavigationItems { get; } = [];
    public IReadOnlyList<ExplorerItemViewModel> InterfaceCatalogItems => InterfaceTreeItems.FirstOrDefault()?.Children ?? [];
    public ReadOnlyObservableCollection<RequestWorkspaceTabViewModel> VisibleWorkspaceTabs => Workspace.VisibleWorkspaceTabs;
    public ObservableCollection<RequestCaseItemViewModel> SavedRequests => UseCasesPanel.RequestCases;
    public ObservableCollection<RequestHistoryItemViewModel> RequestHistory => HistoryPanel.HistoryItems;
    public RequestWorkspaceTabViewModel? ActiveWorkspaceTab
    {
        get => Workspace.ActiveWorkspaceTab;
        set => Workspace.ActiveWorkspaceTab = value;
    }

    public bool IsWorkspaceTabMenuOpen
    {
        get => Workspace.IsWorkspaceTabMenuOpen;
        set => Workspace.IsWorkspaceTabMenuOpen = value;
    }

    public RequestConfigTabViewModel ConfigTab => ActiveWorkspaceTab?.ConfigTab ?? _fallbackWorkspaceTab.ConfigTab;
    public ResponseSectionViewModel ResponseSection => ActiveWorkspaceTab?.ResponseSection ?? _fallbackWorkspaceTab.ResponseSection;

    public string ProjectId => Project.Id;
    public string TabTitle => Project.Name;
    public string ProjectSummary => string.IsNullOrWhiteSpace(Project.Description) ? "暂无项目备注" : Project.Description;
    public string CurrentEnvironmentLabel => EnvironmentPanel.SelectedEnvironment?.Name ?? "未选择环境";
    public string CurrentBaseUrlText => string.IsNullOrWhiteSpace(EnvironmentPanel.SelectedEnvironment?.BaseUrl)
        ? "当前环境暂未配置 BaseUrl"
        : EnvironmentPanel.SelectedEnvironment.BaseUrl;
    public bool HasEnvironmentContext => ActiveWorkspaceTab is not null && !ActiveWorkspaceTab.IsLandingTab;
    public bool HasSavedRequests => SavedRequests.Count > 0;
    public bool HasHistory => RequestHistory.Count > 0;
    public bool HasQuickRequestEntries => SavedRequests.Any(item => string.Equals(item.SourceCase.EntryType, ProjectTabRequestEntryTypes.QuickRequest, StringComparison.OrdinalIgnoreCase));
    public bool HasInterfaceEntries => SavedRequests.Any(item => string.Equals(item.SourceCase.EntryType, ProjectTabRequestEntryTypes.HttpInterface, StringComparison.OrdinalIgnoreCase));
    public bool ShowInterfaceEntriesEmptyState => !HasInterfaceEntries;
    public bool ShowQuickRequestEntriesEmptyState => !HasQuickRequestEntries;
    public bool ShowSavedRequestsEmptyState => !HasQuickRequestEntries && !HasInterfaceEntries;
    public bool ShowHistoryEmptyState => !HasHistory;
    public bool IsInterfaceManagementSection => SelectedWorkspaceSection == WorkspaceSections.InterfaceManagement;
    public bool IsRequestHistorySection => SelectedWorkspaceSection == WorkspaceSections.RequestHistory;
    public bool IsProjectSettingsSection => SelectedWorkspaceSection == WorkspaceSections.ProjectSettings;
    public bool IsQuickRequestEditor => ActiveWorkspaceTab?.IsQuickRequestTab ?? false;
    public bool IsHttpInterfaceEditor => ActiveWorkspaceTab?.IsHttpInterfaceTab ?? false;
    public bool IsRequestEditorOpen => ActiveWorkspaceTab is not null && !ActiveWorkspaceTab.IsLandingTab;
    public bool ShowInterfaceManagementLanding => IsInterfaceManagementSection && (ActiveWorkspaceTab?.IsLandingTab ?? true);
    public bool ShowRequestEditorWorkspace => IsInterfaceManagementSection && IsRequestEditorOpen;
    public string SavedRequestCountText => SavedRequests.Count(item =>
        string.Equals(item.SourceCase.EntryType, ProjectTabRequestEntryTypes.QuickRequest, StringComparison.OrdinalIgnoreCase)
        || string.Equals(item.SourceCase.EntryType, ProjectTabRequestEntryTypes.HttpInterface, StringComparison.OrdinalIgnoreCase)).ToString();
    public string HistoryCountText => RequestHistory.Count.ToString();
    public string EnvironmentCountText => EnvironmentPanel.Environments.Count.ToString();
    public string InterfaceSectionHint => HasInterfaceEntries ? "默认模块 / 接口" : "默认模块下还没有保存的 HTTP 接口";
    public string QuickRequestSectionHint => HasQuickRequestEntries ? "保存到左侧快捷请求目录" : "左侧快捷请求目录还是空的";
    [ObservableProperty]
    private bool isActive;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = "项目工作区已就绪。";

    [ObservableProperty]
    private string selectedWorkspaceSection = WorkspaceSections.InterfaceManagement;

    [ObservableProperty]
    private ProjectWorkspaceNavItemViewModel? selectedWorkspaceNavigationItem;

    [ObservableProperty]
    private string selectedProjectSettingsSection = ProjectSettingsSections.Overview;

    [ObservableProperty]
    private bool isInterfaceCatalogExpanded = true;

    [ObservableProperty]
    private bool isDataModelCatalogExpanded;

    [ObservableProperty]
    private bool isComponentLibraryCatalogExpanded;

    [ObservableProperty]
    private bool isQuickRequestCatalogExpanded = true;

    [ObservableProperty]
    private bool isWorkspaceDeleteConfirmDialogOpen;

    [ObservableProperty]
    private bool responseValidationEnabled = true;

    [ObservableProperty]
    private ExplorerItemViewModel? pendingDeleteWorkspaceItem;

    private void OnSavedRequestsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var (rebuildInterfaceNavigation, rebuildQuickRequestNavigation) = ResolveWorkspaceNavigationRebuildScope(e);
        RequestWorkspaceNavigationRebuild(rebuildInterfaceNavigation, rebuildQuickRequestNavigation);
        NotifyShellState();
    }

    private void OnWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProjectWorkspaceTabsViewModel.ActiveWorkspaceTab))
        {
            OnPropertyChanged(nameof(ActiveWorkspaceTab));
        }
        else if (e.PropertyName == nameof(ProjectWorkspaceTabsViewModel.IsWorkspaceTabMenuOpen))
        {
            OnPropertyChanged(nameof(IsWorkspaceTabMenuOpen));
        }
    }
}
