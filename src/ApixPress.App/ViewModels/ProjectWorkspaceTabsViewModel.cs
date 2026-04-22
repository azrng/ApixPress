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

public partial class ProjectWorkspaceTabsViewModel : ViewModelBase, IDisposable
{
    private readonly Action _selectInterfaceManagementSection;
    private readonly Action<string> _setStatusMessage;
    private readonly ObservableCollection<RequestWorkspaceTabViewModel> _visibleWorkspaceTabs = [];
    private bool _isDisposed;

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

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        WorkspaceTabs.CollectionChanged -= OnWorkspaceTabsCollectionChanged;

        foreach (var tab in WorkspaceTabs.ToList())
        {
            DetachWorkspaceTab(tab);
        }

        WorkspaceTabs.Clear();
        _visibleWorkspaceTabs.Clear();
        ActiveWorkspaceTab = null;
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
            CloseWorkspaceTab(tab);
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
            ActivateWorkspaceTab(ActiveWorkspaceTab);
        }

        _setStatusMessage(tabsToRemove.Count == 0 ? "当前没有其它标签页可关闭。" : "已关闭其它标签页。");
        StateChanged?.Invoke();
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
        _setStatusMessage("已关闭全部标签页。");
        StateChanged?.Invoke();
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
            ActivateWorkspaceTab(WorkspaceTabs[nextIndex]);
        }

        _setStatusMessage("工作标签已关闭。");
        StateChanged?.Invoke();
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
        EditorStateChanged?.Invoke();
        StateChanged?.Invoke();
    }

    partial void OnIsWorkspaceTabMenuOpenChanged(bool value)
    {
        StateChanged?.Invoke();
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

        SyncVisibleWorkspaceTabs();
        StateChanged?.Invoke();
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
                SyncVisibleWorkspaceTabs();
            }

            StateChanged?.Invoke();
            return;
        }

        if (e.PropertyName is nameof(RequestWorkspaceTabViewModel.EntryType)
            or nameof(RequestWorkspaceTabViewModel.ShowInTabStrip))
        {
            SyncVisibleWorkspaceTabs();
            StateChanged?.Invoke();
        }

        if (e.PropertyName is nameof(RequestWorkspaceTabViewModel.SelectedMethod)
            or nameof(RequestWorkspaceTabViewModel.RequestUrl)
            or nameof(RequestWorkspaceTabViewModel.InterfaceFolderPath)
            or nameof(RequestWorkspaceTabViewModel.HttpCaseName)
            or nameof(RequestWorkspaceTabViewModel.EntryType)
            or nameof(RequestWorkspaceTabViewModel.ShowInTabStrip)
            or nameof(RequestWorkspaceTabViewModel.HeaderText))
        {
            EditorStateChanged?.Invoke();
        }
    }

    private void OnWorkspaceConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var tab = WorkspaceTabs.FirstOrDefault(item => ReferenceEquals(item.ConfigTab, sender));
        if (tab is null || !ReferenceEquals(tab, ActiveWorkspaceTab))
        {
            return;
        }

        EditorStateChanged?.Invoke();
    }

    private void OnWorkspaceConfigCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var tab = WorkspaceTabs.FirstOrDefault(item =>
            ReferenceEquals(item.ConfigTab.QueryParameters, sender)
            || ReferenceEquals(item.ConfigTab.Headers, sender)
            || ReferenceEquals(item.ConfigTab.FormFields, sender));
        if (tab is null || !ReferenceEquals(tab, ActiveWorkspaceTab))
        {
            return;
        }

        EditorStateChanged?.Invoke();
    }

    private void SyncVisibleWorkspaceTabs()
    {
        _visibleWorkspaceTabs.ReplaceWith(WorkspaceTabs.Where(item => !item.IsLandingTab || item.ShowInTabStrip));
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
        tab.PropertyChanged += OnWorkspaceTabPropertyChanged;
        tab.ConfigTab.PropertyChanged += OnWorkspaceConfigPropertyChanged;
        tab.ConfigTab.QueryParameters.CollectionChanged += OnWorkspaceConfigCollectionChanged;
        tab.ConfigTab.Headers.CollectionChanged += OnWorkspaceConfigCollectionChanged;
        tab.ConfigTab.FormFields.CollectionChanged += OnWorkspaceConfigCollectionChanged;
    }

    private void DetachWorkspaceTab(RequestWorkspaceTabViewModel tab)
    {
        tab.PropertyChanged -= OnWorkspaceTabPropertyChanged;
        tab.ConfigTab.PropertyChanged -= OnWorkspaceConfigPropertyChanged;
        tab.ConfigTab.QueryParameters.CollectionChanged -= OnWorkspaceConfigCollectionChanged;
        tab.ConfigTab.Headers.CollectionChanged -= OnWorkspaceConfigCollectionChanged;
        tab.ConfigTab.FormFields.CollectionChanged -= OnWorkspaceConfigCollectionChanged;
    }
}
