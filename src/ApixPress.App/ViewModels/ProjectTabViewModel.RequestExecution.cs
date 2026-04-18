using ApixPress.App.Helpers;
using ApixPress.App.Models.DTOs;

namespace ApixPress.App.ViewModels;

public partial class ProjectTabViewModel
{
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
                var historyResult = await _requestHistoryService.AddAsync(ProjectId, snapshot, result.Data, cancellationToken);
                if (historyResult.IsSuccess && historyResult.Data is not null)
                {
                    HistoryPanel.PrependHistoryItem(historyResult.Data);
                }
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
}
