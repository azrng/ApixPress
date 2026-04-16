using ApixPress.App.Models.DTOs;
using CommunityToolkit.Mvvm.Input;

namespace ApixPress.App.ViewModels;

public partial class ProjectTabViewModel
{
    public async Task SaveCurrentEditorAsync()
    {
        SelectedWorkspaceSection = WorkspaceSections.InterfaceManagement;
        var workspaceTab = ActiveWorkspaceTab;
        if (workspaceTab is null || workspaceTab.IsLandingTab)
        {
            StatusMessage = "请先打开一个请求标签。";
            NotifyShellState();
            return;
        }

        if (workspaceTab.IsHttpInterfaceTab)
        {
            await SaveHttpInterfaceAsync(workspaceTab);
            return;
        }

        if (!HasAbsoluteHttpUrl(workspaceTab.RequestUrl))
        {
            StatusMessage = "快捷请求仅支持完整地址，请输入 http:// 或 https:// 开头的 URL。";
            NotifyShellState();
            return;
        }

        OpenQuickRequestSaveDialog(workspaceTab);
    }

    public async Task SaveHistoryAsQuickRequestAsync(RequestHistoryItemViewModel item)
    {
        var snapshot = item.RequestSnapshot;
        var result = await _requestCaseService.SaveAsync(new RequestCaseDto
        {
            ProjectId = ProjectId,
            EntryType = ProjectTabRequestEntryTypes.QuickRequest,
            Name = $"{snapshot.Method} {snapshot.Url}",
            GroupName = "快捷请求",
            Description = $"从 {item.Timestamp.ToLocalTime():yyyy-MM-dd HH:mm} 的历史记录创建",
            RequestSnapshot = snapshot,
            UpdatedAt = DateTime.UtcNow
        }, CancellationToken.None);

        if (result.IsSuccess)
        {
            await ReloadSavedRequestsAsync();
            StatusMessage = "已从历史记录生成快捷请求。";
        }
        else
        {
            StatusMessage = result.Message;
        }

        NotifyShellState();
    }

    [RelayCommand]
    public async Task SaveHttpCaseAsync()
    {
        var workspaceTab = ActiveWorkspaceTab;
        if (workspaceTab is null || !workspaceTab.IsHttpInterfaceTab)
        {
            return;
        }

        SelectedWorkspaceSection = WorkspaceSections.InterfaceManagement;
        var interfaceId = await EnsureHttpInterfaceSavedAsync(workspaceTab, reloadAfterSave: false);
        if (string.IsNullOrWhiteSpace(interfaceId))
        {
            return;
        }

        var snapshot = workspaceTab.BuildSnapshot();
        var result = await _requestCaseService.SaveAsync(new RequestCaseDto
        {
            Id = workspaceTab.EditingCaseId,
            ProjectId = ProjectId,
            EntryType = ProjectTabRequestEntryTypes.HttpCase,
            Name = BuildHttpCaseName(workspaceTab),
            GroupName = "用例",
            FolderPath = ProjectWorkspaceTreeBuilder.NormalizeFolderPath(workspaceTab.InterfaceFolderPath),
            ParentId = interfaceId,
            Description = $"{workspaceTab.ResolveRequestName()} 的请求用例",
            RequestSnapshot = snapshot,
            UpdatedAt = DateTime.UtcNow
        }, CancellationToken.None);

        if (result.IsSuccess && result.Data is not null)
        {
            workspaceTab.EditingCaseId = result.Data.Id;
            workspaceTab.SourceEndpointId = result.Data.RequestSnapshot.EndpointId;
            await ReloadSavedRequestsAsync();
            StatusMessage = "HTTP 接口用例已保存。";
        }
        else
        {
            StatusMessage = result.Message;
        }

        NotifyShellState();
    }

    [RelayCommand]
    private void CloseQuickRequestSaveDialog()
    {
        IsQuickRequestSaveDialogOpen = false;
        StatusMessage = "已取消保存快捷请求。";
        NotifyShellState();
    }

    [RelayCommand]
    private async Task ConfirmQuickRequestSaveAsync()
    {
        var workspaceTab = ActiveWorkspaceTab;
        if (workspaceTab is null || !workspaceTab.IsQuickRequestTab)
        {
            IsQuickRequestSaveDialogOpen = false;
            NotifyShellState();
            return;
        }

        if (string.IsNullOrWhiteSpace(QuickRequestSaveName))
        {
            StatusMessage = "请输入快捷请求名称。";
            NotifyShellState();
            return;
        }

        workspaceTab.ConfigTab.RequestName = QuickRequestSaveName.Trim();
        workspaceTab.ConfigTab.RequestDescription = QuickRequestSaveDescription.Trim();
        await SaveQuickRequestAsync(workspaceTab, workspaceTab.ConfigTab.RequestName);
        if (!string.IsNullOrWhiteSpace(workspaceTab.EditingQuickRequestId))
        {
            IsQuickRequestSaveDialogOpen = false;
        }

        NotifyShellState();
    }

    private void OpenQuickRequestSaveDialog(RequestWorkspaceTabViewModel workspaceTab)
    {
        var fallbackName = string.IsNullOrWhiteSpace(workspaceTab.ConfigTab.RequestName)
            ? workspaceTab.ResolveRequestName()
            : workspaceTab.ConfigTab.RequestName.Trim();
        QuickRequestSaveName = fallbackName;
        QuickRequestSaveDescription = workspaceTab.ConfigTab.RequestDescription;
        IsQuickRequestSaveDialogOpen = true;
        StatusMessage = "请输入快捷请求名称后再保存。";
        NotifyShellState();
    }

    private static string BuildHttpCaseName(RequestWorkspaceTabViewModel workspaceTab)
    {
        return string.IsNullOrWhiteSpace(workspaceTab.HttpCaseName)
            ? "成功"
            : workspaceTab.HttpCaseName.Trim();
    }
}
