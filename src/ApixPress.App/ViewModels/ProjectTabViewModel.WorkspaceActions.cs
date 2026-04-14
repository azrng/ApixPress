using ApixPress.App.Helpers;
using ApixPress.App.Models.DTOs;
using CommunityToolkit.Mvvm.Input;
using Azrng.Core.Results;

namespace ApixPress.App.ViewModels;

public partial class ProjectTabViewModel
{
    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        await LoadWorkspaceAsync();
    }

    public async Task RefreshAsync()
    {
        await LoadWorkspaceAsync(EnvironmentPanel.SelectedEnvironment?.Id);
        StatusMessage = $"项目 {Project.Name} 已刷新。";
        NotifyShellState();
    }

    public async Task SaveCurrentEnvironmentAsync()
    {
        if (!EnvironmentPanel.HasSelectedEnvironment)
        {
            StatusMessage = "请先选择环境后再保存。";
            NotifyShellState();
            return;
        }

        await EnvironmentPanel.SaveEnvironmentCommand.ExecuteAsync(null);
        StatusMessage = $"环境 {CurrentEnvironmentLabel} 已保存。";
        NotifyShellState();
    }

    public async Task SendQuickRequestAsync()
    {
        SelectedWorkspaceSection = WorkspaceSections.InterfaceManagement;
        var workspaceTab = ActiveWorkspaceTab;
        if (workspaceTab is null || workspaceTab.IsLandingTab)
        {
            StatusMessage = "请先打开一个 HTTP 接口或快捷请求标签。";
            NotifyShellState();
            return;
        }

        if (string.IsNullOrWhiteSpace(workspaceTab.RequestUrl))
        {
            StatusMessage = "请输入请求地址。";
            NotifyShellState();
            return;
        }

        if (workspaceTab.IsQuickRequestTab && !HasAbsoluteHttpUrl(workspaceTab.RequestUrl))
        {
            StatusMessage = "快捷请求仅支持完整地址，请输入 http:// 或 https:// 开头的 URL。";
            NotifyShellState();
            return;
        }

        IsBusy = true;
        var cancellationToken = CancellationTokenSourceHelper.Refresh(ref _sendRequestCancellationTokenSource).Token;
        try
        {
            var snapshot = workspaceTab.BuildSnapshot();
            var environment = BuildExecutionEnvironment();
            var result = await _requestExecutionService.SendAsync(snapshot, environment, cancellationToken);
            workspaceTab.ResponseSection.ApplyResult(result, snapshot);

            if (result.IsSuccess || result.Data is not null)
            {
                await _requestHistoryService.AddAsync(ProjectId, snapshot, result.Data, cancellationToken);
                await HistoryPanel.LoadHistoryAsync();
            }

            StatusMessage = result.IsSuccess
                ? (workspaceTab.IsHttpInterfaceTab ? "HTTP 接口请求发送完成。" : "快捷请求发送完成。")
                : result.Message;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusMessage = "已取消当前请求。";
        }
        finally
        {
            IsBusy = false;
            NotifyShellState();
        }
    }

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
            EntryType = RequestEntryTypes.QuickRequest,
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
            EntryType = RequestEntryTypes.HttpCase,
            Name = BuildHttpCaseName(workspaceTab),
            GroupName = "用例",
            FolderPath = NormalizeFolderPath(workspaceTab.InterfaceFolderPath),
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

    public void LoadWorkspaceItem(ExplorerItemViewModel? item)
    {
        if (item is null || item.SourceCase is null)
        {
            return;
        }

        var source = item.SourceCase;
        var parentInterface = string.Equals(source.EntryType, RequestEntryTypes.HttpCase, StringComparison.OrdinalIgnoreCase)
            ? FindRequestById(source.ParentId)
            : null;

        SelectedWorkspaceSection = WorkspaceSections.InterfaceManagement;
        var targetTab = FindWorkspaceTabForSource(source) ?? ReuseActiveLandingOrCreateWorkspace();
        targetTab.ApplySavedRequest(source, parentInterface);

        if (string.Equals(source.EntryType, RequestEntryTypes.HttpInterface, StringComparison.OrdinalIgnoreCase))
        {
            targetTab.HttpCaseName = ResolveLatestCaseName(source.Id);
        }

        ActivateWorkspaceTabCore(targetTab);
        StatusMessage = source.EntryType switch
        {
            RequestEntryTypes.HttpInterface => $"已加载 HTTP 接口：{source.Name}",
            RequestEntryTypes.HttpCase => $"已加载接口用例：{source.Name}",
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

    public async Task DeleteWorkspaceItemAsync(ExplorerItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        var targets = CollectDeletableSourceCases(item)
            .DistinctBy(source => source.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (targets.Count == 0)
        {
            StatusMessage = "当前节点没有可删除的内容。";
            NotifyShellState();
            return;
        }

        var importedInterfaces = targets
            .Where(source => string.Equals(source.EntryType, RequestEntryTypes.HttpInterface, StringComparison.OrdinalIgnoreCase))
            .Where(IsImportedInterface)
            .ToList();
        if (importedInterfaces.Count > 0)
        {
            await _apiWorkspaceService.DeleteImportedHttpInterfacesAsync(ProjectId, importedInterfaces, CancellationToken.None);
        }

        foreach (var source in targets
                     .OrderBy(source => ResolveDeletePriority(source.EntryType))
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
}
