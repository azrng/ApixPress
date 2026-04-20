using ApixPress.App.Models.DTOs;

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
            Catalog.UpsertCaseItem(result.Data);
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
            Catalog.UpsertCaseItem(result.Data);
        }

        return result.Data.Id;
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
