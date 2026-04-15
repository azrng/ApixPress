using CommunityToolkit.Mvvm.Input;

namespace ApixPress.App.ViewModels;

public partial class ProjectTabViewModel
{
    [RelayCommand]
    private void OpenQuickRequestWorkspace()
    {
        SelectedWorkspaceSection = WorkspaceSections.InterfaceManagement;
        var tab = ReuseActiveLandingOrCreateWorkspace();
        tab.ConfigureAsQuickRequest();
        IsWorkspaceTabMenuOpen = false;
        ActivateWorkspaceTabCore(tab);
        StatusMessage = "快捷请求标签已打开。";
        NotifyShellState();
    }

    [RelayCommand]
    private void OpenHttpInterfaceWorkspace()
    {
        SelectedWorkspaceSection = WorkspaceSections.InterfaceManagement;
        var tab = ReuseActiveLandingOrCreateWorkspace();
        tab.ConfigureAsHttpInterface();
        IsWorkspaceTabMenuOpen = false;
        ActivateWorkspaceTabCore(tab);
        StatusMessage = "HTTP 接口标签已打开。";
        NotifyShellState();
    }

    [RelayCommand]
    private void ReturnToInterfaceHome()
    {
        SelectedWorkspaceSection = WorkspaceSections.InterfaceManagement;
        var landingTab = FindLandingWorkspaceTab() ?? CreateWorkspaceTab(activate: false);
        landingTab.ConfigureAsLanding();
        landingTab.ShowInTabStrip = true;
        ActivateWorkspaceTabCore(landingTab);
        IsWorkspaceTabMenuOpen = false;
        StatusMessage = "已返回新建页。";
        NotifyShellState();
    }

    [RelayCommand]
    private void CreateWorkspaceTab()
    {
        SelectedWorkspaceSection = WorkspaceSections.InterfaceManagement;
        var tab = CreateWorkspaceTab(activate: true, showInTabStrip: true);
        tab.ConfigureAsLanding();
        IsWorkspaceTabMenuOpen = false;
        StatusMessage = "已新建一个工作标签。";
        NotifyShellState();
    }

    [RelayCommand]
    private void ToggleWorkspaceTabMenu()
    {
        IsWorkspaceTabMenuOpen = !IsWorkspaceTabMenuOpen;
    }

    [RelayCommand]
    private void CloseCurrentWorkspaceFromMenu()
    {
        IsWorkspaceTabMenuOpen = false;
        CloseWorkspaceTab(ActiveWorkspaceTab);
    }

    [RelayCommand]
    private void CloseOtherWorkspaceTabs()
    {
        IsWorkspaceTabMenuOpen = false;
        if (ActiveWorkspaceTab is null)
        {
            return;
        }

        var tabsToRemove = WorkspaceTabs
            .Where(item => !ReferenceEquals(item, ActiveWorkspaceTab))
            .ToList();
        foreach (var tab in tabsToRemove)
        {
            DetachWorkspaceTab(tab);
            WorkspaceTabs.Remove(tab);
        }

        if (!WorkspaceTabs.Contains(ActiveWorkspaceTab))
        {
            EnsureLandingWorkspaceTab();
        }
        else
        {
            ActivateWorkspaceTabCore(ActiveWorkspaceTab);
        }

        StatusMessage = tabsToRemove.Count == 0 ? "当前没有其它标签页可关闭。" : "已关闭其它标签页。";
        NotifyShellState();
    }

    [RelayCommand]
    private void CloseAllWorkspaceTabs()
    {
        IsWorkspaceTabMenuOpen = false;
        if (WorkspaceTabs.Count == 0)
        {
            return;
        }

        foreach (var tab in WorkspaceTabs.ToList())
        {
            DetachWorkspaceTab(tab);
        }

        WorkspaceTabs.Clear();
        ActiveWorkspaceTab = null;
        EnsureLandingWorkspaceTab();
        StatusMessage = "已关闭全部标签页。";
        NotifyShellState();
    }

    [RelayCommand]
    private void ActivateWorkspaceTab(RequestWorkspaceTabViewModel? tab)
    {
        if (tab is null)
        {
            return;
        }

        IsWorkspaceTabMenuOpen = false;
        ActivateWorkspaceTabCore(tab);
        SelectedWorkspaceSection = WorkspaceSections.InterfaceManagement;
        StatusMessage = tab.IsLandingTab ? "已切换到新建页。" : $"已切换到标签：{tab.HeaderText}";
        NotifyShellState();
    }

    [RelayCommand]
    private void CloseWorkspaceTab(RequestWorkspaceTabViewModel? tab)
    {
        if (tab is null || !WorkspaceTabs.Contains(tab))
        {
            return;
        }

        IsWorkspaceTabMenuOpen = false;
        var removedIndex = WorkspaceTabs.IndexOf(tab);
        DetachWorkspaceTab(tab);
        WorkspaceTabs.Remove(tab);

        if (WorkspaceTabs.Count == 0)
        {
            EnsureLandingWorkspaceTab();
        }
        else if (ReferenceEquals(ActiveWorkspaceTab, tab))
        {
            var nextIndex = Math.Clamp(removedIndex - 1, 0, WorkspaceTabs.Count - 1);
            ActivateWorkspaceTabCore(WorkspaceTabs[nextIndex]);
        }

        StatusMessage = "工作标签已关闭。";
        NotifyShellState();
    }
}
