using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels.Base;
using Irihi.Avalonia.Shared.Contracts;
using Ursa.Controls;

namespace ApixPress.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IDialogContext
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
        private readonly IDialogService _dialogService;
        private readonly Func<object?> _getShellDataContext;
        private readonly Action _requestCloseShellDialog;
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
            IDialogService dialogService,
            Func<object?> getShellDataContext,
            Action requestCloseShellDialog,
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
            _dialogService = dialogService;
            _getShellDataContext = getShellDataContext;
            _requestCloseShellDialog = requestCloseShellDialog;
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
                FallbackConfigTab = new RequestConfigTabViewModel(_filePickerService),
                FallbackResponseSection = new ResponseSectionViewModel(),
                FallbackEnvironmentPanel = new EnvironmentPanelViewModel(_environmentVariableService),
                FallbackUseCasesPanel = new UseCasesPanelViewModel(_requestCaseService, dialogService: _dialogService),
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
                    _getDefaultStatusMessage,
                    _dialogService,
                    _getShellDataContext,
                    _requestCloseShellDialog)
            };
        }
    }

    private readonly IEnvironmentVariableService _environmentVariableService;
    private readonly IRequestCaseService _requestCaseService;
    private readonly IRequestExecutionService _requestExecutionService;
    private readonly IRequestHistoryService _requestHistoryService;
    private readonly ISystemDataService _systemDataService;
    private readonly IProjectWorkspaceService _projectWorkspaceService;
    private readonly IApplicationUpdateService _applicationUpdateService;
    private readonly IApiWorkspaceService _apiWorkspaceService;
    private readonly IFilePickerService _filePickerService;
    private readonly IAppNotificationService _appNotificationService;
    private readonly IProjectDataExportService _projectDataExportService;
    private readonly IProjectHttpSettingsService _projectHttpSettingsService;
    private readonly IClipboardService _clipboardService;
    private readonly IDialogService _dialogService;
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
        IProjectHttpSettingsService projectHttpSettingsService,
        IClipboardService clipboardService,
        IWindowHostService windowHostService,
        IDialogService dialogService)
    {
        _requestExecutionService = requestExecutionService;
        _requestCaseService = requestCaseService;
        _requestHistoryService = requestHistoryService;
        _systemDataService = systemDataService;
        _projectWorkspaceService = projectWorkspaceService;
        _environmentVariableService = environmentVariableService;
        _applicationUpdateService = applicationUpdateService;
        _apiWorkspaceService = apiWorkspaceService;
        _filePickerService = filePickerService;
        _appNotificationService = appNotificationService;
        _projectDataExportService = projectDataExportService;
        _projectHttpSettingsService = projectHttpSettingsService;
        _clipboardService = clipboardService;
        _dialogService = dialogService;

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
            dialogService,
            () => this,
            () => RequestClose?.Invoke(this, DialogResult.None),
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

    public string AppDisplayName { get; } = "ApixPress";
    public string CurrentProjectName => ActiveProjectTab?.Project.Name ?? "项目列表";
    public string CurrentProjectSummary => ActiveProjectTab?.Summary.ProjectSummary ?? "在首页选择项目后，会以新的标签页打开快捷请求工作区。";
    public string CurrentEnvironmentLabel => ActiveProjectTab?.Summary.CurrentEnvironmentLabel ?? "未选择环境";
    public string BrowserStatusText => ProjectPanel.HasProjects
        ? "选择一个项目会在顶部新开标签页，并保留首页列表。"
        : "当前还没有项目，请先创建一个项目。";
    public string RuntimeStackText { get; } = ".NET 10 / Avalonia 12 / Ursa 2";
    public string WindowMaximizeGlyph => IsWindowMaximized ? "\u2750" : "\u25A1";

    [ObservableProperty]
    private ProjectTabViewModel? activeProjectTab;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = "欢迎使用 ApixPress。";

    [ObservableProperty]
    private bool isWindowMaximized;

    /// <summary>弹窗异步关闭通道：环境管理、新建项目、设置弹窗共用（同一时刻至多一个弹窗打开，互不叠加）。</summary>
    public event EventHandler<object?>? RequestClose;

    public void Close() => RequestClose?.Invoke(this, null);

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
        _fallbackConfigTab.Dispose();
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
        NotifyActiveProjectTabBindings();
        NotifyShellState();
    }
}
