using ApixPress.App.Helpers;
using ApixPress.App.Models.DTOs;
using CommunityToolkit.Mvvm.Input;
using Azrng.Core.Results;

namespace ApixPress.App.ViewModels;

public partial class ProjectTabViewModel
{
    private async Task ImportSwaggerAsync(
        Func<CancellationToken, Task<IResultModel<ApiImportPreviewDto>>> previewAction,
        Func<CancellationToken, Task<IResultModel<ApiDocumentDto>>> importAction,
        Func<ApiDocumentDto, string> buildSuccessMessage,
        string previewBusyText,
        string importBusyText)
    {
        if (IsImportDataBusy)
        {
            return;
        }

        var cancellationToken = CancellationTokenSourceHelper.Refresh(ref _importCancellationTokenSource).Token;
        ImportDataBusyText = previewBusyText;
        IsImportDataBusy = true;
        try
        {
            var previewResult = await previewAction(cancellationToken);
            if (!previewResult.IsSuccess || previewResult.Data is null)
            {
                var failureMessage = string.IsNullOrWhiteSpace(previewResult.Message)
                    ? "Swagger 导入预检查失败，请检查文档格式后重试。"
                    : previewResult.Message;
                SetImportDataStatus(failureMessage, ImportStatusStates.Error);
                StatusMessage = failureMessage;
                return;
            }

            if (previewResult.Data.ConflictCount > 0)
            {
                _pendingImportRequest = new PendingImportRequest(importAction, buildSuccessMessage, importBusyText);
                PendingImportPreview = previewResult.Data;
                IsImportOverwriteConfirmDialogOpen = true;
                SetImportDataStatus(
                    $"检测到 {previewResult.Data.ConflictCount} 个同路径接口，确认后将覆盖旧接口并保留未冲突接口。",
                    ImportStatusStates.Info);
                StatusMessage = "检测到同路径接口，等待确认是否覆盖。";
                return;
            }

            await ExecuteImportAsync(importAction, buildSuccessMessage, importBusyText, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            IsImportDataBusy = false;
            NotifyShellState();
        }
    }

    [RelayCommand]
    private void CancelImportOverwriteConfirm()
    {
        ClearPendingImportConfirmation();
        SetImportDataStatus("已取消本次覆盖导入。", ImportStatusStates.Info);
        StatusMessage = "已取消本次覆盖导入。";
        NotifyShellState();
    }

    [RelayCommand]
    private async Task ConfirmImportOverwriteAsync()
    {
        if (_pendingImportRequest is null || IsImportDataBusy)
        {
            return;
        }

        var cancellationToken = CancellationTokenSourceHelper.Refresh(ref _importCancellationTokenSource).Token;
        ImportDataBusyText = _pendingImportRequest.ImportBusyText;
        IsImportDataBusy = true;
        try
        {
            await ExecuteImportAsync(
                _pendingImportRequest.ImportAction,
                _pendingImportRequest.BuildSuccessMessage,
                _pendingImportRequest.ImportBusyText,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            IsImportDataBusy = false;
            NotifyShellState();
        }
    }

    private async Task ExecuteImportAsync(
        Func<CancellationToken, Task<IResultModel<ApiDocumentDto>>> importAction,
        Func<ApiDocumentDto, string> buildSuccessMessage,
        string importBusyText,
        CancellationToken cancellationToken)
    {
        ImportDataBusyText = importBusyText;
        var result = await importAction(cancellationToken);
        if (!result.IsSuccess || result.Data is null)
        {
            var failureMessage = string.IsNullOrWhiteSpace(result.Message)
                ? "Swagger 导入失败，请检查文档格式后重试。"
                : result.Message;
            SetImportDataStatus(failureMessage, ImportStatusStates.Error);
            StatusMessage = failureMessage;
            return;
        }

        ImportDataBusyText = "正在刷新导入结果...";
        await LoadImportedDocumentsAsync(manageBusyState: false);
        var successMessage = buildSuccessMessage(result.Data);
        ClearPendingImportConfirmation();
        SetImportDataStatus(successMessage, ImportStatusStates.Success);
        IsProjectImportDialogOpen = false;
        StatusMessage = successMessage;
    }

    private async Task LoadImportedDocumentsAsync(bool manageBusyState = true)
    {
        if (manageBusyState)
        {
            IsImportDataBusy = true;
        }

        var cancellationToken = CancellationTokenSourceHelper.Refresh(ref _importCancellationTokenSource).Token;
        try
        {
            var documents = await _apiWorkspaceService.GetDocumentsAsync(ProjectId, cancellationToken);
            var documentTasks = documents.Select(async document =>
            {
                var endpoints = await _apiWorkspaceService.GetEndpointsAsync(document.Id, cancellationToken);
                return (
                    Item: new ProjectImportedDocumentItemViewModel
                    {
                        Id = document.Id,
                        Name = document.Name,
                        SourceTypeText = ResolveImportSourceTypeText(document.SourceType),
                        SourceValueText = string.IsNullOrWhiteSpace(document.SourceValue) ? "-" : document.SourceValue,
                        BaseUrlText = string.IsNullOrWhiteSpace(document.BaseUrl) ? "未解析出 BaseUrl" : document.BaseUrl,
                        ImportedAtText = document.ImportedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                        EndpointCount = endpoints.Count
                    },
                    Endpoints: endpoints);
            });

            var results = await Task.WhenAll(documentTasks);
            await SyncImportedInterfacesAsync(results.SelectMany(result => result.Endpoints).ToList());
            ImportedApiDocuments.ReplaceWith(results.Select(result => result.Item));

            if (!HasImportedApiDocuments && ImportDataStatusState == ImportStatusStates.Info)
            {
                SetImportDataStatus("当前项目还没有导入 Swagger 数据，可先从文件或 URL 开始导入。", ImportStatusStates.Info);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            if (manageBusyState)
            {
                IsImportDataBusy = false;
            }
        }
    }

    private async Task SyncImportedInterfacesAsync(IReadOnlyList<ApiEndpointDto> endpoints)
    {
        await _requestCaseService.SyncImportedHttpInterfacesAsync(ProjectId, endpoints, CancellationToken.None);
        await ReloadSavedRequestsAsync();
    }

    private sealed class PendingImportRequest
    {
        public PendingImportRequest(
            Func<CancellationToken, Task<IResultModel<ApiDocumentDto>>> importAction,
            Func<ApiDocumentDto, string> buildSuccessMessage,
            string importBusyText)
        {
            ImportAction = importAction;
            BuildSuccessMessage = buildSuccessMessage;
            ImportBusyText = importBusyText;
        }

        public Func<CancellationToken, Task<IResultModel<ApiDocumentDto>>> ImportAction { get; }

        public Func<ApiDocumentDto, string> BuildSuccessMessage { get; }

        public string ImportBusyText { get; }
    }
}
