using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private sealed class ConstructionResult
    {
        public required RequestConfigTabViewModel FallbackConfigTab { get; init; }
        public required ResponseSectionViewModel FallbackResponseSection { get; init; }
        public required EnvironmentPanelViewModel FallbackEnvironmentPanel { get; init; }
        public required UseCasesPanelViewModel FallbackUseCasesPanel { get; init; }
        public required RequestHistoryPanelViewModel FallbackHistoryPanel { get; init; }
        public required ProjectPanelViewModel ProjectPanel { get; init; }
        public required MainWindowShellPanelsViewModel ShellPanels { get; init; }
    }

    private sealed class Builder
    {
        private readonly IEnvironmentVariableService _environmentVariableService;
        private readonly IRequestCaseService _requestCaseService;
        private readonly IRequestHistoryService _requestHistoryService;
        private readonly ISystemDataService _systemDataService;
        private readonly IProjectWorkspaceService _projectWorkspaceService;
        private readonly IAppShellSettingsService _appShellSettingsService;
        private readonly IApplicationUpdateService _applicationUpdateService;
        private readonly IApplicationRestartService _applicationRestartService;
        private readonly IFilePickerService _filePickerService;
        private readonly IWindowHostService _windowHostService;
        private readonly string _currentAppVersion;
        private readonly Action<string> _setStatusMessage;
        private readonly Action _notifyShellState;
        private readonly Action _clearSystemDataViews;
        private readonly Func<string> _getDefaultStatusMessage;

        public Builder(
            IEnvironmentVariableService environmentVariableService,
            IRequestCaseService requestCaseService,
            IRequestHistoryService requestHistoryService,
            ISystemDataService systemDataService,
            IProjectWorkspaceService projectWorkspaceService,
            IAppShellSettingsService appShellSettingsService,
            IApplicationUpdateService applicationUpdateService,
            IApplicationRestartService applicationRestartService,
            IFilePickerService filePickerService,
            IWindowHostService windowHostService,
            string currentAppVersion,
            Action<string> setStatusMessage,
            Action notifyShellState,
            Action clearSystemDataViews,
            Func<string> getDefaultStatusMessage)
        {
            _environmentVariableService = environmentVariableService;
            _requestCaseService = requestCaseService;
            _requestHistoryService = requestHistoryService;
            _systemDataService = systemDataService;
            _projectWorkspaceService = projectWorkspaceService;
            _appShellSettingsService = appShellSettingsService;
            _applicationUpdateService = applicationUpdateService;
            _applicationRestartService = applicationRestartService;
            _filePickerService = filePickerService;
            _windowHostService = windowHostService;
            _currentAppVersion = currentAppVersion;
            _setStatusMessage = setStatusMessage;
            _notifyShellState = notifyShellState;
            _clearSystemDataViews = clearSystemDataViews;
            _getDefaultStatusMessage = getDefaultStatusMessage;
        }

        public ConstructionResult Build()
        {
            return new ConstructionResult
            {
                FallbackConfigTab = new RequestConfigTabViewModel(null),
                FallbackResponseSection = new ResponseSectionViewModel(),
                FallbackEnvironmentPanel = new EnvironmentPanelViewModel(_environmentVariableService),
                FallbackUseCasesPanel = new UseCasesPanelViewModel(_requestCaseService),
                FallbackHistoryPanel = new RequestHistoryPanelViewModel(_requestHistoryService),
                ProjectPanel = new ProjectPanelViewModel(_projectWorkspaceService),
                ShellPanels = new MainWindowShellPanelsViewModel(
                    new MainWindowSettingsViewModel(
                        _appShellSettingsService,
                        _applicationUpdateService,
                        _applicationRestartService,
                        _windowHostService,
                        _filePickerService,
                        _systemDataService,
                        _currentAppVersion,
                        _setStatusMessage,
                        _notifyShellState,
                        _clearSystemDataViews),
                    CreateNotifications(),
                    _setStatusMessage,
                    _getDefaultStatusMessage)
            };
        }
    }

    private readonly IEnvironmentVariableService _environmentVariableService;
    private readonly IRequestCaseService _requestCaseService;
    private readonly IRequestExecutionService _requestExecutionService;
    private readonly IRequestHistoryService _requestHistoryService;
    private readonly IApiWorkspaceService _apiWorkspaceService;
    private readonly IFilePickerService _filePickerService;
    private readonly IAppNotificationService _appNotificationService;
    private readonly IProjectDataExportService _projectDataExportService;
    private readonly RequestConfigTabViewModel _fallbackConfigTab;
    private readonly ResponseSectionViewModel _fallbackResponseSection;
    private readonly EnvironmentPanelViewModel _fallbackEnvironmentPanel;
    private readonly UseCasesPanelViewModel _fallbackUseCasesPanel;
    private readonly RequestHistoryPanelViewModel _fallbackHistoryPanel;
    private bool _initialized;

    public MainWindowViewModel(
        IRequestExecutionService requestExecutionService,
        IRequestCaseService requestCaseService,
        IRequestHistoryService requestHistoryService,
        ISystemDataService systemDataService,
        IEnvironmentVariableService environmentVariableService,
        IProjectWorkspaceService projectWorkspaceService,
        IAppShellSettingsService appShellSettingsService,
        IApplicationUpdateService applicationUpdateService,
        IApplicationRestartService applicationRestartService,
        IApiWorkspaceService apiWorkspaceService,
        IFilePickerService filePickerService,
        IAppNotificationService appNotificationService,
        IProjectDataExportService projectDataExportService,
        IWindowHostService windowHostService)
    {
        _requestExecutionService = requestExecutionService;
        _requestCaseService = requestCaseService;
        _requestHistoryService = requestHistoryService;
        _environmentVariableService = environmentVariableService;
        _apiWorkspaceService = apiWorkspaceService;
        _filePickerService = filePickerService;
        _appNotificationService = appNotificationService;
        _projectDataExportService = projectDataExportService;

        var construction = new Builder(
            environmentVariableService,
            requestCaseService,
            requestHistoryService,
            systemDataService,
            projectWorkspaceService,
            appShellSettingsService,
            applicationUpdateService,
            applicationRestartService,
            filePickerService,
            windowHostService,
            ResolveCurrentAppVersion(),
            message => StatusMessage = message,
            NotifyShellState,
            ClearSystemDataViews,
            () => ActiveProjectTab?.StatusMessage ?? BrowserStatusText)
            .Build();

        _fallbackConfigTab = construction.FallbackConfigTab;
        _fallbackResponseSection = construction.FallbackResponseSection;
        _fallbackEnvironmentPanel = construction.FallbackEnvironmentPanel;
        _fallbackUseCasesPanel = construction.FallbackUseCasesPanel;
        _fallbackHistoryPanel = construction.FallbackHistoryPanel;

        ProjectPanel = construction.ProjectPanel;
        ShellPanels = construction.ShellPanels;

        AttachShellEventHandlers();
    }

    public ProjectPanelViewModel ProjectPanel { get; }
    public ObservableCollection<ProjectTabViewModel> ProjectTabs { get; } = [];
    public MainWindowShellPanelsViewModel ShellPanels { get; }
    public MainWindowSettingsViewModel SettingsCenter => ShellPanels.SettingsCenter;

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
    public bool HasEnvironmentContext => ActiveProjectTab?.Summary.HasEnvironmentContext ?? false;
    public bool ShowQuickRequestSaveDialog => ActiveProjectTab?.QuickRequestSave.IsDialogOpen ?? false;
    public bool ShowProjectImportDialog => ActiveProjectTab?.Import.IsDialogOpen ?? false;
    public bool ShowProjectImportOverwriteConfirmDialog => ActiveProjectTab?.Import.IsOverwriteConfirmDialogOpen ?? false;
    public bool ShowWorkspaceDeleteConfirmDialog => ActiveProjectTab?.Catalog.IsDeleteConfirmDialogOpen ?? false;

    public string AppDisplayName { get; } = "ApixPress";
    public string CurrentProjectName => ActiveProjectTab?.Project.Name ?? "项目列表";
    public string CurrentProjectSummary => ActiveProjectTab?.Summary.ProjectSummary ?? "在首页选择项目后，会以新的标签页打开快捷请求工作区。";
    public string CurrentEnvironmentLabel => ActiveProjectTab?.Summary.CurrentEnvironmentLabel ?? "未选择环境";
    public string BrowserStatusText => ProjectPanel.HasProjects
        ? "选择一个项目会在顶部新开标签页，并保留首页列表。"
        : "当前还没有项目，请先创建一个项目。";
    public string RuntimeStackText { get; } = ".NET 10 / Avalonia 11 / Ursa";
    public string WindowMaximizeGlyph => IsWindowMaximized ? "\u2750" : "\u25A1";

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
    private bool isWindowMaximized;

    protected override void DisposeManaged()
    {
        DetachShellEventHandlers();

        foreach (var tab in ProjectTabs.ToList())
        {
            ReleaseProjectTab(tab);
        }

        ProjectTabs.Clear();
        ActiveProjectTab = null;
        ProjectPanel.Dispose();
        ShellPanels.Dispose();
        _fallbackEnvironmentPanel.Dispose();
        _fallbackUseCasesPanel.Dispose();
        _fallbackHistoryPanel.Dispose();
    }

    private void AttachShellEventHandlers()
    {
        ProjectTabs.CollectionChanged += OnProjectTabsCollectionChanged;
        ProjectPanel.ProjectCreated += OnProjectCreated;
        ProjectPanel.PropertyChanged += OnProjectPanelPropertyChanged;
        ProjectPanel.Projects.CollectionChanged += OnProjectsCollectionChanged;
    }

    private void DetachShellEventHandlers()
    {
        ProjectTabs.CollectionChanged -= OnProjectTabsCollectionChanged;
        ProjectPanel.ProjectCreated -= OnProjectCreated;
        ProjectPanel.PropertyChanged -= OnProjectPanelPropertyChanged;
        ProjectPanel.Projects.CollectionChanged -= OnProjectsCollectionChanged;
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

    private void ClearSystemDataViews()
    {
        _fallbackHistoryPanel.HistoryItems.Clear();
        ProjectPanel.ClearProjects();

        foreach (var tab in ProjectTabs.ToList())
        {
            ProjectTabs.Remove(tab);
            ReleaseProjectTab(tab);
        }

        ActiveProjectTab = null;
        IsEnvironmentManagerOpen = false;
        NotifyActiveProjectTabBindings();
        NotifyShellState();
    }
}
