using ApixPress.App.Helpers;
using ApixPress.App.Models.DTOs;
using System.Collections.Specialized;

namespace ApixPress.App.ViewModels;

public partial class ProjectTabViewModel
{
    private async Task<bool> SaveQuickRequestAsync(RequestWorkspaceTabViewModel workspaceTab, string? requestNameOverride = null)
    {
        if (!HasAbsoluteHttpUrl(workspaceTab.RequestUrl))
        {
            StatusMessage = "快捷请求仅支持完整地址，请输入 http:// 或 https:// 开头的 URL。";
            NotifyShellState();
            return false;
        }

        var requestName = string.IsNullOrWhiteSpace(requestNameOverride)
            ? workspaceTab.ResolveRequestName()
            : requestNameOverride.Trim();
        var snapshot = workspaceTab.BuildSnapshot(requestName);
        var result = await _requestCaseService.SaveAsync(new RequestCaseDto
        {
            Id = workspaceTab.EditingQuickRequestId,
            ProjectId = ProjectId,
            EntryType = ProjectTabRequestEntryTypes.QuickRequest,
            Name = requestName,
            GroupName = "快捷请求",
            Description = workspaceTab.ConfigTab.RequestDescription,
            RequestSnapshot = snapshot,
            UpdatedAt = DateTime.UtcNow
        }, CancellationToken.None);

        if (result.IsSuccess && result.Data is not null)
        {
            workspaceTab.EditingQuickRequestId = result.Data.Id;
            RunWithWorkspaceNavigationRebuildSuppressed(() => UseCasesPanel.UpsertCaseItem(result.Data));
            StatusMessage = "快捷请求已保存到左侧目录。";
        }
        else
        {
            StatusMessage = result.Message;
        }

        NotifyShellState();
        return result.IsSuccess && result.Data is not null;
    }

    private async Task SaveHttpInterfaceAsync(RequestWorkspaceTabViewModel workspaceTab)
    {
        var interfaceId = await EnsureHttpInterfaceSavedAsync(workspaceTab, reloadAfterSave: true);
        if (!string.IsNullOrWhiteSpace(interfaceId))
        {
            StatusMessage = "HTTP 接口已保存到默认模块。";
            NotifyShellState();
        }
    }

    private async Task<string?> EnsureHttpInterfaceSavedAsync(RequestWorkspaceTabViewModel workspaceTab, bool reloadAfterSave)
    {
        var snapshot = workspaceTab.BuildSnapshot();
        var result = await _requestCaseService.SaveAsync(new RequestCaseDto
        {
            Id = workspaceTab.EditingInterfaceId,
            ProjectId = ProjectId,
            EntryType = ProjectTabRequestEntryTypes.HttpInterface,
            Name = workspaceTab.ResolveRequestName(),
            GroupName = "接口",
            FolderPath = ProjectWorkspaceTreeBuilder.NormalizeFolderPath(workspaceTab.InterfaceFolderPath),
            Description = workspaceTab.ConfigTab.RequestDescription,
            RequestSnapshot = snapshot,
            UpdatedAt = DateTime.UtcNow
        }, CancellationToken.None);

        if (!result.IsSuccess || result.Data is null)
        {
            StatusMessage = result.Message;
            NotifyShellState();
            return null;
        }

        workspaceTab.EditingInterfaceId = result.Data.Id;
        workspaceTab.SourceEndpointId = result.Data.RequestSnapshot.EndpointId;
        if (reloadAfterSave)
        {
            RunWithWorkspaceNavigationRebuildSuppressed(() => UseCasesPanel.UpsertCaseItem(result.Data));
        }

        return result.Data.Id;
    }

    private async Task ReloadSavedRequestsAsync()
    {
        await RunWithWorkspaceNavigationRebuildSuppressedAsync(() => UseCasesPanel.LoadCasesAsync());
    }

