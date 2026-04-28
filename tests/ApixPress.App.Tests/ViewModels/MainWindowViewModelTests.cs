using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels;
using Azrng.Core.Results;
using FakeAppNotificationService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeAppNotificationService;
using FakeApplicationRestartService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeApplicationRestartService;
using FakeSystemDataService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeSystemDataService;
using System.ComponentModel;

namespace ApixPress.App.Tests.ViewModels;

public sealed partial class MainWindowViewModelTests
{
    [Fact]
    public async Task InitializeAsync_ShouldLoadProjectsAndShellSettings()
    {
        var projectService = new FakeProjectWorkspaceService();
        projectService.SeedProjects(
        [
            new("project-1", "订单项目", "订单接口", true),
            new("project-2", "用户项目", "用户接口", false)
        ]);
        var shellSettingsService = new FakeAppShellSettingsService
        {
            CurrentSettings = new()
            {
                RequestTimeoutMilliseconds = 45000,
                ValidateSslCertificate = false,
                AutoFollowRedirects = false,
                SendNoCacheHeader = true,
                EnableVerboseLogging = true,
                EnableUpdateReminder = false,
                StorageDirectoryPath = @"D:\ApixPressData"
            }
        };
        var viewModel = CreateViewModel(projectService, shellSettingsService);

        await viewModel.InitializeAsync();

        Assert.Equal(2, viewModel.ProjectPanel.Projects.Count);
        Assert.Equal(45000, viewModel.SettingsCenter.RequestTimeoutMilliseconds);
        Assert.False(viewModel.SettingsCenter.ValidateSslCertificate);
        Assert.False(viewModel.SettingsCenter.AutoFollowRedirects);
        Assert.True(viewModel.SettingsCenter.SendNoCacheHeader);
        Assert.True(viewModel.SettingsCenter.EnableVerboseLogging);
        Assert.False(viewModel.SettingsCenter.EnableUpdateReminder);
        Assert.Equal(@"D:\ApixPressData", viewModel.SettingsCenter.StorageDirectoryPath);
        Assert.Equal(viewModel.BrowserStatusText, viewModel.StatusMessage);
        Assert.True(viewModel.IsHomeTabActive);
        Assert.False(viewModel.ShowProjectListEmptyState);
        Assert.Empty(viewModel.ShellPanels.Notifications);
        Assert.False(viewModel.ShellPanels.HasNotifications);
        Assert.True(viewModel.ShellPanels.ShowNotificationEmptyState);
        Assert.False(viewModel.ShellPanels.HasUnreadNotifications);
    }

    [Fact]
    public async Task ConfirmClearSystemDataCommand_ShouldClearProjectsAndTabsAfterConfirmation()
    {
        var projectService = new FakeProjectWorkspaceService();
        projectService.SeedProjects(
        [
            new("project-1", "订单项目", "订单接口", true)
        ]);
        var systemDataService = new FakeSystemDataService();
        var viewModel = CreateViewModel(projectService, systemDataService: systemDataService);
        await viewModel.InitializeAsync();
        var project = viewModel.ProjectPanel.Projects.Single();
        await viewModel.OpenProjectWorkspaceCommand.ExecuteAsync(project);

        viewModel.SettingsCenter.RequestClearSystemDataCommand.Execute(null);
        await viewModel.SettingsCenter.ConfirmClearSystemDataCommand.ExecuteAsync(null);

        Assert.Equal(1, systemDataService.ClearAllCallCount);
        Assert.False(viewModel.SettingsCenter.IsClearSystemDataConfirmDialogOpen);
        Assert.Empty(viewModel.ProjectPanel.Projects);
        Assert.Empty(viewModel.ProjectPanel.FilteredProjects);
        Assert.Empty(viewModel.ProjectTabs);
        Assert.Null(viewModel.ActiveProjectTab);
        Assert.True(viewModel.ShowProjectListEmptyState);
        Assert.True(viewModel.SettingsCenter.IsSystemDataRestartPromptOpen);
        Assert.Equal("已清空所有系统数据，建议重启应用以释放全部运行状态。", viewModel.SettingsCenter.ClearSystemDataStatus);
        Assert.Equal(viewModel.SettingsCenter.ClearSystemDataStatus, viewModel.StatusMessage);
    }

