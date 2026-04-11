using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApixPress.App.Models.DTOs;
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
    private readonly IEnvironmentVariableService _environmentVariableService;
    private readonly IRequestCaseService _requestCaseService;
    private readonly IRequestExecutionService _requestExecutionService;
    private readonly IRequestHistoryService _requestHistoryService;
    private readonly RequestConfigTabViewModel _fallbackConfigTab;
    private readonly ResponseSectionViewModel _fallbackResponseSection;
    private readonly EnvironmentPanelViewModel _fallbackEnvironmentPanel;
    private readonly UseCasesPanelViewModel _fallbackUseCasesPanel;
    private readonly RequestHistoryPanelViewModel _fallbackHistoryPanel;
    private bool _initialized;
    private bool _isApplyingShellSettings;

    public MainWindowViewModel(
        IRequestExecutionService requestExecutionService,
        IRequestCaseService requestCaseService,
        IRequestHistoryService requestHistoryService,
        IEnvironmentVariableService environmentVariableService,
        IProjectWorkspaceService projectWorkspaceService,
        IAppShellSettingsService appShellSettingsService)
    {
        _requestExecutionService = requestExecutionService;
        _requestCaseService = requestCaseService;
        _requestHistoryService = requestHistoryService;
        _environmentVariableService = environmentVariableService;
        _appShellSettingsService = appShellSettingsService;

        _fallbackConfigTab = new RequestConfigTabViewModel(null);
        _fallbackResponseSection = new ResponseSectionViewModel();
        _fallbackEnvironmentPanel = new EnvironmentPanelViewModel(environmentVariableService);
        _fallbackUseCasesPanel = new UseCasesPanelViewModel(requestCaseService);
        _fallbackHistoryPanel = new RequestHistoryPanelViewModel(requestHistoryService);

        ProjectPanel = new ProjectPanelViewModel(projectWorkspaceService);
        Notifications = CreateNotifications();

        var version = typeof(MainWindowViewModel).Assembly.GetName().Version;
        CurrentAppVersion = version is null
            ? "1.0.0"
            : $"{version.Major}.{Math.Max(version.Minor, 0)}.{Math.Max(version.Build, 0)}";

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
    public bool ShowProjectListEmptyState => IsHomeTabActive && !ProjectPanel.HasProjects;
    public bool HasEnvironmentContext => ActiveProjectTab?.HasEnvironmentContext ?? false;
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
    public string LatestMockVersion { get; } = "1.1.0";
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
    private string aboutUpdateStatus = "尚未检查更新。";

    [ObservableProperty]
    private string lastUpdateCheckText = "尚未检查";

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        IsBusy = true;
        await LoadShellSettingsAsync();
        await ProjectPanel.LoadProjectsAsync(autoSelect: false);
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
        var tab = ProjectTabs.FirstOrDefault(item => string.Equals(item.ProjectId, project.Id, StringComparison.OrdinalIgnoreCase));
        if (tab is null)
        {
            tab = CreateProjectTab(project);
            ProjectTabs.Add(tab);
            await tab.InitializeAsync();
        }
        else
        {
            SyncTabProject(tab, project);
        }

        ActivateProjectTabCore(tab);
        ProjectPanel.SelectedProject = ProjectPanel.Projects.FirstOrDefault(item =>
            string.Equals(item.Id, project.Id, StringComparison.OrdinalIgnoreCase));
        StatusMessage = $"已打开项目：{tab.Project.Name}";
        IsBusy = false;
        NotifyShellState();
    }

    [RelayCommand]
    private void ActivateHomeTab()
    {
        ActiveProjectTab = null;
        IsEnvironmentManagerOpen = false;
        StatusMessage = BrowserStatusText;
        NotifyShellState();
    }

    [RelayCommand]
    private void ActivateProjectTab(ProjectTabViewModel? tab)
    {
        if (tab is null)
        {
            return;
        }

        ActivateProjectTabCore(tab);
        StatusMessage = tab.StatusMessage;
        NotifyShellState();
    }

    [RelayCommand]
    private void CloseProjectTab(ProjectTabViewModel? tab)
    {
        if (tab is null)
        {
            return;
        }

        var closingIndex = ProjectTabs.IndexOf(tab);
        if (closingIndex < 0)
        {
            return;
        }

        var wasActive = ReferenceEquals(tab, ActiveProjectTab);
        tab.ShellStateChanged -= OnProjectTabShellStateChanged;
        ProjectTabs.Remove(tab);

        if (!wasActive)
        {
            NotifyShellState();
            return;
        }

        if (ProjectTabs.Count == 0)
        {
            ActiveProjectTab = null;
            IsEnvironmentManagerOpen = false;
            StatusMessage = BrowserStatusText;
        }
        else
        {
            var nextIndex = Math.Clamp(closingIndex - 1, 0, ProjectTabs.Count - 1);
            ActivateProjectTabCore(ProjectTabs[nextIndex]);
            StatusMessage = ActiveProjectTab?.StatusMessage ?? BrowserStatusText;
        }

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
        StatusMessage = IsHomeTabActive ? BrowserStatusText : ActiveProjectTab?.StatusMessage ?? BrowserStatusText;
        NotifyShellState();
    }

    [RelayCommand]
    private void OpenEnvironmentManager()
    {
        if (ActiveProjectTab is null)
        {
            StatusMessage = "请先打开一个项目标签页。";
            return;
        }

        IsEnvironmentManagerOpen = true;
        StatusMessage = $"正在管理项目 {ActiveProjectTab.Project.Name} 的环境。";
        NotifyShellState();
    }

    [RelayCommand]
    private void CloseEnvironmentManager()
    {
        IsEnvironmentManagerOpen = false;
        StatusMessage = ActiveProjectTab?.StatusMessage ?? BrowserStatusText;
        NotifyShellState();
    }

    [RelayCommand]
    private async Task SaveEnvironmentManagerAsync()
    {
        if (ActiveProjectTab is null)
        {
            StatusMessage = "请先打开一个项目标签页。";
            return;
        }

        await ActiveProjectTab.SaveCurrentEnvironmentAsync();
        StatusMessage = ActiveProjectTab.StatusMessage;
        NotifyShellState();
    }

    [RelayCommand]
    private async Task SaveAndCloseEnvironmentManagerAsync()
    {
        await SaveEnvironmentManagerAsync();
        if (HasActiveProjectTab)
        {
            CloseEnvironmentManager();
        }
    }

    [RelayCommand]
    private void OpenSettingsDialog()
    {
        CurrentSettingsSection = SettingsSections.General;
        IsSettingsDialogOpen = true;
        IsNotificationCenterOpen = false;
        StatusMessage = "可在这里调整通用设置和查看版本信息。";
        NotifyShellState();
    }

    [RelayCommand]
    private void CloseSettingsDialog()
    {
        IsSettingsDialogOpen = false;
        StatusMessage = ActiveProjectTab?.StatusMessage ?? BrowserStatusText;
        NotifyShellState();
    }

    [RelayCommand]
    private void ShowGeneralSettings()
    {
        CurrentSettingsSection = SettingsSections.General;
        NotifyShellState();
    }

    [RelayCommand]
    private void ShowAboutSettings()
    {
        CurrentSettingsSection = SettingsSections.About;
        NotifyShellState();
    }

    [RelayCommand]
    private void ToggleNotificationCenter()
    {
        IsNotificationCenterOpen = !IsNotificationCenterOpen;
        if (IsNotificationCenterOpen)
        {
            IsSettingsDialogOpen = false;
            MarkAllNotificationsRead();
            StatusMessage = "这里展示近期动态和提醒。";
        }
        else
        {
            StatusMessage = ActiveProjectTab?.StatusMessage ?? BrowserStatusText;
        }

        NotifyShellState();
    }

    [RelayCommand]
    private void MarkAllNotificationsRead()
    {
        foreach (var item in Notifications)
        {
            item.IsUnread = false;
        }

        NotifyShellState();
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        await Task.Delay(240);
        LastUpdateCheckText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        AboutUpdateStatus = $"已完成 Mock 检查，发现可用版本 {LatestMockVersion}。";
        StatusMessage = AboutUpdateStatus;
        NotifyShellState();
    }

    [RelayCommand]
    private async Task RefreshWorkspaceAsync()
    {
        IsBusy = true;
        var activeProjectId = ActiveProjectTab?.ProjectId;
        if (string.IsNullOrWhiteSpace(activeProjectId))
        {
            await ProjectPanel.LoadProjectsAsync(autoSelect: false);
            StatusMessage = BrowserStatusText;
        }
        else
        {
            await ProjectPanel.LoadProjectsAsync(activeProjectId);
            var tab = ProjectTabs.FirstOrDefault(item => string.Equals(item.ProjectId, activeProjectId, StringComparison.OrdinalIgnoreCase));
            if (tab is not null)
            {
                await tab.RefreshAsync();
                ActivateProjectTabCore(tab);
                StatusMessage = tab.StatusMessage;
            }
            else
            {
                ActiveProjectTab = null;
                StatusMessage = BrowserStatusText;
            }
        }

        IsBusy = false;
        NotifyShellState();
    }

    [RelayCommand]
    private async Task SendRequestAsync()
    {
        if (ActiveProjectTab is null)
        {
            StatusMessage = "请先打开一个项目标签页。";
            return;
        }

        await ActiveProjectTab.SendQuickRequestAsync();
        StatusMessage = ActiveProjectTab.StatusMessage;
        NotifyShellState();
    }

    [RelayCommand]
    private async Task SaveCaseAsync()
    {
        if (ActiveProjectTab is null)
        {
            StatusMessage = "请先打开一个项目标签页。";
            return;
        }

        await ActiveProjectTab.SaveCurrentEditorAsync();
        StatusMessage = ActiveProjectTab.StatusMessage;
        NotifyShellState();
    }

    [RelayCommand]
    private void LoadSavedRequest(ExplorerItemViewModel? item)
    {
        if (ActiveProjectTab is null || item is null)
        {
            return;
        }

        ActiveProjectTab.LoadWorkspaceItem(item);
        StatusMessage = ActiveProjectTab.StatusMessage;
        NotifyShellState();
    }

    [RelayCommand]
    private void LoadHistoryItem(RequestHistoryItemViewModel? item)
    {
        if (ActiveProjectTab is null || item is null)
        {
            return;
        }

        ActiveProjectTab.LoadHistoryRequest(item);
        StatusMessage = ActiveProjectTab.StatusMessage;
        NotifyShellState();
    }

    [RelayCommand]
    private async Task SaveHistoryAsCaseAsync(RequestHistoryItemViewModel? item)
    {
        if (ActiveProjectTab is null || item is null)
        {
            return;
        }

        await ActiveProjectTab.SaveHistoryAsQuickRequestAsync(item);
        StatusMessage = ActiveProjectTab.StatusMessage;
        NotifyShellState();
    }

    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        if (ActiveProjectTab is null)
        {
            return;
        }

        await ActiveProjectTab.HistoryPanel.ClearHistoryAsync();
        ActiveProjectTab.StatusMessage = "当前项目的请求历史已清空。";
        StatusMessage = ActiveProjectTab.StatusMessage;
        NotifyShellState();
    }

    public void UpdateWindowState(WindowState state)
    {
        IsWindowMaximized = state == WindowState.Maximized;
        NotifyShellState();
    }

    partial void OnRequestTimeoutMillisecondsChanged(decimal value)
    {
        TriggerShellSettingsSave();
    }

    partial void OnValidateSslCertificateChanged(bool value)
    {
        TriggerShellSettingsSave();
    }

    partial void OnAutoFollowRedirectsChanged(bool value)
    {
        TriggerShellSettingsSave();
    }

    partial void OnSendNoCacheHeaderChanged(bool value)
    {
        TriggerShellSettingsSave();
    }

    partial void OnEnableVerboseLoggingChanged(bool value)
    {
        TriggerShellSettingsSave();
    }

    partial void OnEnableUpdateReminderChanged(bool value)
    {
        TriggerShellSettingsSave();
    }

    partial void OnActiveProjectTabChanged(ProjectTabViewModel? oldValue, ProjectTabViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.IsActive = false;
        }

        foreach (var tab in ProjectTabs)
        {
            tab.IsActive = ReferenceEquals(tab, newValue);
        }

        OnPropertyChanged(nameof(ConfigTab));
        OnPropertyChanged(nameof(ResponseSection));
        OnPropertyChanged(nameof(EnvironmentPanel));
        OnPropertyChanged(nameof(UseCasesPanel));
        OnPropertyChanged(nameof(HistoryPanel));
        OnPropertyChanged(nameof(RequestHistory));
        NotifyShellState();
    }

    private async Task LoadShellSettingsAsync()
    {
        _isApplyingShellSettings = true;
        var result = await _appShellSettingsService.LoadAsync(CancellationToken.None);
        var settings = result.Data ?? new AppShellSettingsDto();

        RequestTimeoutMilliseconds = settings.RequestTimeoutMilliseconds;
        ValidateSslCertificate = settings.ValidateSslCertificate;
        AutoFollowRedirects = settings.AutoFollowRedirects;
        SendNoCacheHeader = settings.SendNoCacheHeader;
        EnableVerboseLogging = settings.EnableVerboseLogging;
        EnableUpdateReminder = settings.EnableUpdateReminder;
        GeneralSettingsSaveStatus = result.IsSuccess
            ? "设置会自动保存到本地工作目录。"
            : "设置读取失败，已回退默认值。";
        _isApplyingShellSettings = false;
    }

    private void TriggerShellSettingsSave()
    {
        if (_isApplyingShellSettings || !_initialized)
        {
            return;
        }

        _ = SaveShellSettingsAsync();
    }

    private async Task SaveShellSettingsAsync()
    {
        var result = await _appShellSettingsService.SaveAsync(new AppShellSettingsDto
        {
            RequestTimeoutMilliseconds = (int)RequestTimeoutMilliseconds,
            ValidateSslCertificate = ValidateSslCertificate,
            AutoFollowRedirects = AutoFollowRedirects,
            SendNoCacheHeader = SendNoCacheHeader,
            EnableVerboseLogging = EnableVerboseLogging,
            EnableUpdateReminder = EnableUpdateReminder
        }, CancellationToken.None);

        GeneralSettingsSaveStatus = result.IsSuccess
            ? $"已自动保存 {DateTime.Now:HH:mm:ss}"
            : $"保存失败：{result.Message}";
        NotifyShellState();
    }

    private ProjectTabViewModel CreateProjectTab(ProjectWorkspaceItemViewModel project)
    {
        var tab = new ProjectTabViewModel(
            project,
            _requestExecutionService,
            _requestCaseService,
            _requestHistoryService,
            _environmentVariableService);
        tab.ShellStateChanged += OnProjectTabShellStateChanged;
        return tab;
    }

    private void ActivateProjectTabCore(ProjectTabViewModel tab)
    {
        ActiveProjectTab = tab;
        IsEnvironmentManagerOpen = false;
    }

    private void OnProjectCreated()
    {
        IsCreateProjectDialogOpen = false;
        StatusMessage = "项目已创建，可在首页卡片中继续打开为新标签页。";
        NotifyShellState();
    }

    private void OnProjectsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncProjectTabsWithProjectList();
    }

    private void SyncProjectTabsWithProjectList()
    {
        var sourceLookup = ProjectPanel.Projects.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var removedTabs = ProjectTabs
            .Where(tab => !sourceLookup.ContainsKey(tab.ProjectId))
            .ToList();

        foreach (var tab in ProjectTabs)
        {
            if (sourceLookup.TryGetValue(tab.ProjectId, out var source))
            {
                SyncTabProject(tab, source);
            }
        }

        foreach (var tab in removedTabs)
        {
            tab.ShellStateChanged -= OnProjectTabShellStateChanged;
            ProjectTabs.Remove(tab);
        }

        if (ActiveProjectTab is not null && !ProjectTabs.Contains(ActiveProjectTab))
        {
            ActiveProjectTab = ProjectTabs.FirstOrDefault();
            if (ActiveProjectTab is null)
            {
                IsEnvironmentManagerOpen = false;
                StatusMessage = BrowserStatusText;
            }
        }

        NotifyShellState();
    }

    private static void SyncTabProject(ProjectTabViewModel tab, ProjectWorkspaceItemViewModel source)
    {
        tab.Project.Name = source.Name;
        tab.Project.Description = source.Description;
        tab.Project.IsDefault = source.IsDefault;
    }

    private void OnProjectTabShellStateChanged(ProjectTabViewModel tab)
    {
        if (ReferenceEquals(tab, ActiveProjectTab) && !string.IsNullOrWhiteSpace(tab.StatusMessage))
        {
            StatusMessage = tab.StatusMessage;
        }

        NotifyShellState();
    }

    private void OnProjectPanelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProjectPanelViewModel.SelectedProject)
            or nameof(ProjectPanelViewModel.HasProjects)
            or nameof(ProjectPanelViewModel.SearchText)
            or nameof(ProjectPanelViewModel.HasSelectedProject))
        {
            NotifyShellState();
        }
    }

    private void OnNotificationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NotificationItemViewModel.IsUnread))
        {
            NotifyShellState();
        }
    }

    private void OnProjectTabsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasProjectTabs));
        NotifyShellState();
    }

    private void NotifyShellState()
    {
        OnPropertyChanged(nameof(IsHomeTabActive));
        OnPropertyChanged(nameof(HasActiveProjectTab));
        OnPropertyChanged(nameof(HasProjectTabs));
        OnPropertyChanged(nameof(IsProjectBrowserMode));
        OnPropertyChanged(nameof(IsWorkspaceMode));
        OnPropertyChanged(nameof(ShowProjectListEmptyState));
        OnPropertyChanged(nameof(HasEnvironmentContext));
        OnPropertyChanged(nameof(ShowGeneralSettingsSection));
        OnPropertyChanged(nameof(ShowAboutSettingsSection));
        OnPropertyChanged(nameof(HasUnreadNotifications));
        OnPropertyChanged(nameof(CurrentProjectName));
        OnPropertyChanged(nameof(CurrentProjectSummary));
        OnPropertyChanged(nameof(CurrentEnvironmentLabel));
        OnPropertyChanged(nameof(BrowserStatusText));
        OnPropertyChanged(nameof(CurrentSettingsTitle));
        OnPropertyChanged(nameof(WindowMaximizeGlyph));
    }

    private static ObservableCollection<NotificationItemViewModel> CreateNotifications()
    {
        return
        [
            new NotificationItemViewModel
            {
                Title = "欢迎使用 ApixPress",
                Message = "首页会固定展示项目列表，打开项目后会在顶部新增工作标签。",
                RelativeTimeText = "刚刚",
                IsUnread = true
            },
            new NotificationItemViewModel
            {
                Title = "快捷请求已切到项目工作区",
                Message = "每个项目标签页会独立保存环境、历史记录和保存请求。",
                RelativeTimeText = "2 分钟前",
                IsUnread = true
            },
            new NotificationItemViewModel
            {
                Title = "环境管理弹框已上线",
                Message = "现在可以在项目页右上角集中管理项目级环境和变量。",
                RelativeTimeText = "5 分钟前",
                IsUnread = false
            }
        ];
    }
}
