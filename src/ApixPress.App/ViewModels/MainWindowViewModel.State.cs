using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ApixPress.App.ViewModels;

public partial class MainWindowViewModel
{
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

    private ProjectTabViewModel CreateProjectTab(ProjectWorkspaceItemViewModel project)
    {
        var tab = new ProjectTabViewModel(
            project,
            _requestExecutionService,
            _requestCaseService,
            _requestHistoryService,
            _environmentVariableService,
            _apiWorkspaceService,
            _filePickerService);
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
            OnPropertyChanged(nameof(ConfigTab));
            OnPropertyChanged(nameof(ResponseSection));
            OnPropertyChanged(nameof(EnvironmentPanel));
            OnPropertyChanged(nameof(UseCasesPanel));
            OnPropertyChanged(nameof(HistoryPanel));
            OnPropertyChanged(nameof(RequestHistory));
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
        OnPropertyChanged(nameof(ShowQuickRequestSaveDialog));
        OnPropertyChanged(nameof(ShowProjectImportDialog));
        OnPropertyChanged(nameof(ShowProjectImportOverwriteConfirmDialog));
        OnPropertyChanged(nameof(ShowWorkspaceDeleteConfirmDialog));
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