    [Fact]
    public async Task ConfirmDeleteProjectFromSettingsCommand_ShouldRemoveProjectTabAndListItem()
    {
        var projectService = new FakeProjectWorkspaceService();
        projectService.SeedProjects(
        [
            new("project-1", "订单项目", "订单接口", true),
            new("project-2", "用户项目", "用户接口", false)
        ]);
        var viewModel = CreateViewModel(projectService);
        await viewModel.InitializeAsync();
        var project = viewModel.ProjectPanel.Projects.Single(item => item.Id == "project-1");
        await viewModel.OpenProjectWorkspaceCommand.ExecuteAsync(project);

        viewModel.ActiveProjectTab!.Settings.RequestDeleteProjectCommand.Execute(null);
        await viewModel.ActiveProjectTab.Settings.ConfirmDeleteProjectCommand.ExecuteAsync(null);

        Assert.DoesNotContain(viewModel.ProjectPanel.Projects, item => item.Id == "project-1");
        Assert.DoesNotContain(viewModel.ProjectTabs, item => item.ProjectId == "project-1");
        Assert.True(viewModel.IsHomeTabActive);
        Assert.Equal("项目已删除，已返回项目列表。", viewModel.StatusMessage);
    }

    [Fact]
    public async Task DismissSystemDataRestartPromptCommand_ShouldKeepApplicationOpen()
    {
        var systemDataService = new FakeSystemDataService();
        var restartService = new FakeApplicationRestartService();
        var viewModel = CreateViewModel(
            systemDataService: systemDataService,
            applicationRestartService: restartService);
        await viewModel.InitializeAsync();
        viewModel.SettingsCenter.RequestClearSystemDataCommand.Execute(null);
        await viewModel.SettingsCenter.ConfirmClearSystemDataCommand.ExecuteAsync(null);

        viewModel.SettingsCenter.DismissSystemDataRestartPromptCommand.Execute(null);

        Assert.False(viewModel.SettingsCenter.IsSystemDataRestartPromptOpen);
        Assert.Equal(0, restartService.RestartCallCount);
        Assert.Equal("已稍后重启，系统数据已清空。", viewModel.StatusMessage);
    }

    [Fact]
    public async Task RestartApplicationCommand_ShouldStartRestartService()
    {
        var systemDataService = new FakeSystemDataService();
        var restartService = new FakeApplicationRestartService();
        var viewModel = CreateViewModel(
            systemDataService: systemDataService,
            applicationRestartService: restartService);
        await viewModel.InitializeAsync();
        viewModel.SettingsCenter.RequestClearSystemDataCommand.Execute(null);
        await viewModel.SettingsCenter.ConfirmClearSystemDataCommand.ExecuteAsync(null);

        await viewModel.SettingsCenter.RestartApplicationCommand.ExecuteAsync(null);

        Assert.Equal(1, restartService.RestartCallCount);
        Assert.Equal("正在重启 ApixPress...", viewModel.StatusMessage);
    }

    [Fact]
    public async Task OpenProjectWorkspaceCommand_ShouldReuseExistingProjectTab()
    {
        var projectService = new FakeProjectWorkspaceService();
        projectService.SeedProjects(
        [
            new("project-1", "订单项目", "订单接口", true)
        ]);
        var viewModel = CreateViewModel(projectService);
        await viewModel.InitializeAsync();
        var project = viewModel.ProjectPanel.Projects.Single();

        await viewModel.OpenProjectWorkspaceCommand.ExecuteAsync(project);
        await viewModel.OpenProjectWorkspaceCommand.ExecuteAsync(project);

        Assert.Single(viewModel.ProjectTabs);
        Assert.Same(viewModel.ProjectTabs[0], viewModel.ActiveProjectTab);
        Assert.Equal(project.Id, viewModel.ActiveProjectTab?.ProjectId);
        Assert.True(viewModel.IsWorkspaceMode);
        Assert.Equal("已打开项目：订单项目", viewModel.StatusMessage);
    }

