using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ApixPress.App.ViewModels;

public partial class MainWindowViewModel
{
    private bool _isNotifyingShellState;
    private bool _shellStateNotifyPending;
    private bool _activeProjectShellStateNotifyPending;

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

        // NotifyActiveProjectTabBindings 只在 ActiveProjectTab 切换时 raise（见 OnActiveProjectTabChanged），
        // ShellState 高频路径（如每键输入）不再全量触发 6 个顶层引用属性重解析。
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

        if (_isNotifyingShellState)
        {
            _shellStateNotifyPending = true;
            return;
        }

        _isNotifyingShellState = true;
        try
        {
            do
            {
                _shellStateNotifyPending = false;
                _activeProjectShellStateNotifyPending = false;

                OnPropertyChanged(nameof(IsHomeTabActive));
                OnPropertyChanged(nameof(HasActiveProjectTab));
                OnPropertyChanged(nameof(HasProjectTabs));
                OnPropertyChanged(nameof(IsProjectBrowserMode));
                OnPropertyChanged(nameof(IsWorkspaceMode));
                OnPropertyChanged(nameof(ShowProjectListEmptyState));
                OnPropertyChanged(nameof(ShowProjectSearchEmptyState));
                RaiseActiveProjectShellState();
                OnPropertyChanged(nameof(BrowserStatusText));
                OnPropertyChanged(nameof(WindowMaximizeGlyph));
            }
            while ((_shellStateNotifyPending || _activeProjectShellStateNotifyPending) && !IsDisposed);
        }
        finally
        {
            _isNotifyingShellState = false;
        }
    }

    private void NotifyActiveProjectShellState()
    {
        if (IsDisposed)
        {
            return;
        }

        if (_isNotifyingShellState)
        {
            _activeProjectShellStateNotifyPending = true;
            return;
        }

        RaiseActiveProjectShellState();
    }

    private void RaiseActiveProjectShellState()
    {
        OnPropertyChanged(nameof(HasEnvironmentContext));
        OnPropertyChanged(nameof(ShowQuickRequestSaveDialog));
        OnPropertyChanged(nameof(ShowRequestCodeDialog));
        OnPropertyChanged(nameof(ShowProjectImportDialog));
        OnPropertyChanged(nameof(ShowProjectImportOverwriteConfirmDialog));
        OnPropertyChanged(nameof(ShowWorkspaceDeleteConfirmDialog));
        OnPropertyChanged(nameof(ShowCreateFolderDialog));
        OnPropertyChanged(nameof(CurrentProjectName));
        OnPropertyChanged(nameof(CurrentProjectSummary));
        OnPropertyChanged(nameof(CurrentEnvironmentLabel));
    }

    private static ObservableCollection<NotificationItemViewModel> CreateNotifications()
    {
        return [];
    }
}
