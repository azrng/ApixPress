using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ApixPress.App.Helpers;
using ApixPress.App.Messages;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels.Base;
using Azrng.Core.Results;
using Irihi.Avalonia.Shared.Contracts;
using Ursa.Controls;

namespace ApixPress.App.ViewModels;

public partial class ProjectWorkspaceCatalogViewModel : ViewModelBase, IDialogContext
{
    private const string ImportedEndpointKeyPrefix = "swagger-import:";
    private const int InterfaceSearchDebounceMilliseconds = 250;

    private readonly string _projectId;
    private readonly IRequestCaseService _requestCaseService;
    private readonly IApiWorkspaceService _apiWorkspaceService;
    private readonly UseCasesPanelViewModel _useCasesPanel;
    private readonly ProjectWorkspaceTabsViewModel _workspace;
    private readonly Func<Task> _openInterfaceRootWorkspaceAsync;
    private readonly Action _showInterfaceManagementSection;
    private readonly IMessenger _messenger;
    private readonly Func<Task> _reloadImportedDocumentsAsync;
    private readonly IDialogService _dialogService;
    private CancellationTokenSource? _interfaceSearchDebounceCancellationTokenSource;
    private int _navigationRebuildSuspendCount;
    private bool _interfaceNavigationRebuildPending;
    private bool _quickRequestNavigationRebuildPending;

    public ProjectWorkspaceCatalogViewModel(
        string projectId,
        IRequestCaseService requestCaseService,
        IApiWorkspaceService apiWorkspaceService,
        UseCasesPanelViewModel useCasesPanel,
        ProjectWorkspaceTabsViewModel workspace,
        Func<Task> openInterfaceRootWorkspaceAsync,
        Action showInterfaceManagementSection,
        IMessenger messenger,
        Func<Task> reloadImportedDocumentsAsync,
        IDialogService dialogService)
    {
        _projectId = projectId;
        _requestCaseService = requestCaseService;
        _apiWorkspaceService = apiWorkspaceService;
        _useCasesPanel = useCasesPanel;
        _workspace = workspace;
        _openInterfaceRootWorkspaceAsync = openInterfaceRootWorkspaceAsync;
        _showInterfaceManagementSection = showInterfaceManagementSection;
        _messenger = messenger;
        _reloadImportedDocumentsAsync = reloadImportedDocumentsAsync;
        _dialogService = dialogService;

        _useCasesPanel.RequestCases.CollectionChanged += OnSavedRequestsCollectionChanged;
    }

    public ObservableCollection<ExplorerItemViewModel> InterfaceTreeItems { get; } = [];
    public ObservableCollection<ExplorerItemViewModel> QuickRequestTreeItems { get; } = [];

    /// <summary>
    /// 接口树扁平化后的可见行，供 ListBox + VirtualizingStackPanel 虚拟化展示。
    /// Avalonia TreeView 子层级无法虚拟化，151+ 接口时展开需要一次性实例化全部行导致卡顿，
    /// 因此树模型（InterfaceTreeItems）仍是唯一事实，这里仅派生当前展开状态下的可见行。
    /// </summary>
    public ObservableCollection<ExplorerItemViewModel> InterfaceFlatRows { get; } = [];

