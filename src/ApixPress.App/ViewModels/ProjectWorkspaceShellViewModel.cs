using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class ProjectWorkspaceShellViewModel : ViewModelBase
{
    private static class Sections
    {
        public const string InterfaceManagement = "interface-management";
        public const string RequestHistory = "request-history";
        public const string ProjectSettings = "project-settings";
    }

    private readonly ProjectTabWorkspaceContext _workspaceContext;
    private readonly ProjectTabHostContext _hostContext;
    private readonly Func<Task> _ensureRequestHistoryLoadedAsync;

    internal ProjectWorkspaceShellViewModel(
        ProjectTabWorkspaceContext workspaceContext,
        ProjectTabHostContext hostContext,
        Func<Task> ensureRequestHistoryLoadedAsync)
    {
        _workspaceContext = workspaceContext;
        _hostContext = hostContext;
        _ensureRequestHistoryLoadedAsync = ensureRequestHistoryLoadedAsync;

        NavigationItems.Add(new ProjectWorkspaceNavItemViewModel(
            Sections.InterfaceManagement,
            "接口管理",
            "M4,5 L20,5 L20,7 L4,7 Z M4,10 L20,10 L20,12 L4,12 Z M4,15 L20,15 L20,17 L4,17 Z",
            ShowInterfaceManagementCommand));
        NavigationItems.Add(new ProjectWorkspaceNavItemViewModel(
            Sections.RequestHistory,
            "请求历史",
            "M12,4 A8,8 0 1 0 20,12 A8,8 0 1 0 12,4 M12,7 L12,12 L15.5,14",
            ShowRequestHistoryCommand));
    }

    public ObservableCollection<ProjectWorkspaceNavItemViewModel> NavigationItems { get; } = [];
    public bool IsInterfaceManagementSection => SelectedSection == Sections.InterfaceManagement;
    public bool IsRequestHistorySection => SelectedSection == Sections.RequestHistory;
    public bool IsProjectSettingsSection => SelectedSection == Sections.ProjectSettings;
    public bool ShowInterfaceManagementLanding => IsInterfaceManagementSection && (_workspaceContext.GetActiveWorkspaceTab()?.IsLandingTab ?? true);
    public bool ShowRequestEditorWorkspace => IsInterfaceManagementSection && _workspaceContext.GetActiveWorkspaceTab() is { IsLandingTab: false };
    public ProjectWorkspaceContentMode CurrentContentMode => ResolveCurrentContentMode();

    [ObservableProperty]
    private string selectedSection = Sections.InterfaceManagement;

    [ObservableProperty]
    private ProjectWorkspaceNavItemViewModel? selectedNavigationItem;

    public void AddProjectSettingsNavigation(ICommand command)
    {
        NavigationItems.Add(new ProjectWorkspaceNavItemViewModel(
            Sections.ProjectSettings,
            "项目设置",
            "M12,8.5 A3.5,3.5 0 1 0 12,15.5 A3.5,3.5 0 1 0 12,8.5 M12,3 L13.2,3.3 L13.8,5 L15.5,5.5 L17,4.7 L18.3,6 L17.5,7.5 L18,9.2 L19.7,9.8 L20,11 L18.3,12.2 L18,13.8 L19.5,15 L18.3,16.3 L16.8,15.5 L15.2,16 L14.5,17.7 L13.3,18 L12,16.7 L10.7,18 L9.5,17.7 L8.8,16 L7.2,15.5 L5.7,16.3 L4.5,15 L6,13.8 L5.7,12.2 L4,11 L4.3,9.8 L6,9.2 L6.5,7.5 L5.7,6 L7,4.7 L8.5,5.5 L10.2,5 L10.8,3.3 Z",
            command));
        SyncNavigationSelection();
    }

    public void SelectInterfaceManagementSection()
    {
        SelectedSection = Sections.InterfaceManagement;
    }

    public void SelectRequestHistorySection()
    {
        SelectedSection = Sections.RequestHistory;
    }

    public void ShowProjectSettingsSection()
    {
        SelectedSection = Sections.ProjectSettings;
    }

    public void NotifyWorkspaceStateChanged()
    {
        OnPropertyChanged(nameof(ShowInterfaceManagementLanding));
        OnPropertyChanged(nameof(ShowRequestEditorWorkspace));
        OnPropertyChanged(nameof(CurrentContentMode));
    }

    [RelayCommand]
    private void ShowInterfaceManagement()
    {
        SelectInterfaceManagementSection();
        _workspaceContext.EnsureLandingWorkspaceTab();
        _hostContext.SetStatusMessage(_workspaceContext.GetActiveWorkspaceTab()?.IsLandingTab == true
            ? "接口管理已就绪，可在中间新建 HTTP 接口或快捷请求。"
            : "接口管理已打开。");
        NotifyWorkspaceStateChanged();
        _hostContext.NotifyShellState();
    }

    [RelayCommand]
    private async Task ShowRequestHistory()
    {
        SelectRequestHistorySection();
        _hostContext.SetStatusMessage("正在载入请求历史...");
        await _ensureRequestHistoryLoadedAsync();
        _hostContext.SetStatusMessage(_workspaceContext.HasHistory() ? "这里展示当前项目的请求历史。" : "当前项目还没有请求历史。");
        _hostContext.NotifyShellState();
    }

    partial void OnSelectedSectionChanged(string value)
    {
        SyncNavigationSelection();
        OnPropertyChanged(nameof(IsInterfaceManagementSection));
        OnPropertyChanged(nameof(IsRequestHistorySection));
        OnPropertyChanged(nameof(IsProjectSettingsSection));
        OnPropertyChanged(nameof(ShowInterfaceManagementLanding));
        OnPropertyChanged(nameof(ShowRequestEditorWorkspace));
        OnPropertyChanged(nameof(CurrentContentMode));
    }

    partial void OnSelectedNavigationItemChanged(ProjectWorkspaceNavItemViewModel? value)
    {
        if (value is null || string.Equals(SelectedSection, value.SectionKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SelectedSection = value.SectionKey;
    }

    private void SyncNavigationSelection()
    {
        var selectedItem = NavigationItems.FirstOrDefault(item =>
            string.Equals(item.SectionKey, SelectedSection, StringComparison.OrdinalIgnoreCase));

        foreach (var navigationItem in NavigationItems)
        {
            navigationItem.IsSelected = ReferenceEquals(navigationItem, selectedItem);
        }

        if (!ReferenceEquals(SelectedNavigationItem, selectedItem))
        {
            SelectedNavigationItem = selectedItem;
        }
    }

    private ProjectWorkspaceContentMode ResolveCurrentContentMode()
    {
        if (IsProjectSettingsSection)
        {
            return ProjectWorkspaceContentMode.ProjectSettings;
        }

        if (IsRequestHistorySection)
        {
            return ProjectWorkspaceContentMode.RequestHistory;
        }

        return _workspaceContext.GetActiveWorkspaceTab() is { IsLandingTab: false }
            ? ProjectWorkspaceContentMode.RequestEditor
            : ProjectWorkspaceContentMode.Landing;
    }
}
