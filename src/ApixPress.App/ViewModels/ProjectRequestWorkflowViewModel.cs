using CommunityToolkit.Mvvm.Input;
using ApixPress.App.Helpers;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class ProjectRequestWorkflowViewModel : ViewModelBase
{
    private readonly string _projectId;
    private readonly IRequestExecutionService _requestExecutionService;
    private readonly IRequestCaseService _requestCaseService;
    private readonly IRequestHistoryService _requestHistoryService;
    private readonly ProjectWorkspaceTabsViewModel _workspace;
    private readonly RequestHistoryPanelViewModel _historyPanel;
    private readonly EnvironmentPanelViewModel _environmentPanel;
    private readonly ProjectWorkspaceCatalogViewModel _catalog;
    private readonly ProjectTabWorkspaceContext _workspaceContext;
    private readonly Action<RequestWorkspaceTabViewModel> _openQuickRequestSaveDialog;
    private readonly ProjectTabHostContext _hostContext;
    private CancellationTokenSource? _sendRequestCancellationTokenSource;

    internal ProjectRequestWorkflowViewModel(
        string projectId,
        IRequestExecutionService requestExecutionService,
        IRequestCaseService requestCaseService,
        IRequestHistoryService requestHistoryService,
        ProjectWorkspaceTabsViewModel workspace,
        RequestHistoryPanelViewModel historyPanel,
        EnvironmentPanelViewModel environmentPanel,
        ProjectWorkspaceCatalogViewModel catalog,
        ProjectTabWorkspaceContext workspaceContext,
        Action<RequestWorkspaceTabViewModel> openQuickRequestSaveDialog,
        ProjectTabHostContext hostContext)
    {
        _projectId = projectId;
        _requestExecutionService = requestExecutionService;
        _requestCaseService = requestCaseService;
        _requestHistoryService = requestHistoryService;
        _workspace = workspace;
        _historyPanel = historyPanel;
        _environmentPanel = environmentPanel;
        _catalog = catalog;
        _workspaceContext = workspaceContext;
        _openQuickRequestSaveDialog = openQuickRequestSaveDialog;
        _hostContext = hostContext;
    }

    protected override void DisposeManaged()
    {
        CancellationTokenSourceHelper.CancelAndDispose(ref _sendRequestCancellationTokenSource);
    }

    public async Task SendRequestAsync()
    {
        if (IsDisposed)
        {
            return;
        }

        _workspaceContext.SelectInterfaceManagementSection();
        var workspaceTab = _workspaceContext.GetActiveWorkspaceTab();
        if (workspaceTab is null || workspaceTab.IsLandingTab)
        {
            _hostContext.SetStatusMessage("请先打开一个 HTTP 接口或快捷请求标签。");
            _hostContext.NotifyShellState();
            return;
        }

        if (string.IsNullOrWhiteSpace(workspaceTab.RequestUrl))
        {
            _hostContext.SetStatusMessage("请输入请求地址。");
            _hostContext.NotifyShellState();
            return;
        }

        if (workspaceTab.IsQuickRequestTab && !HasAbsoluteHttpUrl(workspaceTab.RequestUrl))
        {
            _hostContext.SetStatusMessage("快捷请求仅支持完整地址，请输入 http:// 或 https:// 开头的 URL。");
            _hostContext.NotifyShellState();
            return;
        }

        _hostContext.SetBusyState(true);
        workspaceTab.ResponseSection.BeginLoading(workspaceTab.IsHttpInterfaceTab
            ? "正在发送 HTTP 接口请求..."
            : "正在发送快捷请求...");
        var cancellationTokenSource = CancellationTokenSourceHelper.Refresh(ref _sendRequestCancellationTokenSource);
        var cancellationToken = cancellationTokenSource.Token;
        try
        {
            var snapshot = workspaceTab.BuildSnapshot();
            var environment = BuildExecutionEnvironment();
            var result = await _requestExecutionService.SendAsync(snapshot, environment, cancellationToken);
            workspaceTab.ResponseSection.ApplyResult(result, snapshot);

            if (result.IsSuccess || result.Data is not null)
            {
                var historyResult = await _requestHistoryService.AddAsync(_projectId, snapshot, result.Data, cancellationToken);
                if (historyResult.IsSuccess && historyResult.Data is not null)
                {
                    _historyPanel.PrependHistoryItem(historyResult.Data);
                }
            }

            _hostContext.SetStatusMessage(result.IsSuccess
                ? (workspaceTab.IsHttpInterfaceTab ? "HTTP 接口请求发送完成。" : "快捷请求发送完成。")
                : result.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _hostContext.SetStatusMessage("已取消当前请求。");
        }
        finally
        {
            workspaceTab.ResponseSection.EndLoading();
            if (cancellationToken.IsCancellationRequested)
            {
                _hostContext.SetStatusMessage("已取消当前请求。");
            }

            if (ReferenceEquals(_sendRequestCancellationTokenSource, cancellationTokenSource))
            {
                _sendRequestCancellationTokenSource.Dispose();
                _sendRequestCancellationTokenSource = null;
            }

            _hostContext.SetBusyState(false);
            _hostContext.NotifyShellState();
        }
    }

    public void CancelRequest()
    {
        if (IsDisposed || _sendRequestCancellationTokenSource is null)
        {
            return;
        }

        _sendRequestCancellationTokenSource.Cancel();
        _hostContext.SetStatusMessage("已取消当前请求。");
        _hostContext.NotifyShellState();
    }

    public async Task SaveCurrentEditorAsync()
    {
        if (IsDisposed)
        {
            return;
        }

        _workspaceContext.SelectInterfaceManagementSection();
        var workspaceTab = _workspaceContext.GetActiveWorkspaceTab();
        if (workspaceTab is null || workspaceTab.IsLandingTab)
        {
            _hostContext.SetStatusMessage("请先打开一个请求标签。");
            _hostContext.NotifyShellState();
            return;
        }

        if (workspaceTab.IsHttpInterfaceTab)
        {
            await SaveHttpInterfaceAsync(workspaceTab);
            return;
        }

        if (!HasAbsoluteHttpUrl(workspaceTab.RequestUrl))
        {
            _hostContext.SetStatusMessage("快捷请求仅支持完整地址，请输入 http:// 或 https:// 开头的 URL。");
            _hostContext.NotifyShellState();
            return;
        }

        _openQuickRequestSaveDialog(workspaceTab);
        _hostContext.NotifyShellState();
    }

    public async Task SaveHistoryAsQuickRequestAsync(RequestHistoryItemViewModel item)
    {
        var snapshot = item.RequestSnapshot;
        var result = await _requestCaseService.SaveAsync(new RequestCaseDto
        {
            ProjectId = _projectId,
            EntryType = ProjectTabRequestEntryTypes.QuickRequest,
            Name = $"{snapshot.Method} {snapshot.Url}",
            GroupName = "快捷请求",
            Description = $"从 {item.Timestamp.ToLocalTime():yyyy-MM-dd HH:mm} 的历史记录创建",
            RequestSnapshot = snapshot,
            UpdatedAt = DateTime.UtcNow
        }, CancellationToken.None);

        if (result.IsSuccess)
        {
            if (result.Data is not null)
            {
                _catalog.UpsertCaseItem(result.Data);
            }

            _hostContext.SetStatusMessage("已从历史记录生成快捷请求。");
        }
        else
        {
            _hostContext.SetStatusMessage(result.Message);
        }

        _hostContext.NotifyShellState();
    }

    public async Task<bool> SaveQuickRequestAsync(RequestWorkspaceTabViewModel workspaceTab, string? requestNameOverride = null)
    {
        if (!HasAbsoluteHttpUrl(workspaceTab.RequestUrl))
        {
            _hostContext.SetStatusMessage("快捷请求仅支持完整地址，请输入 http:// 或 https:// 开头的 URL。");
            _hostContext.NotifyShellState();
            return false;
        }

        var requestName = string.IsNullOrWhiteSpace(requestNameOverride)
            ? workspaceTab.ResolveRequestName()
            : requestNameOverride.Trim();
        var snapshot = workspaceTab.BuildSnapshot(requestName);
        var result = await _requestCaseService.SaveAsync(new RequestCaseDto
        {
            Id = workspaceTab.EditingQuickRequestId,
            ProjectId = _projectId,
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
            _catalog.UpsertCaseItem(result.Data);
            workspaceTab.MarkCleanState();
            _hostContext.SetStatusMessage("快捷请求已保存到左侧目录。");
        }
        else
        {
            _hostContext.SetStatusMessage(result.Message);
        }

        _hostContext.NotifyShellState();
        return result.IsSuccess && result.Data is not null;
    }

    [RelayCommand]
    public async Task SaveHttpCaseAsync()
    {
        var workspaceTab = _workspaceContext.GetActiveWorkspaceTab();
        if (workspaceTab is null || !workspaceTab.IsHttpInterfaceTab)
        {
            return;
        }

        _workspaceContext.SelectInterfaceManagementSection();
        var savedInterface = await EnsureHttpInterfaceSavedAsync(workspaceTab, reloadAfterSave: false);
        if (savedInterface is null)
        {
            return;
        }

        _catalog.UpsertCaseItem(savedInterface);

        var snapshot = workspaceTab.BuildSnapshot();
        var result = await _requestCaseService.SaveAsync(new RequestCaseDto
        {
            Id = workspaceTab.EditingCaseId,
            ProjectId = _projectId,
            EntryType = ProjectTabRequestEntryTypes.HttpCase,
            Name = BuildHttpCaseName(workspaceTab),
            GroupName = "用例",
            FolderPath = ProjectWorkspaceTreeBuilder.NormalizeFolderPath(workspaceTab.InterfaceFolderPath),
            ParentId = savedInterface.Id,
            Description = $"{workspaceTab.ResolveRequestName()} 的请求用例",
            RequestSnapshot = snapshot,
            UpdatedAt = DateTime.UtcNow
        }, CancellationToken.None);

        if (result.IsSuccess && result.Data is not null)
        {
            workspaceTab.EditingCaseId = result.Data.Id;
            workspaceTab.SourceEndpointId = result.Data.RequestSnapshot.EndpointId;
            _catalog.UpsertCaseItem(result.Data);
            workspaceTab.MarkCleanState();
            _hostContext.SetStatusMessage("HTTP 接口用例已保存。");
        }
        else
        {
            _hostContext.SetStatusMessage(result.Message);
        }

        _hostContext.NotifyShellState();
    }

    private ProjectEnvironmentDto BuildExecutionEnvironment()
    {
        var environment = _environmentPanel.GetSelectedEnvironmentDto();
        if (environment is not null)
        {
            return environment;
        }

        return new ProjectEnvironmentDto
        {
            Id = string.Empty,
            ProjectId = _projectId,
            Name = "未配置环境",
            BaseUrl = string.Empty,
            IsActive = false,
            SortOrder = 0
        };
    }

    private async Task SaveHttpInterfaceAsync(RequestWorkspaceTabViewModel workspaceTab)
    {
        var savedInterface = await EnsureHttpInterfaceSavedAsync(workspaceTab, reloadAfterSave: true);
        if (savedInterface is not null)
        {
            _hostContext.SetStatusMessage("HTTP 接口已保存到默认模块。");
            _hostContext.NotifyShellState();
        }
    }

    private async Task<RequestCaseDto?> EnsureHttpInterfaceSavedAsync(RequestWorkspaceTabViewModel workspaceTab, bool reloadAfterSave)
    {
        var snapshot = workspaceTab.BuildSnapshot();
        var result = await _requestCaseService.SaveAsync(new RequestCaseDto
        {
            Id = workspaceTab.EditingInterfaceId,
            ProjectId = _projectId,
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
            _hostContext.SetStatusMessage(result.Message);
            _hostContext.NotifyShellState();
            return null;
        }

        workspaceTab.EditingInterfaceId = result.Data.Id;
        workspaceTab.SourceEndpointId = result.Data.RequestSnapshot.EndpointId;
        if (reloadAfterSave)
        {
            _catalog.UpsertCaseItem(result.Data);
        }

        workspaceTab.MarkCleanState();
        return result.Data;
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
}
