using CommunityToolkit.Mvvm.Input;

namespace ApixPress.App.ViewModels;

public partial class MainWindowViewModel
{
    [RelayCommand]
    private async Task OpenProjectWorkspaceAsync(ProjectWorkspaceItemViewModel? project)
    {
        if (project is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var tab = ProjectTabs.FirstOrDefault(item => string.Equals(item.ProjectId, project.Id, StringComparison.OrdinalIgnoreCase));
            if (tab is null)
            {
                tab = CreateProjectTab(project);
                ProjectTabs.Add(tab);
                ActivateProjectTabCore(tab);
                ProjectPanel.SelectedProject = ProjectPanel.Projects.FirstOrDefault(item =>
                    string.Equals(item.Id, project.Id, StringComparison.OrdinalIgnoreCase));
                StatusMessage = $"正在打开项目：{tab.Project.Name}";
                NotifyShellState();
                await tab.InitializeAsync();
            }
            else
            {
                SyncTabProject(tab, project);
                ActivateProjectTabCore(tab);
                ProjectPanel.SelectedProject = ProjectPanel.Projects.FirstOrDefault(item =>
                    string.Equals(item.Id, project.Id, StringComparison.OrdinalIgnoreCase));
            }

            StatusMessage = $"已打开项目：{tab.Project.Name}";
        }
        finally
        {
            IsBusy = false;
            NotifyShellState();
        }
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
        ProjectTabs.Remove(tab);
        ReleaseProjectTab(tab);

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
}
