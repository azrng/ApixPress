using ApixPress.App.Models.DTOs;
using Azrng.Core.Results;

namespace ApixPress.App.ViewModels;

public partial class ProjectTabViewModel
{
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

        SelectedWorkspaceSection = WorkspaceSections.InterfaceManagement;
        var targetTab = FindWorkspaceTabForSource(source) ?? ReuseActiveLandingOrCreateWorkspace();
        targetTab.ApplySavedRequest(source, parentInterface);

        if (string.Equals(source.EntryType, ProjectTabRequestEntryTypes.HttpInterface, StringComparison.OrdinalIgnoreCase))
        {
            targetTab.HttpCaseName = ResolveLatestCaseName(source.Id);
        }

        ActivateWorkspaceTabCore(targetTab);
        StatusMessage = source.EntryType switch
        {
            ProjectTabRequestEntryTypes.HttpInterface => $"已加载 HTTP 接口：{source.Name}",
            ProjectTabRequestEntryTypes.HttpCase => $"已加载接口用例：{source.Name}",
            _ => $"已加载快捷请求：{source.Name}"
        };
        NotifyShellState();
    }

    public void LoadHistoryRequest(RequestHistoryItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        var targetTab = ActiveWorkspaceTab?.IsLandingTab == true
            ? ActiveWorkspaceTab
            : FindFirstQuickRequestTab() ?? CreateWorkspaceTab(activate: false);

        targetTab ??= CreateWorkspaceTab(activate: false);
        targetTab.ConfigureAsQuickRequest();
        targetTab.ApplySnapshot(item.RequestSnapshot);
        if (item.ResponseSnapshot is not null)
        {
            targetTab.ResponseSection.ApplyResult(ResultModel<ResponseSnapshotDto>.Success(item.ResponseSnapshot), item.RequestSnapshot);
        }

        ActivateWorkspaceTabCore(targetTab);
        SelectedWorkspaceSection = WorkspaceSections.RequestHistory;
        StatusMessage = $"已加载历史请求：{item.Method} {item.Url}";
        NotifyShellState();
    }
}
