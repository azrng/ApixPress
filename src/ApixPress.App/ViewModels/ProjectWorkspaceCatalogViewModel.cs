using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels.Base;
using Azrng.Core.Results;

namespace ApixPress.App.ViewModels;

public partial class ProjectWorkspaceCatalogViewModel : ViewModelBase, IDisposable
{
    private const string ImportedEndpointKeyPrefix = "swagger-import:";

    private readonly string _projectId;
    private readonly IRequestCaseService _requestCaseService;
    private readonly IApiWorkspaceService _apiWorkspaceService;
    private readonly UseCasesPanelViewModel _useCasesPanel;
    private readonly ProjectWorkspaceTabsViewModel _workspace;
    private readonly Action _showInterfaceManagementSection;
    private readonly Action<string> _setStatusMessage;
    private readonly Action _notifyShellState;
    private readonly Func<Task> _reloadImportedDocumentsAsync;
    private int _navigationRebuildSuspendCount;
    private bool _interfaceNavigationRebuildPending;
    private bool _quickRequestNavigationRebuildPending;
    private bool _isDisposed;

    public ProjectWorkspaceCatalogViewModel(
        string projectId,
        IRequestCaseService requestCaseService,
        IApiWorkspaceService apiWorkspaceService,
        UseCasesPanelViewModel useCasesPanel,
        ProjectWorkspaceTabsViewModel workspace,
        Action showInterfaceManagementSection,
        Action<string> setStatusMessage,
        Action notifyShellState,
        Func<Task> reloadImportedDocumentsAsync)
    {
        _projectId = projectId;
        _requestCaseService = requestCaseService;
        _apiWorkspaceService = apiWorkspaceService;
        _useCasesPanel = useCasesPanel;
        _workspace = workspace;
        _showInterfaceManagementSection = showInterfaceManagementSection;
        _setStatusMessage = setStatusMessage;
        _notifyShellState = notifyShellState;
        _reloadImportedDocumentsAsync = reloadImportedDocumentsAsync;

        _useCasesPanel.RequestCases.CollectionChanged += OnSavedRequestsCollectionChanged;
    }

    public ObservableCollection<ExplorerItemViewModel> InterfaceTreeItems { get; } = [];
    public ObservableCollection<ExplorerItemViewModel> QuickRequestTreeItems { get; } = [];
    public ObservableCollection<RequestCaseItemViewModel> SavedRequests => _useCasesPanel.RequestCases;
    public IReadOnlyList<ExplorerItemViewModel> InterfaceCatalogItems => InterfaceTreeItems.FirstOrDefault()?.Children ?? [];
    public bool HasQuickRequestEntries => SavedRequests.Any(item => string.Equals(item.SourceCase.EntryType, ProjectTabRequestEntryTypes.QuickRequest, StringComparison.OrdinalIgnoreCase));
    public bool HasInterfaceEntries => SavedRequests.Any(item => string.Equals(item.SourceCase.EntryType, ProjectTabRequestEntryTypes.HttpInterface, StringComparison.OrdinalIgnoreCase));
    public bool ShowInterfaceEntriesEmptyState => !HasInterfaceEntries;
    public bool ShowQuickRequestEntriesEmptyState => !HasQuickRequestEntries;
    public bool ShowSavedRequestsEmptyState => !HasQuickRequestEntries && !HasInterfaceEntries;
    public string InterfaceSectionHint => HasInterfaceEntries ? "默认模块 / 接口" : "默认模块下还没有保存的 HTTP 接口";
    public string QuickRequestSectionHint => HasQuickRequestEntries ? "保存到左侧快捷请求目录" : "左侧快捷请求目录还是空的";
    public bool HasPendingDeleteTarget => PendingDeleteWorkspaceItem is not null;
    public string PendingDeleteTitle => PendingDeleteWorkspaceItem?.Title ?? string.Empty;
    public string PendingDeleteDescription
    {
        get
        {
            if (PendingDeleteWorkspaceItem is null)
            {
                return string.Empty;
            }

            var count = ProjectWorkspaceTreeBuilder.CollectDeletableSourceCases(PendingDeleteWorkspaceItem)
                .Select(item => item.Id)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            return count <= 1
                ? "删除后无法恢复，请确认当前已不再需要。"
                : $"该节点下共 {count} 项内容会被一起删除，删除后无法恢复。";
        }
    }

