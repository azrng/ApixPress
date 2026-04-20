using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private static class SettingsSections
    {
        public const string General = "general";
        public const string About = "about";
    }

    private readonly IAppShellSettingsService _appShellSettingsService;
    private readonly IApplicationUpdateService _applicationUpdateService;
    private readonly IEnvironmentVariableService _environmentVariableService;
    private readonly IRequestCaseService _requestCaseService;
    private readonly IRequestExecutionService _requestExecutionService;
    private readonly IRequestHistoryService _requestHistoryService;
    private readonly IApiWorkspaceService _apiWorkspaceService;
    private readonly IFilePickerService _filePickerService;
    private readonly RequestConfigTabViewModel _fallbackConfigTab;
    private readonly ResponseSectionViewModel _fallbackResponseSection;
    private readonly EnvironmentPanelViewModel _fallbackEnvironmentPanel;
    private readonly UseCasesPanelViewModel _fallbackUseCasesPanel;
    private readonly RequestHistoryPanelViewModel _fallbackHistoryPanel;
    private readonly SemaphoreSlim _shellSettingsSaveSemaphore = new(1, 1);
    private CancellationTokenSource? _shellSettingsSaveCancellationTokenSource;
    private bool _initialized;
    private bool _isApplyingShellSettings;

    public MainWindowViewModel(
        IRequestExecutionService requestExecutionService,
        IRequestCaseService requestCaseService,
        IRequestHistoryService requestHistoryService,
        IEnvironmentVariableService environmentVariableService,
        IProjectWorkspaceService projectWorkspaceService,
        IAppShellSettingsService appShellSettingsService,
        IApplicationUpdateService applicationUpdateService,
        IApiWorkspaceService apiWorkspaceService,
        IFilePickerService filePickerService)
    {
        _requestExecutionService = requestExecutionService;
        _requestCaseService = requestCaseService;
        _requestHistoryService = requestHistoryService;
        _environmentVariableService = environmentVariableService;
        _appShellSettingsService = appShellSettingsService;
        _applicationUpdateService = applicationUpdateService;
        _apiWorkspaceService = apiWorkspaceService;
        _filePickerService = filePickerService;

        _fallbackConfigTab = new RequestConfigTabViewModel(null);
        _fallbackResponseSection = new ResponseSectionViewModel();
        _fallbackEnvironmentPanel = new EnvironmentPanelViewModel(environmentVariableService);
        _fallbackUseCasesPanel = new UseCasesPanelViewModel(requestCaseService);
        _fallbackHistoryPanel = new RequestHistoryPanelViewModel(requestHistoryService);

        ProjectPanel = new ProjectPanelViewModel(projectWorkspaceService);
        Notifications = CreateNotifications();

        CurrentAppVersion = ResolveCurrentAppVersion();
        UpdateChannelName = _applicationUpdateService.ChannelName;
        AboutUpdateStatus = _applicationUpdateService.IsConfigured
            ? $"当前通过 {UpdateChannelName} 检查更新。"
            : "尚未配置更新源，请先补充 appsettings.json 中的 Update 节点。";

        ProjectTabs.CollectionChanged += OnProjectTabsCollectionChanged;
        ProjectPanel.ProjectCreated += OnProjectCreated;
        ProjectPanel.PropertyChanged += OnProjectPanelPropertyChanged;
        ProjectPanel.Projects.CollectionChanged += OnProjectsCollectionChanged;

        foreach (var item in Notifications)
        {
            item.PropertyChanged += OnNotificationPropertyChanged;
        }
    }

    public ProjectPanelViewModel ProjectPanel { get; }
    public ObservableCollection<ProjectTabViewModel> ProjectTabs { get; } = [];
    public ObservableCollection<NotificationItemViewModel> Notifications { get; }

    public RequestConfigTabViewModel ConfigTab => ActiveProjectTab?.ConfigTab ?? _fallbackConfigTab;
    public ResponseSectionViewModel ResponseSection => ActiveProjectTab?.ResponseSection ?? _fallbackResponseSection;
    public EnvironmentPanelViewModel EnvironmentPanel => ActiveProjectTab?.EnvironmentPanel ?? _fallbackEnvironmentPanel;
    public UseCasesPanelViewModel UseCasesPanel => ActiveProjectTab?.UseCasesPanel ?? _fallbackUseCasesPanel;
    public RequestHistoryPanelViewModel HistoryPanel => ActiveProjectTab?.HistoryPanel ?? _fallbackHistoryPanel;
    public ObservableCollection<RequestHistoryItemViewModel> RequestHistory => HistoryPanel.HistoryItems;

    public bool IsHomeTabActive => ActiveProjectTab is null;
    public bool HasActiveProjectTab => ActiveProjectTab is not null;
    public bool HasProjectTabs => ProjectTabs.Count > 0;
    public bool IsProjectBrowserMode => IsHomeTabActive;
    public bool IsWorkspaceMode => HasActiveProjectTab;
    public bool ShowProjectListEmptyState => IsHomeTabActive && !ProjectPanel.HasAnyProjects;
    public bool ShowProjectSearchEmptyState => IsHomeTabActive && ProjectPanel.HasAnyProjects && !ProjectPanel.HasProjects;
    public bool HasEnvironmentContext => ActiveProjectTab?.HasEnvironmentContext ?? false;
    public bool ShowQuickRequestSaveDialog => ActiveProjectTab?.QuickRequestSave.IsDialogOpen ?? false;
    public bool ShowProjectImportDialog => ActiveProjectTab?.Import.IsDialogOpen ?? false;
    public bool ShowProjectImportOverwriteConfirmDialog => ActiveProjectTab?.Import.IsOverwriteConfirmDialogOpen ?? false;
    public bool ShowWorkspaceDeleteConfirmDialog => ActiveProjectTab?.Catalog.IsDeleteConfirmDialogOpen ?? false;
    public bool ShowGeneralSettingsSection => CurrentSettingsSection == SettingsSections.General;
    public bool ShowAboutSettingsSection => CurrentSettingsSection == SettingsSections.About;
    public bool HasUnreadNotifications => Notifications.Any(item => item.IsUnread);

    public string AppDisplayName { get; } = "ApixPress";
    public string CurrentProjectName => ActiveProjectTab?.Project.Name ?? "项目列表";
    public string CurrentProjectSummary => ActiveProjectTab?.ProjectSummary ?? "在首页选择项目后，会以新的标签页打开快捷请求工作区。";
    public string CurrentEnvironmentLabel => ActiveProjectTab?.CurrentEnvironmentLabel ?? "未选择环境";
    public string BrowserStatusText => ProjectPanel.HasProjects
        ? "选择一个项目会在顶部新开标签页，并保留首页列表。"
        : "当前还没有项目，请先创建一个项目。";
    public string CurrentAppVersion { get; }
    public string CurrentSettingsTitle => ShowAboutSettingsSection ? "关于" : "通用";
    public string UpdateChannelName { get; }
    public string RuntimeStackText { get; } = ".NET 10 / Avalonia 11 / Ursa";
    public string WindowMaximizeGlyph => IsWindowMaximized ? "\u2750" : "\u25A1";
    public string CheckForUpdatesButtonText => IsCheckingForUpdates ? "检查中..." : "检查更新";

    [ObservableProperty]
    private ProjectTabViewModel? activeProjectTab;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = "欢迎使用 ApixPress。";

    [ObservableProperty]
    private bool isCreateProjectDialogOpen;

    [ObservableProperty]
    private bool isEnvironmentManagerOpen;

    [ObservableProperty]
    private bool isSettingsDialogOpen;

    [ObservableProperty]
    private bool isNotificationCenterOpen;

    [ObservableProperty]
    private bool isWindowMaximized;

    [ObservableProperty]
    private string currentSettingsSection = SettingsSections.General;

    [ObservableProperty]
    private decimal requestTimeoutMilliseconds = 30000;

    [ObservableProperty]
    private bool validateSslCertificate = true;

    [ObservableProperty]
    private bool autoFollowRedirects = true;

    [ObservableProperty]
    private bool sendNoCacheHeader;

    [ObservableProperty]
    private bool enableVerboseLogging;

    [ObservableProperty]
    private bool enableUpdateReminder = true;

    [ObservableProperty]
    private string generalSettingsSaveStatus = "设置会自动保存到本地工作目录。";

    [ObservableProperty]
    private bool isCheckingForUpdates;

    [ObservableProperty]
    private string aboutUpdateStatus = "尚未检查更新。";

    [ObservableProperty]
    private string lastUpdateCheckText = "尚未检查";

    [ObservableProperty]
    private string latestAvailableVersion = "尚未检查";

    partial void OnIsCheckingForUpdatesChanged(bool value)
    {
        OnPropertyChanged(nameof(CheckForUpdatesButtonText));
    }

    private static string ResolveCurrentAppVersion()
    {
        var assembly = typeof(MainWindowViewModel).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var metadataSeparatorIndex = informationalVersion.IndexOf('+');
            if (metadataSeparatorIndex >= 0)
            {
                informationalVersion = informationalVersion[..metadataSeparatorIndex];
            }

            informationalVersion = informationalVersion.Trim();
            if (!string.IsNullOrWhiteSpace(informationalVersion))
            {
                return informationalVersion;
            }
        }

        return assembly.GetName().Version?.ToString() ?? "1.0.0.0";
    }
}
