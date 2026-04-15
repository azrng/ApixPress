using ApixPress.App.Helpers;
using ApixPress.App.Models.DTOs;

namespace ApixPress.App.ViewModels;

public partial class ProjectTabViewModel
{
    private async Task SaveQuickRequestAsync(RequestWorkspaceTabViewModel workspaceTab, string? requestNameOverride = null)
    {
        if (!HasAbsoluteHttpUrl(workspaceTab.RequestUrl))
        {
            StatusMessage = "快捷请求仅支持完整地址，请输入 http:// 或 https:// 开头的 URL。";
            NotifyShellState();
            return;
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
            await ReloadSavedRequestsAsync();
            StatusMessage = "快捷请求已保存到左侧目录。";
        }
        else
        {
            StatusMessage = result.Message;
        }

        NotifyShellState();
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
            await ReloadSavedRequestsAsync();
        }

        return result.Data.Id;
    }

    private async Task ReloadSavedRequestsAsync()
    {
        await UseCasesPanel.LoadCasesAsync();
        RebuildWorkspaceNavigation();
    }

    private void RebuildWorkspaceNavigation()
    {
        InterfaceTreeItems.Clear();
        QuickRequestTreeItems.Clear();
        var (interfaceRoot, quickRequests) = ProjectWorkspaceTreeBuilder.Build(SavedRequests, RequestDeleteWorkspaceTreeItemCommand);
        InterfaceTreeItems.Add(interfaceRoot);
        QuickRequestTreeItems.ReplaceWith(quickRequests);

        OnPropertyChanged(nameof(InterfaceCatalogItems));
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

    private ProjectEnvironmentDto BuildExecutionEnvironment()
    {
        var environment = EnvironmentPanel.GetSelectedEnvironmentDto();
        if (environment is not null)
        {
            return environment;
        }

        return new ProjectEnvironmentDto
        {
            Id = string.Empty,
            ProjectId = ProjectId,
            Name = "未配置环境",
            BaseUrl = string.Empty,
            IsActive = false,
            SortOrder = 0
        };
    }

    private void SetImportDataStatus(string message, string statusState)
    {
        ImportDataStatusText = message;
        ImportDataStatusState = statusState;
    }

    private void ClearPendingImportConfirmation()
    {
        _pendingImportRequest = null;
        PendingImportPreview = null;
        IsImportOverwriteConfirmDialogOpen = false;
    }

    private static string ResolveImportSourceTypeText(string sourceType)
    {
        return string.Equals(sourceType, "URL", StringComparison.OrdinalIgnoreCase)
            ? "URL 导入"
            : "文件上传";
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

    private static string BuildHttpCaseName(RequestWorkspaceTabViewModel workspaceTab)
    {
        return string.IsNullOrWhiteSpace(workspaceTab.HttpCaseName)
            ? "成功"
            : workspaceTab.HttpCaseName.Trim();
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

    private static bool IsImportedInterface(RequestCaseDto requestCase)
    {
        return requestCase.RequestSnapshot.EndpointId.StartsWith(ImportedEndpointKeyPrefix, StringComparison.OrdinalIgnoreCase);
    }
}
