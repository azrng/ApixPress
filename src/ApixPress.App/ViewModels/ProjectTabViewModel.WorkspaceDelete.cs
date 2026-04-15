using ApixPress.App.Models.DTOs;
using CommunityToolkit.Mvvm.Input;

namespace ApixPress.App.ViewModels;

public partial class ProjectTabViewModel
{
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
            StatusMessage = "当前节点没有可删除的内容。";
            NotifyShellState();
            return;
        }

        var importedInterfaces = targets
            .Where(source => string.Equals(source.EntryType, ProjectTabRequestEntryTypes.HttpInterface, StringComparison.OrdinalIgnoreCase))
            .Where(IsImportedInterface)
            .ToList();
        if (importedInterfaces.Count > 0)
        {
            await _apiWorkspaceService.DeleteImportedHttpInterfacesAsync(ProjectId, importedInterfaces, CancellationToken.None);
        }

        foreach (var source in targets
                     .OrderBy(source => ProjectWorkspaceTreeBuilder.ResolveDeletePriority(source.EntryType))
                     .ThenBy(source => source.Name, StringComparer.OrdinalIgnoreCase))
        {
            await _requestCaseService.DeleteAsync(ProjectId, source.Id, CancellationToken.None);
        }

        CloseWorkspaceTabsForDeletedCases(targets);
        if (importedInterfaces.Count > 0)
        {
            await LoadImportedDocumentsAsync(manageBusyState: false);
        }
        else
        {
            await ReloadSavedRequestsAsync();
        }

        StatusMessage = targets.Count == 1
            ? $"已删除：{targets[0].Name}"
            : $"已删除 {targets.Count} 项内容。";
        NotifyShellState();
    }

    [RelayCommand]
    private void RequestDeleteWorkspaceTreeItem(ExplorerItemViewModel? item)
    {
        if (item is null || !item.CanDelete)
        {
            return;
        }

        PendingDeleteWorkspaceItem = item;
        IsWorkspaceDeleteConfirmDialogOpen = true;
        StatusMessage = $"准备删除：{item.Title}";
        NotifyShellState();
    }

    [RelayCommand]
    private void CancelWorkspaceItemDelete()
    {
        PendingDeleteWorkspaceItem = null;
        IsWorkspaceDeleteConfirmDialogOpen = false;
        StatusMessage = "已取消删除。";
        NotifyShellState();
    }

    [RelayCommand]
    private async Task ConfirmWorkspaceItemDeleteAsync()
    {
        if (PendingDeleteWorkspaceItem is null)
        {
            IsWorkspaceDeleteConfirmDialogOpen = false;
            NotifyShellState();
            return;
        }

        var item = PendingDeleteWorkspaceItem;
        PendingDeleteWorkspaceItem = null;
        IsWorkspaceDeleteConfirmDialogOpen = false;
        await DeleteWorkspaceItemAsync(item);
    }

    private void CloseWorkspaceTabsForDeletedCases(IReadOnlyCollection<RequestCaseDto> deletedCases)
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

    private static bool IsImportedInterface(RequestCaseDto requestCase)
    {
        return requestCase.RequestSnapshot.EndpointId.StartsWith(ImportedEndpointKeyPrefix, StringComparison.OrdinalIgnoreCase);
    }
}