    private async Task SyncImportedInterfacesAsync(IReadOnlyList<ApiEndpointDto> endpoints)
    {
        await _requestCaseService.SyncImportedHttpInterfacesAsync(ProjectId, endpoints, CancellationToken.None);
        await ReloadSavedRequestsAsync();
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

    private void RebuildWorkspaceNavigation()
    {
        RebuildInterfaceNavigation();
        RebuildQuickRequestNavigation();
    }

    private void RequestWorkspaceNavigationRebuild(bool rebuildInterfaceNavigation = true, bool rebuildQuickRequestNavigation = true)
    {
        if (!rebuildInterfaceNavigation && !rebuildQuickRequestNavigation)
        {
            return;
        }

        if (_workspaceNavigationRebuildSuspendCount > 0)
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

    private void RunWithWorkspaceNavigationRebuildSuppressed(Action action)
    {
        _workspaceNavigationRebuildSuspendCount++;
        try
        {
            action();
        }
        finally
        {
            _workspaceNavigationRebuildSuspendCount--;
            FlushWorkspaceNavigationRebuild();
        }
    }

    private async Task RunWithWorkspaceNavigationRebuildSuppressedAsync(Func<Task> action)
    {
        _workspaceNavigationRebuildSuspendCount++;
        try
        {
            await action();
        }
        finally
        {
            _workspaceNavigationRebuildSuspendCount--;
            FlushWorkspaceNavigationRebuild();
        }
    }

    private void FlushWorkspaceNavigationRebuild()
    {
        if (_workspaceNavigationRebuildSuspendCount > 0)
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
        System.Collections.ObjectModel.ObservableCollection<ExplorerItemViewModel> target,
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

    private static int FindExplorerItemIndex(
        System.Collections.ObjectModel.ObservableCollection<ExplorerItemViewModel> items,
        string nodeKey)
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

    private RequestWorkspaceTabViewModel ReuseActiveLandingOrCreateWorkspace()
    {
        if (ActiveWorkspaceTab?.IsLandingTab == true)
        {
            return ActiveWorkspaceTab;
        }

        return CreateWorkspaceTab(activate: false);
    }

    private RequestWorkspaceTabViewModel CreateWorkspaceTab(bool activate, bool showInTabStrip = true)
    {
        var tab = new RequestWorkspaceTabViewModel();
        tab.ConfigureAsLanding();
        tab.ShowInTabStrip = showInTabStrip;
        AttachWorkspaceTab(tab);
        WorkspaceTabs.Add(tab);
        if (activate)
        {
            ActivateWorkspaceTabCore(tab);
        }

        return tab;
    }

    private void EnsureLandingWorkspaceTab()
    {
        if (WorkspaceTabs.Count == 0)
        {
            var tab = CreateWorkspaceTab(activate: false, showInTabStrip: false);
            tab.ConfigureAsLanding();
            tab.ShowInTabStrip = false;
            ActivateWorkspaceTabCore(tab);
            return;
        }

        if (ActiveWorkspaceTab is null)
        {
            ActivateWorkspaceTabCore(WorkspaceTabs[0]);
        }
    }

    private RequestWorkspaceTabViewModel? FindLandingWorkspaceTab()
    {
        return WorkspaceTabs
            .Where(item => item.IsLandingTab)
            .OrderByDescending(item => item.ShowInTabStrip)
            .FirstOrDefault();
    }

    private RequestWorkspaceTabViewModel? FindFirstQuickRequestTab()
    {
        return WorkspaceTabs.FirstOrDefault(item => item.IsQuickRequestTab);
    }

    private RequestWorkspaceTabViewModel? FindWorkspaceTabForSource(RequestCaseDto source)
    {
        return WorkspaceTabs.FirstOrDefault(item =>
            string.Equals(source.EntryType, ProjectTabRequestEntryTypes.QuickRequest, StringComparison.OrdinalIgnoreCase)
                ? string.Equals(item.EditingQuickRequestId, source.Id, StringComparison.OrdinalIgnoreCase)
                : string.Equals(source.EntryType, ProjectTabRequestEntryTypes.HttpInterface, StringComparison.OrdinalIgnoreCase)
                    ? string.Equals(item.EditingInterfaceId, source.Id, StringComparison.OrdinalIgnoreCase)
                    : string.Equals(item.EditingCaseId, source.Id, StringComparison.OrdinalIgnoreCase));
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

    private void ActivateWorkspaceTabCore(RequestWorkspaceTabViewModel tab)
    {
        ActiveWorkspaceTab = tab;
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

    private static bool HasAbsoluteHttpUrl(string? value)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }
}
