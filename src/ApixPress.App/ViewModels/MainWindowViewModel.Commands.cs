using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;

namespace ApixPress.App.ViewModels;

public partial class MainWindowViewModel
{
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
        if (item is null)
        {
            return;
        }

        if (item.HasChildren && !item.CanLoad)
        {
            item.IsExpanded = !item.IsExpanded;
            NotifyShellState();
            return;
        }

        if (ActiveProjectTab is null)
        {
            return;
        }

        ActiveProjectTab.LoadWorkspaceItem(item);
        StatusMessage = ActiveProjectTab.StatusMessage;
        NotifyShellState();
    }

    [RelayCommand]
    private async Task DeleteSavedRequestAsync(ExplorerItemViewModel? item)
    {
        if (ActiveProjectTab is null || item is null)
        {
            return;
        }

        await ActiveProjectTab.DeleteWorkspaceItemAsync(item);
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
}
