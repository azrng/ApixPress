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
        OnPropertyChanged(nameof(ShowRequestCodeDialog));
        OnPropertyChanged(nameof(ShowProjectImportDialog));
        OnPropertyChanged(nameof(ShowProjectImportOverwriteConfirmDialog));
        OnPropertyChanged(nameof(ShowWorkspaceDeleteConfirmDialog));
        OnPropertyChanged(nameof(CurrentProjectName));
        OnPropertyChanged(nameof(CurrentProjectSummary));
        OnPropertyChanged(nameof(CurrentEnvironmentLabel));
    }

    private static ObservableCollection<NotificationItemViewModel> CreateNotifications()
    {
        return [];
    }
}