    [ObservableProperty]
    private bool isInterfaceCatalogExpanded = true;

    [ObservableProperty]
    private bool isDataModelCatalogExpanded;

    [ObservableProperty]
    private bool isComponentLibraryCatalogExpanded;

    [ObservableProperty]
    private bool isQuickRequestCatalogExpanded = true;

    [ObservableProperty]
    private bool isDeleteConfirmDialogOpen;

    [ObservableProperty]
    private ExplorerItemViewModel? pendingDeleteWorkspaceItem;

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _useCasesPanel.RequestCases.CollectionChanged -= OnSavedRequestsCollectionChanged;
    }

    public void LoadWorkspaceItem(ExplorerItemViewModel? item)
    {
        if (item is null || item.SourceCase is null)
        {
            return;
        }

        var source = item.SourceCase;
        var parentInterface = string.Equals(source.EntryType, ProjectTabRequestEntryTypes.HttpCase, StringComparison.OrdinalIgnoreCase)
            ? FindRequestById(source.ParentId)
            : null;

        _showInterfaceManagementSection();
        var targetTab = _workspace.FindWorkspaceTabForSource(source) ?? _workspace.ReuseActiveLandingOrCreateWorkspace();
        targetTab.ApplySavedRequest(source, parentInterface);

        if (string.Equals(source.EntryType, ProjectTabRequestEntryTypes.HttpInterface, StringComparison.OrdinalIgnoreCase))
        {
            targetTab.HttpCaseName = ResolveLatestCaseName(source.Id);
        }

        _workspace.ActivateWorkspaceTab(targetTab);
        _setStatusMessage(source.EntryType switch
        {
            ProjectTabRequestEntryTypes.HttpInterface => $"已加载 HTTP 接口：{source.Name}",
            ProjectTabRequestEntryTypes.HttpCase => $"已加载接口用例：{source.Name}",
            _ => $"已加载快捷请求：{source.Name}"
        });
        _notifyShellState();
    }

    public async Task DeleteWorkspaceItemAsync(ExplorerItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        var targets = ProjectWorkspaceTreeBuilder.CollectDeletableSourceCases(item)
            .DistinctBy(source => source.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (targets.Count == 0)
        {
            _setStatusMessage("当前节点没有可删除的内容。");
            _notifyShellState();
            return;
        }

        var importedInterfaces = targets
            .Where(source => string.Equals(source.EntryType, ProjectTabRequestEntryTypes.HttpInterface, StringComparison.OrdinalIgnoreCase))
            .Where(IsImportedInterface)
            .ToList();
        if (importedInterfaces.Count > 0)
        {
            await _apiWorkspaceService.DeleteImportedHttpInterfacesAsync(_projectId, importedInterfaces, CancellationToken.None);
        }

        await _requestCaseService.DeleteRangeAsync(
            _projectId,
            targets
                .OrderBy(source => ProjectWorkspaceTreeBuilder.ResolveDeletePriority(source.EntryType))
                .ThenBy(source => source.Name, StringComparer.OrdinalIgnoreCase)
                .Select(source => source.Id)
                .ToList(),
            CancellationToken.None);

        _workspace.CloseTabsForDeletedCases(targets);
        if (importedInterfaces.Count > 0)
        {
            await _reloadImportedDocumentsAsync();
        }
        else
        {
            RemoveCases(targets.Select(target => target.Id));
        }

        _setStatusMessage(targets.Count == 1
            ? $"已删除：{targets[0].Name}"
            : $"已删除 {targets.Count} 项内容。");
        _notifyShellState();
    }

    public void UpsertCaseItem(RequestCaseDto requestCase)
    {
        RunWithNavigationRebuildSuppressed(() => _useCasesPanel.UpsertCaseItem(requestCase));
    }

    public void RemoveCases(IEnumerable<string> ids)
    {
        RunWithNavigationRebuildSuppressed(() => _useCasesPanel.RemoveCases(ids));
    }

    public async Task ReloadSavedRequestsAsync()
    {
        await RunWithNavigationRebuildSuppressedAsync(() => _useCasesPanel.LoadCasesAsync());
    }

    public async Task SyncImportedInterfacesAsync(IReadOnlyList<ApiEndpointDto> endpoints)
    {
        await _requestCaseService.SyncImportedHttpInterfacesAsync(_projectId, endpoints, CancellationToken.None);
        await ReloadSavedRequestsAsync();
    }

    [RelayCommand]
    private void RequestDeleteWorkspaceTreeItem(ExplorerItemViewModel? item)
    {
        if (item is null || !item.CanDelete)
        {
            return;
        }

        PendingDeleteWorkspaceItem = item;
        IsDeleteConfirmDialogOpen = true;
        _setStatusMessage($"准备删除：{item.Title}");
        _notifyShellState();
    }

    [RelayCommand]
    private void CancelDelete()
    {
        PendingDeleteWorkspaceItem = null;
        IsDeleteConfirmDialogOpen = false;
        _setStatusMessage("已取消删除。");
        _notifyShellState();
    }

    [RelayCommand]
    private async Task ConfirmDeleteAsync()
    {
        if (PendingDeleteWorkspaceItem is null)
        {
            IsDeleteConfirmDialogOpen = false;
            _notifyShellState();
            return;
        }

        var item = PendingDeleteWorkspaceItem;
        PendingDeleteWorkspaceItem = null;
        IsDeleteConfirmDialogOpen = false;
        await DeleteWorkspaceItemAsync(item);
    }

    partial void OnPendingDeleteWorkspaceItemChanged(ExplorerItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasPendingDeleteTarget));
        OnPropertyChanged(nameof(PendingDeleteTitle));
        OnPropertyChanged(nameof(PendingDeleteDescription));
    }

    private void OnSavedRequestsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var (rebuildInterfaceNavigation, rebuildQuickRequestNavigation) = ResolveWorkspaceNavigationRebuildScope(e);
        RequestWorkspaceNavigationRebuild(rebuildInterfaceNavigation, rebuildQuickRequestNavigation);
        NotifySavedRequestStateChanged();
        _notifyShellState();
    }

    private void NotifySavedRequestStateChanged()
    {
        OnPropertyChanged(nameof(HasQuickRequestEntries));
        OnPropertyChanged(nameof(HasInterfaceEntries));
        OnPropertyChanged(nameof(ShowInterfaceEntriesEmptyState));
        OnPropertyChanged(nameof(ShowQuickRequestEntriesEmptyState));
        OnPropertyChanged(nameof(ShowSavedRequestsEmptyState));
        OnPropertyChanged(nameof(InterfaceSectionHint));
        OnPropertyChanged(nameof(QuickRequestSectionHint));
    }

    private void RebuildInterfaceNavigation()
    {
        SynchronizeExplorerItems(
            InterfaceTreeItems,
            [ProjectWorkspaceTreeBuilder.BuildInterfaceRoot(SavedRequests, RequestDeleteWorkspaceTreeItemCommand)]);
        OnPropertyChanged(nameof(InterfaceCatalogItems));
    }

    private void RebuildQuickRequestNavigation()
    {
        SynchronizeExplorerItems(
            QuickRequestTreeItems,
            ProjectWorkspaceTreeBuilder.BuildQuickRequests(SavedRequests, RequestDeleteWorkspaceTreeItemCommand));
    }

    private void RequestWorkspaceNavigationRebuild(bool rebuildInterfaceNavigation = true, bool rebuildQuickRequestNavigation = true)
    {
        if (!rebuildInterfaceNavigation && !rebuildQuickRequestNavigation)
        {
            return;
        }

        if (_navigationRebuildSuspendCount > 0)
        {
            _interfaceNavigationRebuildPending |= rebuildInterfaceNavigation;
            _quickRequestNavigationRebuildPending |= rebuildQuickRequestNavigation;
            return;
        }

        if (rebuildInterfaceNavigation)
        {
            RebuildInterfaceNavigation();
        }

        if (rebuildQuickRequestNavigation)
        {
            RebuildQuickRequestNavigation();
        }
    }

    private void RunWithNavigationRebuildSuppressed(Action action)
    {
        _navigationRebuildSuspendCount++;
        try
        {
            action();
        }
        finally
        {
            _navigationRebuildSuspendCount--;
            FlushWorkspaceNavigationRebuild();
        }
    }

    private async Task RunWithNavigationRebuildSuppressedAsync(Func<Task> action)
    {
        _navigationRebuildSuspendCount++;
        try
        {
            await action();
        }
        finally
        {
            _navigationRebuildSuspendCount--;
            FlushWorkspaceNavigationRebuild();
        }
    }

    private void FlushWorkspaceNavigationRebuild()
    {
        if (_navigationRebuildSuspendCount > 0)
        {
            return;
        }

        var rebuildInterfaceNavigation = _interfaceNavigationRebuildPending;
        var rebuildQuickRequestNavigation = _quickRequestNavigationRebuildPending;
        _interfaceNavigationRebuildPending = false;
        _quickRequestNavigationRebuildPending = false;
        RequestWorkspaceNavigationRebuild(rebuildInterfaceNavigation, rebuildQuickRequestNavigation);
    }

    private static (bool RebuildInterfaceNavigation, bool RebuildQuickRequestNavigation) ResolveWorkspaceNavigationRebuildScope(NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is NotifyCollectionChangedAction.Reset or NotifyCollectionChangedAction.Move or NotifyCollectionChangedAction.Replace)
        {
            return (true, true);
        }

        var changedItems = EnumerateChangedRequestCases(e).ToList();
        if (changedItems.Count == 0)
        {
            return (true, true);
        }

        var rebuildQuickRequestNavigation = changedItems.Any(IsQuickRequestCaseItem);
        var rebuildInterfaceNavigation = changedItems.Any(item => !IsQuickRequestCaseItem(item));
        return (rebuildInterfaceNavigation, rebuildQuickRequestNavigation);
    }

    private static IEnumerable<RequestCaseItemViewModel> EnumerateChangedRequestCases(NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<RequestCaseItemViewModel>())
            {
                yield return item;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<RequestCaseItemViewModel>())
            {
                yield return item;
            }
        }
    }

    private static bool IsQuickRequestCaseItem(RequestCaseItemViewModel item)
    {
        return string.Equals(item.SourceCase.EntryType, ProjectTabRequestEntryTypes.QuickRequest, StringComparison.OrdinalIgnoreCase);
    }

    private static void SynchronizeExplorerItems(
        ObservableCollection<ExplorerItemViewModel> target,
        IReadOnlyList<ExplorerItemViewModel> desiredItems)
    {
        for (var index = 0; index < desiredItems.Count; index++)
        {
            var desiredItem = desiredItems[index];
            var currentIndex = FindExplorerItemIndex(target, desiredItem.NodeKey);
            if (currentIndex >= 0)
            {
                var existingItem = target[currentIndex];
                existingItem.SyncFrom(desiredItem);
                SynchronizeExplorerItems(existingItem.Children, desiredItem.Children);
                if (currentIndex != index)
                {
                    target.Move(currentIndex, index);
                }

                continue;
            }

            target.Insert(index, desiredItem);
        }

        while (target.Count > desiredItems.Count)
        {
            target.RemoveAt(target.Count - 1);
        }
    }

    private static int FindExplorerItemIndex(ObservableCollection<ExplorerItemViewModel> items, string nodeKey)
    {
        for (var index = 0; index < items.Count; index++)
        {
            if (string.Equals(items[index].NodeKey, nodeKey, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private RequestCaseDto? FindRequestById(string id)
    {
        return SavedRequests
            .Select(item => item.SourceCase)
            .FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    private string ResolveLatestCaseName(string interfaceId)
    {
        return SavedRequests
            .Where(item => string.Equals(item.SourceCase.EntryType, ProjectTabRequestEntryTypes.HttpCase, StringComparison.OrdinalIgnoreCase))
            .Where(item => string.Equals(item.SourceCase.ParentId, interfaceId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.UpdatedAt)
            .Select(item => item.Name)
            .FirstOrDefault()
            ?? "成功";
    }

    private static bool IsImportedInterface(RequestCaseDto requestCase)
    {
        return requestCase.RequestSnapshot.EndpointId.StartsWith(ImportedEndpointKeyPrefix, StringComparison.OrdinalIgnoreCase);
    }
}