    private readonly List<ExplorerItemViewModel> _flatRowSubscriptions = [];
    public ObservableCollection<RequestCaseItemViewModel> SavedRequests => _useCasesPanel.RequestCases;
    public IReadOnlyList<ExplorerItemViewModel> InterfaceCatalogItems => InterfaceTreeItems.FirstOrDefault()?.Children ?? [];
    public bool HasQuickRequestEntries => SavedRequests.Any(item => string.Equals(item.SourceCase.EntryType, ProjectTabRequestEntryTypes.QuickRequest, StringComparison.OrdinalIgnoreCase));
    public bool HasInterfaceEntries => SavedRequests.Any(item => string.Equals(item.SourceCase.EntryType, ProjectTabRequestEntryTypes.HttpInterface, StringComparison.OrdinalIgnoreCase));
    public bool HasInterfaceSearchText => !string.IsNullOrWhiteSpace(InterfaceSearchText);
    public bool ShowInterfaceEntriesEmptyState => !HasInterfaceEntries || (HasInterfaceSearchText && InterfaceCatalogItems.Count == 0);
    public string InterfaceEmptyStateText => HasInterfaceSearchText ? "没有匹配的 HTTP 接口" : "当前还没有保存的 HTTP 接口";
    public bool ShowQuickRequestEntriesEmptyState => !HasQuickRequestEntries;
    public bool ShowSavedRequestsEmptyState => !HasQuickRequestEntries && !HasInterfaceEntries;
    public string InterfaceSectionHint => HasInterfaceEntries ? "接口 / 目录" : "还没有保存的 HTTP 接口";
    public string QuickRequestSectionHint => HasQuickRequestEntries ? "保存到左侧快捷请求目录" : "左侧快捷请求目录还是空的";
    public bool HasPendingDeleteTarget => PendingDeleteWorkspaceItem is not null;
    public string PendingDeleteTitle => PendingDeleteWorkspaceItem?.Title ?? string.Empty;
    public string PendingCreateFolderParentDisplay => string.IsNullOrWhiteSpace(PendingCreateFolderParentPath)
        ? "根目录"
        : PendingCreateFolderParentPath;
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
    private bool isInterfaceCatalogExpanded;

    [ObservableProperty]
    private bool isQuickRequestCatalogExpanded = true;

    /// <summary>删除确认 / 新建目录弹窗共用异步关闭通道（两者同为模态，不会同时打开）。</summary>
    public event EventHandler<object?>? RequestClose;

    public void Close() => RequestClose?.Invoke(this, null);

    [ObservableProperty]
    private string folderNameInput = string.Empty;

    [ObservableProperty]
    private string pendingCreateFolderParentPath = string.Empty;

    [ObservableProperty]
    private string interfaceSearchText = string.Empty;

    [ObservableProperty]
    private ExplorerItemViewModel? pendingDeleteWorkspaceItem;

    protected override void DisposeManaged()
    {
        CancellationTokenSourceHelper.CancelAndDispose(ref _interfaceSearchDebounceCancellationTokenSource);
        _useCasesPanel.RequestCases.CollectionChanged -= OnSavedRequestsCollectionChanged;
        DisposeExplorerItems(InterfaceTreeItems);
        DisposeExplorerItems(QuickRequestTreeItems);
        InterfaceTreeItems.Clear();
        QuickRequestTreeItems.Clear();
        UnsubscribeFlatRows();
        InterfaceFlatRows.Clear();
    }

    public async Task LoadWorkspaceItem(ExplorerItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        if (string.Equals(item.NodeType, "interface-root", StringComparison.OrdinalIgnoreCase))
        {
            await _openInterfaceRootWorkspaceAsync();
            return;
        }

        // 目录节点只承载结构，点击不打开编辑标签（展开/收起由树自身处理）
        if (item.IsFolderNode
            || (item.SourceCase is not null
                && string.Equals(item.SourceCase.EntryType, ProjectTabRequestEntryTypes.Folder, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (item.SourceCase is null)
        {
            return;
        }

        var source = item.SourceCase;
        var openStopwatch = System.Diagnostics.Stopwatch.StartNew();
        _showInterfaceManagementSection();
        var targetTab = _workspace.ResolveTabForWorkspaceNavigation(source);
        // 标签已在编辑该用例且有未保存修改时只激活不回填：旧快照覆盖编辑器状态会导致保存出陈旧数据
        if (!IsTabStillEditingSource(targetTab, source) || !targetTab.HasUnsavedChanges)
        {
            ApplyWorkspaceItemToTab(targetTab, source);
        }

        _workspace.ActivateWorkspaceTab(targetTab);
        _messenger.Send(new StatusMessageRequest(source.EntryType switch
        {
            ProjectTabRequestEntryTypes.HttpInterface => $"已加载 HTTP 接口：{source.Name}",
            ProjectTabRequestEntryTypes.HttpCase => $"已加载接口用例：{source.Name}",
            _ => $"已加载快捷请求：{source.Name}"
        }));
        _messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.ShellState));
        PerfLog.Log("树节点·打开工作区标签", openStopwatch.ElapsedMilliseconds, source.Name);

        if (source.HasLoadedDetail)
        {
            return;
        }

        _ = LoadWorkspaceItemDetailAsync(item, targetTab, source);
    }

    public Task OpenHttpInterfaceAsync(RequestCaseDto source)
    {
        return LoadSavedRequestAsync(source);
    }

    /// <summary>从用例抽屉加载已保存请求，复用接口树的标签和详情加载流程。</summary>
    public Task LoadSavedRequestAsync(RequestCaseDto source)
    {
        var item = new ExplorerItemViewModel
        {
            NodeType = source.EntryType,
            CanLoad = true,
            SourceCase = source
        };
        return LoadWorkspaceItem(item);
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
            _messenger.Send(new StatusMessageRequest("当前节点没有可删除的内容。"));
            _messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.ShellState));
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
        RemoveCases(targets.Select(target => target.Id));
        if (importedInterfaces.Count > 0)
        {
            await _reloadImportedDocumentsAsync();
        }

