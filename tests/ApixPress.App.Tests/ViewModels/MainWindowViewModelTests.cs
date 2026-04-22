using ApixPress.App.Models.DTOs;
using ApixPress.App.ViewModels;

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
                EnableUpdateReminder = false
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
        Assert.Equal(viewModel.BrowserStatusText, viewModel.StatusMessage);
        Assert.True(viewModel.IsHomeTabActive);
        Assert.False(viewModel.ShowProjectListEmptyState);
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
    public async Task CheckForUpdatesCommand_ShouldStartUpdaterWhenNewVersionDetected()
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

        Assert.True(viewModel.HasUnreadNotifications);

        viewModel.ToggleNotificationCenterCommand.Execute(null);

        Assert.True(viewModel.IsNotificationCenterOpen);
        Assert.False(viewModel.IsSettingsDialogOpen);
        Assert.False(viewModel.HasUnreadNotifications);
        Assert.All(viewModel.Notifications, item => Assert.False(item.IsUnread));
        Assert.Equal("这里展示近期动态和提醒。", viewModel.StatusMessage);
    }
}
