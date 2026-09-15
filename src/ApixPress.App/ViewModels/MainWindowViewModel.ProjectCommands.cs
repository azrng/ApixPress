using ApixPress.App.Helpers;
using CommunityToolkit.Mvvm.Input;
using Ursa.Controls;

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
                await PerfLog.MeasureAsync($"打开项目·工作区初始化({tab.Project.Name})", () => tab.InitializeAsync());
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
        StatusMessage = BrowserStatusText;
    }

    [RelayCommand]
    private async Task OpenNewTabAsync()
    {
        // 对齐原型：标题栏「＋」在当前项目内打开「新建…」标签；首页态回退到默认项目工作区
        var tab = ActiveProjectTab
            ?? ProjectTabs.FirstOrDefault(item => item.Project.IsDefault)
            ?? ProjectTabs.FirstOrDefault();

        if (tab is null)
        {
            var project = ProjectPanel.Projects.FirstOrDefault(item => item.IsDefault)
                ?? ProjectPanel.Projects.FirstOrDefault();
            if (project is null)
            {
                StatusMessage = "请先创建项目，再新建接口或快捷请求。";
                NotifyShellState();
                return;
            }

            await OpenProjectWorkspaceAsync(project);
            tab = ActiveProjectTab;
        }
        else if (!ReferenceEquals(tab, ActiveProjectTab))
        {
            ActivateProjectTabCore(tab);
        }

        tab?.Workspace.CreateWorkspaceTabCommand.Execute(null);
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

    /// <summary>打开「新建项目」弹窗；弹窗关闭后按结果恢复状态栏文案（成功文案由 OnProjectCreated 设置）。</summary>
    [RelayCommand]
    private async Task OpenCreateProjectDialogAsync()
    {
        StatusMessage = "填写项目名称和备注信息后即可创建项目。";
        NotifyShellState();
        var result = await _dialogService.ShowDialogAsync<Views.Controls.CreateProjectDialogView>(this, "新建项目");
        if (result != DialogResult.OK)
        {
            StatusMessage = IsHomeTabActive ? BrowserStatusText : ActiveProjectTab?.StatusMessage ?? BrowserStatusText;
        }

        NotifyShellState();
    }

    [RelayCommand]
    private void CloseCreateProjectDialog()
    {
        RequestClose?.Invoke(this, DialogResult.Cancel);
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