    [Fact]
    public async Task OpenProjectWorkspaceCommand_ShouldActivateProjectTabBeforeInitializationCompletes()
    {
        var projectService = new FakeProjectWorkspaceService();
        projectService.SeedProjects(
        [
            new("project-1", "订单项目", "订单接口", true)
        ]);
        var loadGate = new TaskCompletionSource<bool>();
        var environmentService = new DeferredEnvironmentVariableService(loadGate);
        var viewModel = CreateViewModel(projectService, environmentVariableService: environmentService);
        await viewModel.InitializeAsync();
        var project = viewModel.ProjectPanel.Projects.Single();

        var openTask = viewModel.OpenProjectWorkspaceCommand.ExecuteAsync(project);

        Assert.True(SpinWait.SpinUntil(() => viewModel.ActiveProjectTab is not null, TimeSpan.FromSeconds(1)));
        Assert.Single(viewModel.ProjectTabs);
        Assert.Same(viewModel.ProjectTabs[0], viewModel.ActiveProjectTab);
        Assert.True(viewModel.ActiveProjectTab!.IsWorkspaceLoading);
        Assert.Contains("订单项目", viewModel.ActiveProjectTab.WorkspaceLoadingText);
        Assert.False(openTask.IsCompleted);

        loadGate.SetResult(true);
        await openTask;

        Assert.False(viewModel.ActiveProjectTab.IsWorkspaceLoading);
        Assert.Equal("已打开项目：订单项目", viewModel.StatusMessage);
    }

