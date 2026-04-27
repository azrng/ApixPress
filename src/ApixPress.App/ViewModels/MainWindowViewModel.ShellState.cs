using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ApixPress.App.ViewModels;

public partial class MainWindowViewModel
{
    partial void OnActiveProjectTabChanged(ProjectTabViewModel? oldValue, ProjectTabViewModel? newValue)
    {
        if (IsDisposed)
        {
            return;
        }

        if (ReferenceEquals(oldValue, newValue))
        {
            return;
        }

        if (oldValue is not null)
        {
            oldValue.IsActive = false;
        }

        if (newValue is not null)
        {
            newValue.IsActive = true;
        }

        NotifyActiveProjectTabBindings();
        NotifyShellState();
    }

    private void OnProjectCreated()
    {
        if (IsDisposed)
        {
            return;
        }

        IsCreateProjectDialogOpen = false;
        StatusMessage = "项目已创建，可在首页卡片中继续打开为新标签页。";
        NotifyShellState();
    }

    private void OnProjectTabShellStateChanged(ProjectTabViewModel tab)
    {
        if (IsDisposed)
        {
            return;
        }

        if (!ReferenceEquals(tab, ActiveProjectTab))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(tab.StatusMessage))
        {
            StatusMessage = tab.StatusMessage;
        }

        NotifyActiveProjectTabBindings();
        NotifyActiveProjectShellState();
    }

    private void OnProjectPanelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        if (e.PropertyName is nameof(ProjectPanelViewModel.SelectedProject)
            or nameof(ProjectPanelViewModel.HasProjects)
            or nameof(ProjectPanelViewModel.HasAnyProjects)
            or nameof(ProjectPanelViewModel.SearchText)
            or nameof(ProjectPanelViewModel.HasSelectedProject))
        {
            NotifyShellState();
        }
    }

    private void NotifyShellState()
    {
        if (IsDisposed)
        {
            return;
        }

        OnPropertyChanged(nameof(IsHomeTabActive));
        OnPropertyChanged(nameof(HasActiveProjectTab));
        OnPropertyChanged(nameof(HasProjectTabs));
        OnPropertyChanged(nameof(IsProjectBrowserMode));
        OnPropertyChanged(nameof(IsWorkspaceMode));
        OnPropertyChanged(nameof(ShowProjectListEmptyState));
        OnPropertyChanged(nameof(ShowProjectSearchEmptyState));
        NotifyActiveProjectShellState();
        OnPropertyChanged(nameof(BrowserStatusText));
        OnPropertyChanged(nameof(WindowMaximizeGlyph));
    }

    private void NotifyActiveProjectShellState()
    {
        if (IsDisposed)
        {
            return;
        }

        OnPropertyChanged(nameof(HasEnvironmentContext));
        OnPropertyChanged(nameof(ShowQuickRequestSaveDialog));
        OnPropertyChanged(nameof(ShowProjectImportDialog));
        OnPropertyChanged(nameof(ShowProjectImportOverwriteConfirmDialog));
        OnPropertyChanged(nameof(ShowWorkspaceDeleteConfirmDialog));
        OnPropertyChanged(nameof(CurrentProjectName));
        OnPropertyChanged(nameof(CurrentProjectSummary));
        OnPropertyChanged(nameof(CurrentEnvironmentLabel));
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
