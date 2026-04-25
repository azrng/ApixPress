using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels;
using Azrng.Core.Results;

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
        Assert.False(openTask.IsCompleted);

        loadGate.SetResult(true);
        await openTask;

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

        Assert.True(viewModel.ShellPanels.HasUnreadNotifications);

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
        Assert.Equal("可在这里调整通用设置和查看版本信息。", viewModel.StatusMessage);
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
