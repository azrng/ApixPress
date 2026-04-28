using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApixPress.App.Helpers;
using ApixPress.App.Models.DTOs;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class ProjectWorkspaceTabsViewModel : ViewModelBase
{
    private readonly Action _selectInterfaceManagementSection;
    private readonly Action<string> _setStatusMessage;
    private readonly ObservableCollection<RequestWorkspaceTabViewModel> _visibleWorkspaceTabs = [];
    private readonly Dictionary<RequestConfigTabViewModel, RequestWorkspaceTabViewModel> _tabByConfig = [];
    private readonly Dictionary<INotifyCollectionChanged, RequestWorkspaceTabViewModel> _tabByConfigCollection = [];
    private int _notificationSuspendCount;
    private bool _stateChangedPending;
    private bool _editorStateChangedPending;
    private bool _visibleWorkspaceSyncPending;

    public ProjectWorkspaceTabsViewModel(
        Action selectInterfaceManagementSection,
        Action<string> setStatusMessage)
    {
        _selectInterfaceManagementSection = selectInterfaceManagementSection;
        _setStatusMessage = setStatusMessage;

        VisibleWorkspaceTabs = new ReadOnlyObservableCollection<RequestWorkspaceTabViewModel>(_visibleWorkspaceTabs);
        WorkspaceTabs.CollectionChanged += OnWorkspaceTabsCollectionChanged;
    }

    public event Action? StateChanged;
    public event Action? EditorStateChanged;
    public event Action<RequestWorkspaceTabViewModel?, RequestWorkspaceTabViewModel?>? ActiveWorkspaceTabChanged;

    public ObservableCollection<RequestWorkspaceTabViewModel> WorkspaceTabs { get; } = [];
    public ReadOnlyObservableCollection<RequestWorkspaceTabViewModel> VisibleWorkspaceTabs { get; }

    [ObservableProperty]
    private RequestWorkspaceTabViewModel? activeWorkspaceTab;

    [ObservableProperty]
    private bool isWorkspaceTabMenuOpen;

    public RequestWorkspaceTabViewModel ReuseActiveLandingOrCreateWorkspace()
    {
        if (ActiveWorkspaceTab?.IsLandingTab == true)
        {
            return ActiveWorkspaceTab;
        }

        return CreateWorkspaceTab(activate: false);
    }

    public RequestWorkspaceTabViewModel ResolveTabForWorkspaceNavigation(RequestCaseDto source)
    {
        var existingTab = FindWorkspaceTabForSource(source);
        if (existingTab is not null)
        {
            return existingTab;
        }

        if (ActiveWorkspaceTab?.IsLandingTab == true || ActiveWorkspaceTab?.CanReuseForWorkspaceNavigation == true)
        {
            return ActiveWorkspaceTab;
        }

        return CreateWorkspaceTab(activate: false);
    }

    public RequestWorkspaceTabViewModel CreateWorkspaceTab(bool activate, bool showInTabStrip = true)
    {
        var tab = new RequestWorkspaceTabViewModel();
        tab.ConfigureAsLanding();
        tab.ShowInTabStrip = showInTabStrip;
        AttachWorkspaceTab(tab);
        WorkspaceTabs.Add(tab);
        if (activate)
        {
            ActivateWorkspaceTab(tab);
        }

        return tab;
    }

    public void EnsureLandingWorkspaceTab()
    {
        if (WorkspaceTabs.Count == 0)
        {
            var tab = CreateWorkspaceTab(activate: false, showInTabStrip: false);
            tab.ConfigureAsLanding();
            tab.ShowInTabStrip = false;
            ActivateWorkspaceTab(tab);
            return;
        }

        if (ActiveWorkspaceTab is null)
        {
            ActivateWorkspaceTab(WorkspaceTabs[0]);
        }
    }

    public void ResetToLanding()
    {
        IsWorkspaceTabMenuOpen = false;
        foreach (var tab in WorkspaceTabs.ToList())
        {
            DetachWorkspaceTab(tab);
            WorkspaceTabs.Remove(tab);
        }

        ActiveWorkspaceTab = null;
        EnsureLandingWorkspaceTab();
        StateChanged?.Invoke();
    }

    public RequestWorkspaceTabViewModel? FindFirstQuickRequestTab()
    {
        return WorkspaceTabs.FirstOrDefault(item => item.IsQuickRequestTab);
    }

    public RequestWorkspaceTabViewModel? FindWorkspaceTabForSource(RequestCaseDto source)
    {
        return WorkspaceTabs.FirstOrDefault(item =>
            string.Equals(source.EntryType, ProjectTabRequestEntryTypes.QuickRequest, StringComparison.OrdinalIgnoreCase)
                ? string.Equals(item.EditingQuickRequestId, source.Id, StringComparison.OrdinalIgnoreCase)
                : string.Equals(source.EntryType, ProjectTabRequestEntryTypes.HttpInterface, StringComparison.OrdinalIgnoreCase)
                    ? string.Equals(item.EditingInterfaceId, source.Id, StringComparison.OrdinalIgnoreCase)
                    : string.Equals(item.EditingCaseId, source.Id, StringComparison.OrdinalIgnoreCase));
    }

    public void ActivateWorkspaceTab(RequestWorkspaceTabViewModel tab)
    {
        ActiveWorkspaceTab = tab;
    }

    public void RunWithNotificationsSuspended(Action action)
    {
        _notificationSuspendCount++;
        try
        {
            action();
        }
        finally
        {
            _notificationSuspendCount--;
            FlushDeferredNotifications();
        }
    }

    protected override void DisposeManaged()
    {
        WorkspaceTabs.CollectionChanged -= OnWorkspaceTabsCollectionChanged;

        foreach (var tab in WorkspaceTabs.ToList())
        {
            DetachWorkspaceTab(tab);
        }

        WorkspaceTabs.Clear();
        _visibleWorkspaceTabs.Clear();
        ActiveWorkspaceTab = null;
        StateChanged = null;
        EditorStateChanged = null;
        ActiveWorkspaceTabChanged = null;
    }

    public void CloseTabsForDeletedCases(IReadOnlyCollection<RequestCaseDto> deletedCases)
    {
        var deletedIds = deletedCases
            .Select(item => item.Id)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (deletedIds.Count == 0)
        {
            return;
        }

        var tabsToClose = WorkspaceTabs
            .Where(tab => deletedIds.Contains(tab.EditingQuickRequestId)
                || deletedIds.Contains(tab.EditingInterfaceId)
                || deletedIds.Contains(tab.EditingCaseId))
            .ToList();
        foreach (var tab in tabsToClose)
        {
            CloseWorkspaceTab(tab, respectPin: false);
        }
    }

    [RelayCommand]
    private void OpenQuickRequestWorkspace()
    {
        _selectInterfaceManagementSection();
        var tab = ReuseActiveLandingOrCreateWorkspace();
        tab.ConfigureAsQuickRequest();
        IsWorkspaceTabMenuOpen = false;
        ActivateWorkspaceTab(tab);
        _setStatusMessage("快捷请求标签已打开。");
        StateChanged?.Invoke();
    }

    [RelayCommand]
    private void OpenHttpInterfaceWorkspace()
    {
        _selectInterfaceManagementSection();
        var tab = ReuseActiveLandingOrCreateWorkspace();
        tab.ConfigureAsHttpInterface();
        IsWorkspaceTabMenuOpen = false;
        ActivateWorkspaceTab(tab);
        _setStatusMessage("HTTP 接口标签已打开。");
        StateChanged?.Invoke();
    }

    [RelayCommand]
    private void ReturnToInterfaceHome()
    {
        _selectInterfaceManagementSection();
        var landingTab = FindLandingWorkspaceTab() ?? CreateWorkspaceTab(activate: false);
        landingTab.ConfigureAsLanding();
        landingTab.ShowInTabStrip = true;
        ActivateWorkspaceTab(landingTab);
        IsWorkspaceTabMenuOpen = false;
        _setStatusMessage("已返回新建页。");
        StateChanged?.Invoke();
    }

    [RelayCommand]
    private void CreateWorkspaceTab()
    {
        _selectInterfaceManagementSection();
        var tab = CreateWorkspaceTab(activate: true, showInTabStrip: true);
        tab.ConfigureAsLanding();
        IsWorkspaceTabMenuOpen = false;
        _setStatusMessage("已新建一个工作标签。");
        StateChanged?.Invoke();
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
        CloseWorkspaceTab(ActiveWorkspaceTab, respectPin: true);
    }

    [RelayCommand]
    private void CloseOtherWorkspaceTabs()
    {
        IsWorkspaceTabMenuOpen = false;
        CloseOtherWorkspaceTabs(ActiveWorkspaceTab);
    }

    [RelayCommand]
    private void CloseAllWorkspaceTabs()
    {
        IsWorkspaceTabMenuOpen = false;
        if (WorkspaceTabs.Count == 0)
        {
            return;
        }

        var tabsToRemove = WorkspaceTabs
            .Where(item => !item.IsPinned)
            .ToList();
        if (tabsToRemove.Count == 0)
        {
            _setStatusMessage("当前没有可关闭的非固定标签页。");
            StateChanged?.Invoke();
            return;
        }

        foreach (var tab in tabsToRemove)
        {
            DetachWorkspaceTab(tab);
            WorkspaceTabs.Remove(tab);
        }

        if (ActiveWorkspaceTab is null || !WorkspaceTabs.Contains(ActiveWorkspaceTab))
        {
            ActiveWorkspaceTab = null;
            EnsureLandingWorkspaceTab();
        }

        _setStatusMessage(WorkspaceTabs.Any(item => item.IsPinned) ? "已关闭全部非固定标签页。" : "已关闭全部标签页。");
        StateChanged?.Invoke();
    }

    [RelayCommand]
    private void CloseWorkspaceTab(RequestWorkspaceTabViewModel? tab)
    {
        CloseWorkspaceTab(tab, respectPin: true);
    }

    private bool CloseWorkspaceTab(RequestWorkspaceTabViewModel? tab, bool respectPin)
    {
        if (tab is null || !WorkspaceTabs.Contains(tab))
        {
            return false;
        }

        if (respectPin && tab.IsPinned)
        {
            IsWorkspaceTabMenuOpen = false;
            _setStatusMessage("固定标签页请先取消固定后再关闭。");
            StateChanged?.Invoke();
            return false;
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
            ActivateWorkspaceTab(WorkspaceTabs[nextIndex]);
        }

        _setStatusMessage("工作标签已关闭。");
        StateChanged?.Invoke();
        return true;
    }

    partial void OnActiveWorkspaceTabChanged(RequestWorkspaceTabViewModel? oldValue, RequestWorkspaceTabViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.IsActive = false;
        }

        if (newValue is not null)
        {
            newValue.IsActive = true;
            _selectInterfaceManagementSection();
            _setStatusMessage(newValue.IsLandingTab
                ? "已切换到新建页。"
                : $"已切换到标签：{newValue.HeaderText}");
        }

        ActiveWorkspaceTabChanged?.Invoke(oldValue, newValue);
        RequestNotifications(stateChanged: true, editorStateChanged: true);
    }

    partial void OnIsWorkspaceTabMenuOpenChanged(bool value)
    {
        RequestNotifications(stateChanged: true);
    }

    private void OnWorkspaceTabsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<RequestWorkspaceTabViewModel>())
            {
                item.IsActive = ReferenceEquals(item, ActiveWorkspaceTab);
            }
        }

        RequestNotifications(syncVisibleWorkspaceTabs: true, stateChanged: true);
    }

    private void OnWorkspaceTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not RequestWorkspaceTabViewModel tab)
        {
            return;
        }

        if (!ReferenceEquals(tab, ActiveWorkspaceTab))
        {
            if (e.PropertyName is nameof(RequestWorkspaceTabViewModel.EntryType)
                or nameof(RequestWorkspaceTabViewModel.ShowInTabStrip))
            {
                RequestNotifications(syncVisibleWorkspaceTabs: true, stateChanged: true);
                return;
            }

            RequestNotifications(stateChanged: true);
            return;
        }

        if (e.PropertyName is nameof(RequestWorkspaceTabViewModel.EntryType)
            or nameof(RequestWorkspaceTabViewModel.ShowInTabStrip))
        {
            RequestNotifications(syncVisibleWorkspaceTabs: true, stateChanged: true, editorStateChanged: true);
            return;
        }

        if (e.PropertyName is nameof(RequestWorkspaceTabViewModel.SelectedMethod)
            or nameof(RequestWorkspaceTabViewModel.RequestUrl)
            or nameof(RequestWorkspaceTabViewModel.InterfaceFolderPath)
            or nameof(RequestWorkspaceTabViewModel.HttpCaseName)
            or nameof(RequestWorkspaceTabViewModel.EntryType)
            or nameof(RequestWorkspaceTabViewModel.ShowInTabStrip)
            or nameof(RequestWorkspaceTabViewModel.HeaderText))
        {
            RequestNotifications(stateChanged: true, editorStateChanged: true);
            return;
        }

        RequestNotifications(stateChanged: true);
    }

    private void OnWorkspaceConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not RequestConfigTabViewModel configTab
            || !_tabByConfig.TryGetValue(configTab, out var tab)
            || !ReferenceEquals(tab, ActiveWorkspaceTab))
        {
            return;
        }

        RequestNotifications(stateChanged: true, editorStateChanged: true);
    }

    private void OnWorkspaceConfigCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (sender is not INotifyCollectionChanged collection
            || !_tabByConfigCollection.TryGetValue(collection, out var tab)
            || !ReferenceEquals(tab, ActiveWorkspaceTab))
        {
            return;
        }

        RequestNotifications(stateChanged: true, editorStateChanged: true);
    }

    private void SyncVisibleWorkspaceTabs()
    {
        _visibleWorkspaceTabs.ReplaceWith(WorkspaceTabs.Where(item => !item.IsLandingTab || item.ShowInTabStrip));
    }

    private void CloseOtherWorkspaceTabs(RequestWorkspaceTabViewModel? keepTab)
    {
        if (keepTab is null)
        {
            return;
        }

        var tabsToRemove = WorkspaceTabs
            .Where(item => !ReferenceEquals(item, keepTab) && !item.IsPinned)
            .ToList();
        foreach (var tab in tabsToRemove)
        {
            DetachWorkspaceTab(tab);
            WorkspaceTabs.Remove(tab);
        }

        if (WorkspaceTabs.Contains(keepTab))
        {
            ActivateWorkspaceTab(keepTab);
        }
        else
        {
            EnsureLandingWorkspaceTab();
        }

        _setStatusMessage(tabsToRemove.Count == 0 ? "当前没有其它非固定标签页可关闭。" : "已关闭其它非固定标签页。");
        StateChanged?.Invoke();
    }

    private RequestWorkspaceTabViewModel? FindLandingWorkspaceTab()
    {
        return WorkspaceTabs
            .Where(item => item.IsLandingTab)
            .OrderByDescending(item => item.ShowInTabStrip)
            .FirstOrDefault();
    }

    private void AttachWorkspaceTab(RequestWorkspaceTabViewModel tab)
    {
        tab.CloseRequested = item => CloseWorkspaceTab(item, respectPin: true);
        tab.CloseOtherRequested = CloseOtherWorkspaceTabs;
        tab.CloseAllRequested = CloseAllWorkspaceTabs;
        tab.PropertyChanged += OnWorkspaceTabPropertyChanged;
        tab.ConfigTab.PropertyChanged += OnWorkspaceConfigPropertyChanged;
        tab.ConfigTab.QueryParameters.CollectionChanged += OnWorkspaceConfigCollectionChanged;
        tab.ConfigTab.Headers.CollectionChanged += OnWorkspaceConfigCollectionChanged;
        tab.ConfigTab.FormFields.CollectionChanged += OnWorkspaceConfigCollectionChanged;
        _tabByConfig[tab.ConfigTab] = tab;
        _tabByConfigCollection[tab.ConfigTab.QueryParameters] = tab;
        _tabByConfigCollection[tab.ConfigTab.Headers] = tab;
        _tabByConfigCollection[tab.ConfigTab.FormFields] = tab;
    }

    private void DetachWorkspaceTab(RequestWorkspaceTabViewModel tab)
    {
        tab.CloseRequested = null;
        tab.CloseOtherRequested = null;
        tab.CloseAllRequested = null;
        tab.PropertyChanged -= OnWorkspaceTabPropertyChanged;
        tab.ConfigTab.PropertyChanged -= OnWorkspaceConfigPropertyChanged;
        tab.ConfigTab.QueryParameters.CollectionChanged -= OnWorkspaceConfigCollectionChanged;
        tab.ConfigTab.Headers.CollectionChanged -= OnWorkspaceConfigCollectionChanged;
        tab.ConfigTab.FormFields.CollectionChanged -= OnWorkspaceConfigCollectionChanged;
        _tabByConfig.Remove(tab.ConfigTab);
        _tabByConfigCollection.Remove(tab.ConfigTab.QueryParameters);
        _tabByConfigCollection.Remove(tab.ConfigTab.Headers);
        _tabByConfigCollection.Remove(tab.ConfigTab.FormFields);
        tab.Dispose();
    }

    private void RequestNotifications(
        bool syncVisibleWorkspaceTabs = false,
        bool stateChanged = false,
        bool editorStateChanged = false)
    {
        if (syncVisibleWorkspaceTabs)
        {
            _visibleWorkspaceSyncPending = true;
        }

        if (stateChanged)
        {
            _stateChangedPending = true;
        }

        if (editorStateChanged)
        {
            _editorStateChangedPending = true;
        }

        if (_notificationSuspendCount > 0)
        {
            return;
        }

        FlushDeferredNotifications();
    }

    private void FlushDeferredNotifications()
    {
        if (_notificationSuspendCount > 0)
        {
            return;
        }

        if (_visibleWorkspaceSyncPending)
        {
            _visibleWorkspaceSyncPending = false;
            SyncVisibleWorkspaceTabs();
        }

        if (_editorStateChangedPending)
        {
            _editorStateChangedPending = false;
            EditorStateChanged?.Invoke();
        }

        if (_stateChangedPending)
        {
            _stateChangedPending = false;
            StateChanged?.Invoke();
        }
    }
}