        _messenger.Send(new StatusMessageRequest(targets.Count == 1
            ? $"已删除：{targets[0].Name}"
            : $"已删除 {targets.Count} 项内容。"));
        _messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.ShellState));
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
        var syncResult = await _requestCaseService.SyncImportedHttpInterfacesAsync(_projectId, endpoints, CancellationToken.None);
        ApplyImportedInterfaceSyncResult(syncResult);
    }

    [RelayCommand]
    private async Task OpenInterfaceRootAsync()
    {
        await _openInterfaceRootWorkspaceAsync();
    }

    /// <summary>打开“新建目录”弹窗；parentFolderPath 为空表示在根目录创建。取消或 chrome 关闭时重置草稿。</summary>
    [RelayCommand]
    private async Task RequestCreateFolderDialogAsync(string? parentFolderPath)
    {
        PendingCreateFolderParentPath = ProjectWorkspaceTreeBuilder.NormalizeFolderPath(parentFolderPath ?? string.Empty);
        FolderNameInput = string.Empty;
        _messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.ShellState));
        var result = await _dialogService.ShowDialogAsync<Views.Controls.WorkspaceCreateFolderDialogView>(this, "新建目录");
        if (result != DialogResult.OK)
        {
            FolderNameInput = string.Empty;
            PendingCreateFolderParentPath = string.Empty;
        }
    }

    [RelayCommand]
    private void CancelCreateFolder()
    {
        RequestClose?.Invoke(this, DialogResult.Cancel);
    }

    [RelayCommand]
    private async Task ConfirmCreateFolderAsync()
    {
        var parentPath = PendingCreateFolderParentPath;
        var result = await _requestCaseService.CreateFolderAsync(_projectId, parentPath, FolderNameInput, CancellationToken.None);
        if (!result.IsSuccess || result.Data is null)
        {
            _messenger.Send(new StatusMessageRequest(result.Message));
            return;
        }

        // 目录行进入用例集合后，树会自动重建并展示空目录
        UpsertCaseItem(result.Data);
        FolderNameInput = string.Empty;
        PendingCreateFolderParentPath = string.Empty;
        RequestClose?.Invoke(this, DialogResult.OK);
        _messenger.Send(new StatusMessageRequest($"已创建目录：{result.Data.Name}"));
        _messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.ShellState));
    }

    /// <summary>打开删除确认弹窗；确认后执行删除，取消或 chrome 关闭时清空待删目标。</summary>
    [RelayCommand]
    private async Task RequestDeleteWorkspaceTreeItemAsync(ExplorerItemViewModel? item)
    {
        if (item is null || !item.CanDelete)
        {
            return;
        }

        PendingDeleteWorkspaceItem = item;
        _messenger.Send(new StatusMessageRequest($"准备删除：{item.Title}"));
        _messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.ShellState));
        var result = await _dialogService.ShowDialogAsync<Views.Controls.WorkspaceDeleteConfirmDialogView>(this, "确认删除");
        if (result != DialogResult.OK)
        {
            PendingDeleteWorkspaceItem = null;
            _messenger.Send(new StatusMessageRequest("已取消删除。"));
            _messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.ShellState));
        }
    }

    [RelayCommand]
    private void CancelDelete()
    {
        RequestClose?.Invoke(this, DialogResult.Cancel);
    }

    [RelayCommand]
    private async Task ConfirmDeleteAsync()
    {
        if (PendingDeleteWorkspaceItem is null)
        {
            RequestClose?.Invoke(this, DialogResult.None);
            _messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.ShellState));
            return;
        }

        var item = PendingDeleteWorkspaceItem;
        PendingDeleteWorkspaceItem = null;
        RequestClose?.Invoke(this, DialogResult.OK);
        await DeleteWorkspaceItemAsync(item);
    }

    partial void OnPendingDeleteWorkspaceItemChanged(ExplorerItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasPendingDeleteTarget));
        OnPropertyChanged(nameof(PendingDeleteTitle));
        OnPropertyChanged(nameof(PendingDeleteDescription));
    }

    partial void OnInterfaceSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasInterfaceSearchText));
        OnPropertyChanged(nameof(InterfaceEmptyStateText));
        OnPropertyChanged(nameof(ShowInterfaceEntriesEmptyState));
        _ = DebounceInterfaceSearchRebuildAsync();
    }

    private async Task DebounceInterfaceSearchRebuildAsync()
    {
        var cancellationToken = CancellationTokenSourceHelper
            .Refresh(ref _interfaceSearchDebounceCancellationTokenSource)
            .Token;

        try
        {
            await Task.Delay(InterfaceSearchDebounceMilliseconds, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (IsDisposed)
        {
            return;
        }

        RequestWorkspaceNavigationRebuild(rebuildQuickRequestNavigation: false);
    }

    private void OnSavedRequestsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var (rebuildInterfaceNavigation, rebuildQuickRequestNavigation) = ResolveWorkspaceNavigationRebuildScope(e);
        RequestWorkspaceNavigationRebuild(rebuildInterfaceNavigation, rebuildQuickRequestNavigation);
        NotifySavedRequestStateChanged();
        _messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.ShellState));
    }

    private void NotifySavedRequestStateChanged()
    {
        OnPropertyChanged(nameof(HasQuickRequestEntries));
        OnPropertyChanged(nameof(HasInterfaceEntries));
        OnPropertyChanged(nameof(HasInterfaceSearchText));
        OnPropertyChanged(nameof(ShowInterfaceEntriesEmptyState));
        OnPropertyChanged(nameof(InterfaceEmptyStateText));
        OnPropertyChanged(nameof(ShowQuickRequestEntriesEmptyState));
        OnPropertyChanged(nameof(ShowSavedRequestsEmptyState));
        OnPropertyChanged(nameof(InterfaceSectionHint));
        OnPropertyChanged(nameof(QuickRequestSectionHint));
    }

    private void RebuildInterfaceNavigation()
    {
        PerfLog.Measure("接口树·导航重建", () =>
        {
            var treeCommands = new WorkspaceTreeItemCommands(
                RequestDeleteWorkspaceTreeItemCommand,
                RequestCreateFolderDialogCommand,
                _workspace.OpenHttpInterfaceWorkspaceCommand);
            SynchronizeExplorerItems(
                InterfaceTreeItems,
                [ProjectWorkspaceTreeBuilder.BuildInterfaceRoot(
                    ResolveInterfaceNavigationItems(),
                    treeCommands,
                    HasInterfaceSearchText)]);
            OnPropertyChanged(nameof(InterfaceCatalogItems));
            OnPropertyChanged(nameof(ShowInterfaceEntriesEmptyState));
            RebuildInterfaceFlatRows();
        });
    }

    private void RebuildQuickRequestNavigation()
    {
        SynchronizeExplorerItems(
            QuickRequestTreeItems,
            ProjectWorkspaceTreeBuilder.BuildQuickRequests(SavedRequests, RequestDeleteWorkspaceTreeItemCommand));
    }

    /// <summary>按当前展开状态全量重建扁平可见行（树重建、搜索过滤后走这里）。</summary>
    private void RebuildInterfaceFlatRows()
    {
        UnsubscribeFlatRows();
        InterfaceFlatRows.Clear();
        if (InterfaceTreeItems.Count > 0)
        {
            AppendInterfaceFlatRows(InterfaceTreeItems[0].Children, depth: 0, insertIndex: 0);
        }
        OnPropertyChanged(nameof(InterfaceFlatRows));
    }

    /// <summary>把 nodes 及其已展开的后代按顺序写入扁平行列表，返回最后一个写入位置。</summary>
    private int AppendInterfaceFlatRows(IReadOnlyList<ExplorerItemViewModel> nodes, int depth, int insertIndex)
    {
        foreach (var node in nodes)
        {
            node.DisplayDepth = depth;
            InterfaceFlatRows.Insert(insertIndex, node);
            TrackFlatRow(node);
            insertIndex++;
            if (node.IsExpanded && node.AreChildrenLoaded)
            {
                insertIndex = AppendInterfaceFlatRows(node.Children, depth + 1, insertIndex);
            }
        }

        return insertIndex;
    }

    /// <summary>展开/收起只增量维护可见行，避免整表 Reset 导致滚动位置丢失与全行重建。</summary>
    private void OnFlatRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (IsDisposed
            || sender is not ExplorerItemViewModel node
            || !string.Equals(e.PropertyName, nameof(ExplorerItemViewModel.IsExpanded), StringComparison.Ordinal))
        {
            return;
        }

        // 大组（如 59 个端点）首次展开的行插入是树交互卡顿的主要测量点
        PerfLog.Measure(node.IsExpanded ? "接口树·展开节点" : "接口树·收起节点", () =>
        {
            var rowIndex = InterfaceFlatRows.IndexOf(node);
            if (rowIndex < 0)
            {
                RebuildInterfaceFlatRows();
                return;
            }

            RemoveFlatDescendantRows(node);
            if (node.IsExpanded)
            {
                // OnIsExpandedChanged 已触发延迟子级加载，这里兜底幂等
                node.EnsureChildrenLoaded();
                AppendInterfaceFlatRows(node.Children, node.DisplayDepth + 1, rowIndex + 1);
            }
        });
    }

    private void RemoveFlatDescendantRows(ExplorerItemViewModel node)
    {
        var descendants = new HashSet<ExplorerItemViewModel>();
        CollectDescendants(node, descendants);
        for (var index = InterfaceFlatRows.Count - 1; index >= 0; index--)
        {
            var row = InterfaceFlatRows[index];
            if (descendants.Contains(row))
            {
                UntrackFlatRow(row);
                InterfaceFlatRows.RemoveAt(index);
            }
        }
    }

    private static void CollectDescendants(ExplorerItemViewModel node, HashSet<ExplorerItemViewModel> into)
    {
        foreach (var child in node.Children)
        {
            into.Add(child);
            CollectDescendants(child, into);
        }
    }

    private void TrackFlatRow(ExplorerItemViewModel node)
    {
        node.PropertyChanged += OnFlatRowPropertyChanged;
        _flatRowSubscriptions.Add(node);
    }

    private void UntrackFlatRow(ExplorerItemViewModel node)
    {
        node.PropertyChanged -= OnFlatRowPropertyChanged;
        _flatRowSubscriptions.Remove(node);
    }

    private void UnsubscribeFlatRows()
    {
        foreach (var node in _flatRowSubscriptions)
        {
            node.PropertyChanged -= OnFlatRowPropertyChanged;
        }

        _flatRowSubscriptions.Clear();
    }

    private IReadOnlyList<RequestCaseItemViewModel> ResolveInterfaceNavigationItems()    {
        var keyword = InterfaceSearchText.Trim();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return SavedRequests;
        }

        var matchedInterfaceIds = SavedRequests
            .Where(item => string.Equals(item.SourceCase.EntryType, ProjectTabRequestEntryTypes.HttpInterface, StringComparison.OrdinalIgnoreCase))
            .Where(item => InterfaceMatchesSearch(item, keyword))
            .Select(item => item.SourceCase.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return SavedRequests
            .Where(item =>
                string.Equals(item.SourceCase.EntryType, ProjectTabRequestEntryTypes.HttpInterface, StringComparison.OrdinalIgnoreCase)
                    && matchedInterfaceIds.Contains(item.SourceCase.Id)
                || string.Equals(item.SourceCase.EntryType, ProjectTabRequestEntryTypes.HttpCase, StringComparison.OrdinalIgnoreCase)
                    && matchedInterfaceIds.Contains(item.SourceCase.ParentId))
            .ToList();
    }

    private static bool InterfaceMatchesSearch(RequestCaseItemViewModel item, string keyword)
    {
        return ContainsSearchText(item.Name, keyword)
            || ContainsSearchText(item.SourceCase.Name, keyword)
            || ContainsSearchText(item.SourceCase.RequestSnapshot.Url, keyword);
    }

    private static bool ContainsSearchText(string value, string keyword)
    {
        return value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
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

    private void ApplyImportedInterfaceSyncResult(ImportedHttpInterfaceSyncResultDto syncResult)
    {
        if (syncResult.UpsertedCases.Count == 0 && syncResult.DeletedCaseIds.Count == 0)
        {
            return;
        }

        RunWithNavigationRebuildSuppressed(() =>
        {
            RemoveCases(syncResult.DeletedCaseIds);
            // 导入 N 个接口一次批量落集合：单次 Reset，避免逐条 Upsert 触发 2N 次级联重建
            _useCasesPanel.UpsertCases(syncResult.UpsertedCases);
        });
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

                if (existingItem.AreChildrenLoaded)
                {
                    desiredItem.EnsureChildrenLoaded();
                }
                else if (desiredItem.AreChildrenLoaded)
                {
                    existingItem.EnsureChildrenLoaded();
                }

                if (existingItem.AreChildrenLoaded || desiredItem.AreChildrenLoaded)
                {
                    SynchronizeExplorerItems(existingItem.Children, desiredItem.Children);
                }

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
            var removedItem = target[^1];
            target.RemoveAt(target.Count - 1);
            removedItem.Dispose();
        }
    }

    private static void DisposeExplorerItems(IEnumerable<ExplorerItemViewModel> items)
    {
        foreach (var item in items)
        {
            item.Dispose();
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

    private void ApplyWorkspaceItemToTab(RequestWorkspaceTabViewModel targetTab, RequestCaseDto source)
    {
        var parentInterface = string.Equals(source.EntryType, ProjectTabRequestEntryTypes.HttpCase, StringComparison.OrdinalIgnoreCase)
            ? FindRequestById(source.ParentId)
            : null;

        _workspace.RunWithNotificationsSuspended(() =>
        {
            targetTab.ApplySavedRequest(source, parentInterface);

            if (string.Equals(source.EntryType, ProjectTabRequestEntryTypes.HttpInterface, StringComparison.OrdinalIgnoreCase))
            {
                targetTab.HttpCaseName = ResolveLatestCaseName(source.Id);
            }
        });
    }

    private static bool IsTabStillEditingSource(RequestWorkspaceTabViewModel tab, RequestCaseDto source)
    {
        return source.EntryType switch
        {
            ProjectTabRequestEntryTypes.QuickRequest => string.Equals(tab.EditingQuickRequestId, source.Id, StringComparison.OrdinalIgnoreCase),
            ProjectTabRequestEntryTypes.HttpInterface => string.Equals(tab.EditingInterfaceId, source.Id, StringComparison.OrdinalIgnoreCase),
            ProjectTabRequestEntryTypes.HttpCase => string.Equals(tab.EditingCaseId, source.Id, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private async Task LoadWorkspaceItemDetailAsync(
        ExplorerItemViewModel item,
        RequestWorkspaceTabViewModel targetTab,
        RequestCaseDto source)
    {
        try
        {
            await Task.Yield();
            var detail = await EnsureCaseDetailLoadedAsync(item);
            if (string.Equals(detail.Id, source.Id, StringComparison.OrdinalIgnoreCase)
                && IsTabStillEditingSource(targetTab, source)
                && !targetTab.HasUnsavedChanges)
            {
                ApplyWorkspaceItemToTab(targetTab, detail);
                _messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.ShellState));
            }
        }
        catch (Exception exception)
        {
            _messenger.Send(new StatusMessageRequest($"加载接口详情失败：{exception.Message}"));
            _messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.ShellState));
        }
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

    private async Task<RequestCaseDto> EnsureCaseDetailLoadedAsync(ExplorerItemViewModel item)
    {
        var requestCase = item.SourceCase;
        if (requestCase is null)
        {
            return new RequestCaseDto();
        }

        if (requestCase.HasLoadedDetail || string.IsNullOrWhiteSpace(requestCase.Id))
        {
            return requestCase;
        }

        var detail = await _requestCaseService.GetDetailAsync(_projectId, requestCase.Id, CancellationToken.None);
        if (detail is null)
        {
            return requestCase;
        }

        item.SourceCase = detail;
        return detail;
    }
}