    [Fact]
    public async Task ActivateProjectTabCommand_ShouldNotifyMainWindowShellBindingsOnlyOncePerSwitch()
    {
        var projectService = new FakeProjectWorkspaceService();
        projectService.SeedProjects(
        [
            new("project-1", "订单项目", "订单接口", true),
            new("project-2", "用户项目", "用户接口", false)
        ]);
        var viewModel = CreateViewModel(projectService);
        await viewModel.InitializeAsync();

        await viewModel.OpenProjectWorkspaceCommand.ExecuteAsync(viewModel.ProjectPanel.Projects[0]);
        var firstTab = Assert.Single(viewModel.ProjectTabs);

        await viewModel.OpenProjectWorkspaceCommand.ExecuteAsync(viewModel.ProjectPanel.Projects[1]);
        var secondTab = Assert.Single(viewModel.ProjectTabs, tab => tab.ProjectId == "project-2");

        var propertyChangeCount = 0;
        void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainWindowViewModel.CurrentProjectName))
            {
                propertyChangeCount++;
            }
        }

        viewModel.PropertyChanged += OnPropertyChanged;
        try
        {
            viewModel.ActivateProjectTabCommand.Execute(firstTab);
        }
        finally
        {
            viewModel.PropertyChanged -= OnPropertyChanged;
        }

        Assert.Same(firstTab, viewModel.ActiveProjectTab);
        Assert.False(secondTab.IsActive);
        Assert.True(firstTab.IsActive);
        Assert.Equal(1, propertyChangeCount);
    }

    [Fact]
    public async Task InactiveProjectTabStateChange_ShouldNotRefreshMainWindowShellState()
    {
        var projectService = new FakeProjectWorkspaceService();
        projectService.SeedProjects(
        [
            new("project-1", "订单项目", "订单接口", true),
            new("project-2", "用户项目", "用户接口", false)
        ]);
        var viewModel = CreateViewModel(projectService);
        await viewModel.InitializeAsync();

        await viewModel.OpenProjectWorkspaceCommand.ExecuteAsync(viewModel.ProjectPanel.Projects[0]);
        var firstTab = Assert.Single(viewModel.ProjectTabs);

        await viewModel.OpenProjectWorkspaceCommand.ExecuteAsync(viewModel.ProjectPanel.Projects[1]);
        var secondTab = Assert.Single(viewModel.ProjectTabs, tab => tab.ProjectId == "project-2");

        viewModel.ActivateProjectTabCommand.Execute(firstTab);
        var statusBefore = viewModel.StatusMessage;
        var propertyChanges = new List<string>();
        void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.PropertyName))
            {
                propertyChanges.Add(e.PropertyName!);
            }
        }

        viewModel.PropertyChanged += OnPropertyChanged;
        try
        {
            secondTab.Workspace.ToggleWorkspaceTabMenuCommand.Execute(null);
        }
        finally
        {
            viewModel.PropertyChanged -= OnPropertyChanged;
        }

        Assert.True(secondTab.Workspace.IsWorkspaceTabMenuOpen);
        Assert.Same(firstTab, viewModel.ActiveProjectTab);
        Assert.Equal(statusBefore, viewModel.StatusMessage);
        Assert.Empty(propertyChanges);
    }

    [Fact]
    public async Task CheckForUpdatesCommand_ShouldUpdateStatusWhenAlreadyLatest()
    {
        var updateService = new FakeApplicationUpdateService
        {
            CheckResult = new AppUpdateCheckResultDto
            {
                CurrentVersion = "1.0.0.0",
                LatestVersion = "1.0.0.0",
                HasUpdate = false
            }
        };
        var viewModel = CreateViewModel(applicationUpdateService: updateService);
        await viewModel.InitializeAsync();

        await viewModel.SettingsCenter.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.Equal(1, updateService.CheckCalls);
        Assert.Equal(0, updateService.StartCalls);
        Assert.Equal("1.0.0.0", viewModel.SettingsCenter.LatestAvailableVersion);
        Assert.Equal("当前已是最新版本 1.0.0.0。", viewModel.SettingsCenter.AboutUpdateStatus);
        Assert.Equal(viewModel.SettingsCenter.AboutUpdateStatus, viewModel.StatusMessage);
        Assert.NotEqual("尚未检查", viewModel.SettingsCenter.LastUpdateCheckText);
    }

    [Fact]
    public async Task CheckForUpdatesCommand_ShouldStartUpdateFlowWhenNewVersionDetected()
    {
        var updateService = new FakeApplicationUpdateService
        {
            CheckResult = new AppUpdateCheckResultDto
            {
                CurrentVersion = "1.0.0.0",
                LatestVersion = "1.1.0.0",
                HasUpdate = true
            }
        };
        var viewModel = CreateViewModel(applicationUpdateService: updateService);
        await viewModel.InitializeAsync();

        await viewModel.SettingsCenter.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.Equal(1, updateService.CheckCalls);
        Assert.Equal(1, updateService.StartCalls);
        Assert.Equal("1.1.0.0", viewModel.SettingsCenter.LatestAvailableVersion);
    }

    [Fact]
    public async Task InitializeAsync_ShouldNotifyWhenUpdateReminderEnabledAndNewVersionDetected()
    {
        var shellSettingsService = new FakeAppShellSettingsService
        {
            CurrentSettings = new AppShellSettingsDto
            {
                EnableUpdateReminder = true
            }
        };
        var updateService = new FakeApplicationUpdateService
        {
            CheckResult = new AppUpdateCheckResultDto
            {
                CurrentVersion = "1.0.0.0",
                LatestVersion = "1.2.0.0",
                HasUpdate = true
            }
        };
        var notificationService = new FakeAppNotificationService();
        var viewModel = CreateViewModel(
            shellSettingsService: shellSettingsService,
            applicationUpdateService: updateService,
            appNotificationService: notificationService);

        await viewModel.InitializeAsync();

        Assert.True(SpinWait.SpinUntil(() => notificationService.Notifications.Count == 1, TimeSpan.FromSeconds(1)));
        Assert.Equal(1, updateService.CheckCalls);
        var toast = Assert.Single(notificationService.Notifications);
        Assert.Equal("发现新版本", toast.Title);
        Assert.Contains("1.2.0.0", toast.Content);

        var notification = viewModel.ShellPanels.Notifications[0];
        Assert.True(notification.IsUnread);
        Assert.Equal("发现新版本", notification.Title);
        Assert.Contains("1.2.0.0", notification.Message);
        Assert.True(viewModel.ShellPanels.HasUnreadNotifications);
    }

    [Fact]
    public async Task InitializeAsync_ShouldSkipStartupUpdateCheckWhenReminderDisabled()
    {
        var shellSettingsService = new FakeAppShellSettingsService
        {
            CurrentSettings = new AppShellSettingsDto
            {
                EnableUpdateReminder = false
            }
        };
        var updateService = new FakeApplicationUpdateService
        {
            CheckResult = new AppUpdateCheckResultDto
            {
                CurrentVersion = "1.0.0.0",
                LatestVersion = "1.2.0.0",
                HasUpdate = true
            }
        };
        var notificationService = new FakeAppNotificationService();
        var viewModel = CreateViewModel(
            shellSettingsService: shellSettingsService,
            applicationUpdateService: updateService,
            appNotificationService: notificationService);

        await viewModel.InitializeAsync();

        Assert.Equal(0, updateService.CheckCalls);
        Assert.Empty(notificationService.Notifications);
        Assert.DoesNotContain(viewModel.ShellPanels.Notifications, item => item.Title == "发现新版本");
    }

    [Fact]
    public async Task RefreshWorkspaceCommand_ShouldRemoveClosedProjectTabWhenProjectDeleted()
    {
        var projectService = new FakeProjectWorkspaceService();
        projectService.SeedProjects(
        [
            new("project-1", "订单项目", "订单接口", true)
        ]);
        var viewModel = CreateViewModel(projectService);
        await viewModel.InitializeAsync();
        var project = viewModel.ProjectPanel.Projects.Single();
        await viewModel.OpenProjectWorkspaceCommand.ExecuteAsync(project);

        projectService.RemoveProject(project.Id);
        await viewModel.RefreshWorkspaceCommand.ExecuteAsync(null);

        Assert.Empty(viewModel.ProjectTabs);
        Assert.Null(viewModel.ActiveProjectTab);
        Assert.True(viewModel.IsHomeTabActive);
        Assert.Equal(viewModel.BrowserStatusText, viewModel.StatusMessage);
    }

    [Fact]
    public async Task RefreshWorkspaceCommand_ShouldDisposeRemovedProjectTab()
    {
        var projectService = new FakeProjectWorkspaceService();
        projectService.SeedProjects(
        [
            new("project-1", "订单项目", "订单接口", true)
        ]);
        var viewModel = CreateViewModel(projectService);
        await viewModel.InitializeAsync();
        var project = viewModel.ProjectPanel.Projects.Single();

        await viewModel.OpenProjectWorkspaceCommand.ExecuteAsync(project);
        var removedTab = Assert.Single(viewModel.ProjectTabs);

        projectService.RemoveProject(project.Id);
        await viewModel.RefreshWorkspaceCommand.ExecuteAsync(null);

        Assert.Empty(removedTab.WorkspaceTabs);
        Assert.Null(removedTab.ActiveWorkspaceTab);
    }

    [Fact]
    public async Task CloseProjectTabCommand_ShouldDisposeClosedTabAndKeepRemainingTabActive()
    {
        var projectService = new FakeProjectWorkspaceService();
        projectService.SeedProjects(
        [
            new("project-1", "订单项目", "订单接口", true),
            new("project-2", "用户项目", "用户接口", false)
        ]);
        var viewModel = CreateViewModel(projectService);
        await viewModel.InitializeAsync();
        var firstProject = viewModel.ProjectPanel.Projects[0];
        var secondProject = viewModel.ProjectPanel.Projects[1];

        await viewModel.OpenProjectWorkspaceCommand.ExecuteAsync(firstProject);
        var firstTab = Assert.Single(viewModel.ProjectTabs);

        await viewModel.OpenProjectWorkspaceCommand.ExecuteAsync(secondProject);
        var secondTab = Assert.Single(viewModel.ProjectTabs, tab => tab.ProjectId == secondProject.Id);

        viewModel.CloseProjectTabCommand.Execute(secondTab);

        Assert.Single(viewModel.ProjectTabs);
        Assert.Same(firstTab, viewModel.ActiveProjectTab);
        Assert.Empty(secondTab.WorkspaceTabs);
        Assert.Null(secondTab.ActiveWorkspaceTab);
    }

    [Fact]
    public void ToggleNotificationCenterCommand_ShouldMarkNotificationsAsRead()
    {
        var viewModel = CreateViewModel();
        viewModel.ShellPanels.Notifications.Add(new NotificationItemViewModel
        {
            Title = "发现新版本",
            Message = "发现 ApixPress 1.2.0.0。",
            RelativeTimeText = "刚刚",
            IsUnread = true
        });

        Assert.True(viewModel.ShellPanels.HasUnreadNotifications);
        Assert.True(viewModel.ShellPanels.HasNotifications);
        Assert.False(viewModel.ShellPanels.ShowNotificationEmptyState);

        viewModel.ShellPanels.ToggleNotificationCenterCommand.Execute(null);

        Assert.True(viewModel.ShellPanels.IsNotificationCenterOpen);
        Assert.False(viewModel.ShellPanels.IsSettingsDialogOpen);
        Assert.False(viewModel.ShellPanels.HasUnreadNotifications);
        Assert.All(viewModel.ShellPanels.Notifications, item => Assert.False(item.IsUnread));
        Assert.Equal("这里展示近期动态和提醒。", viewModel.StatusMessage);
    }

    [Fact]
    public void OpenSettingsDialogCommand_ShouldCloseNotificationCenterAndResetToGeneralSection()
    {
        var viewModel = CreateViewModel();
        viewModel.SettingsCenter.ShowAboutSettingsCommand.Execute(null);
        viewModel.ShellPanels.ToggleNotificationCenterCommand.Execute(null);

        viewModel.ShellPanels.OpenSettingsDialogCommand.Execute(null);

        Assert.True(viewModel.ShellPanels.IsSettingsDialogOpen);
        Assert.False(viewModel.ShellPanels.IsNotificationCenterOpen);
        Assert.True(viewModel.SettingsCenter.ShowGeneralSettingsSection);
        Assert.False(viewModel.SettingsCenter.ShowAboutSettingsSection);
        Assert.Equal("可在这里调整通用、存储设置和查看版本信息。", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Dispose_ShouldReleaseProjectTabs()
    {
        var projectService = new FakeProjectWorkspaceService();
        projectService.SeedProjects(
        [
            new("project-1", "订单项目", "订单接口", true),
            new("project-2", "用户项目", "用户接口", false)
        ]);
        var viewModel = CreateViewModel(projectService);
        await viewModel.InitializeAsync();

        await viewModel.OpenProjectWorkspaceCommand.ExecuteAsync(viewModel.ProjectPanel.Projects[0]);
        await viewModel.OpenProjectWorkspaceCommand.ExecuteAsync(viewModel.ProjectPanel.Projects[1]);

        var tabs = viewModel.ProjectTabs.ToList();

        viewModel.Dispose();

        Assert.Empty(viewModel.ProjectTabs);
        Assert.Null(viewModel.ActiveProjectTab);
        Assert.All(tabs, tab =>
        {
            Assert.Empty(tab.WorkspaceTabs);
            Assert.Null(tab.ActiveWorkspaceTab);
        });
    }

    [Fact]
    public void Dispose_ShouldBeIdempotent()
    {
        var viewModel = CreateViewModel();

        viewModel.Dispose();
        var exception = Record.Exception(viewModel.Dispose);

        Assert.Null(exception);
        Assert.Empty(viewModel.ProjectTabs);
        Assert.Null(viewModel.ActiveProjectTab);
    }

    private sealed class DeferredEnvironmentVariableService : IEnvironmentVariableService
    {
        private readonly TaskCompletionSource<bool> _loadGate;

        public DeferredEnvironmentVariableService(TaskCompletionSource<bool> loadGate)
        {
            _loadGate = loadGate;
        }

        public async Task<IReadOnlyList<ProjectEnvironmentDto>> GetEnvironmentsAsync(string projectId, CancellationToken cancellationToken)
        {
            await _loadGate.Task.WaitAsync(cancellationToken);
            return
            [
                new ProjectEnvironmentDto
                {
                    Id = "env-1",
                    ProjectId = projectId,
                    Name = "开发",
                    BaseUrl = "https://api.demo.local",
                    IsActive = true,
                    SortOrder = 1
                }
            ];
        }

        public Task<IResultModel<ProjectEnvironmentDto>> SaveEnvironmentAsync(ProjectEnvironmentDto environment, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<ProjectEnvironmentDto>>(ResultModel<ProjectEnvironmentDto>.Success(environment));
        }

        public Task<IResultModel<ProjectEnvironmentDto>> SetActiveEnvironmentAsync(string projectId, string environmentId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<ProjectEnvironmentDto>>(ResultModel<ProjectEnvironmentDto>.Success(new ProjectEnvironmentDto
            {
                Id = environmentId,
                ProjectId = projectId,
                Name = "开发",
                BaseUrl = "https://api.demo.local",
                IsActive = true,
                SortOrder = 1
            }));
        }

        public Task<IResultModel<bool>> DeleteEnvironmentAsync(string projectId, string environmentId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<bool>>(ResultModel<bool>.Success(true));
        }

        public Task<IReadOnlyList<EnvironmentVariableDto>> GetVariablesAsync(string environmentId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<EnvironmentVariableDto>>([]);
        }

        public Task<IResultModel<EnvironmentVariableDto>> SaveVariableAsync(EnvironmentVariableDto variable, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<EnvironmentVariableDto>>(ResultModel<EnvironmentVariableDto>.Success(variable));
        }

        public Task<IResultModel<IReadOnlyList<EnvironmentVariableDto>>> SaveVariablesAsync(
            ProjectEnvironmentDto environment,
            IReadOnlyList<EnvironmentVariableDto> variables,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<IReadOnlyList<EnvironmentVariableDto>>>(
                ResultModel<IReadOnlyList<EnvironmentVariableDto>>.Success(variables));
        }

        public Task<IResultModel<bool>> DeleteVariableAsync(string id, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<bool>>(ResultModel<bool>.Success(true));
        }

        public Task<IReadOnlyDictionary<string, string>> GetActiveDictionaryAsync(string environmentId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
        }
    }
}
