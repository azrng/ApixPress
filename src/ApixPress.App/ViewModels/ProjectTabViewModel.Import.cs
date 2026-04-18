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
                    ? ImportTexts.PreviewFailureFallback
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
                    ImportTexts.FormatOverwriteDetectedStatus(previewResult.Data.ConflictCount),
                    ImportStatusStates.Info);
                StatusMessage = ImportTexts.OverwritePendingShellStatus;
                return;
            }

            await ExecuteImportAsync(importAction, buildSuccessMessage, importBusyText, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            HandleUnexpectedImportFailure(exception, ImportTexts.UnexpectedFailureFallback);
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
        SetImportDataStatus(ImportTexts.OverwriteCancelled, ImportStatusStates.Info);
        StatusMessage = ImportTexts.OverwriteCancelled;
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
        catch (Exception exception)
        {
            HandleUnexpectedImportFailure(exception, ImportTexts.UnexpectedFailureFallback);
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
                ? ImportTexts.ImportFailureFallback
                : result.Message;
            SetImportDataStatus(failureMessage, ImportStatusStates.Error);
            StatusMessage = failureMessage;
            return;
        }

        ImportDataBusyText = ImportTexts.BusyRefreshResult;
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
            var documentsTask = _apiWorkspaceService.GetDocumentsAsync(ProjectId, cancellationToken);
            var endpointsTask = _apiWorkspaceService.GetProjectEndpointsAsync(ProjectId, cancellationToken);
            await Task.WhenAll(documentsTask, endpointsTask);

            var documents = await documentsTask;
            var endpoints = await endpointsTask;
            var endpointCountByDocumentId = endpoints
                .GroupBy(item => item.DocumentId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

            await SyncImportedInterfacesAsync(endpoints);
            ImportedApiDocuments.ReplaceWith(documents.Select(document => new ProjectImportedDocumentItemViewModel
            {
                Id = document.Id,
                Name = document.Name,
                SourceTypeText = ResolveImportSourceTypeText(document.SourceType),
                SourceValueText = string.IsNullOrWhiteSpace(document.SourceValue) ? "-" : document.SourceValue,
                BaseUrlText = string.IsNullOrWhiteSpace(document.BaseUrl) ? ImportTexts.BaseUrlFallback : document.BaseUrl,
                ImportedAtText = document.ImportedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                EndpointCount = endpointCountByDocumentId.TryGetValue(document.Id, out var endpointCount)
                    ? endpointCount
                    : 0
            }));

            if (!HasImportedApiDocuments && ImportDataStatusState == ImportStatusStates.Info)
            {
                SetImportDataStatus(ImportTexts.EmptyStateStatus, ImportStatusStates.Info);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            HandleUnexpectedImportFailure(exception, ImportTexts.RefreshAfterImportFailure);
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

    private void HandleUnexpectedImportFailure(Exception exception, string fallbackMessage)
    {
        ClearPendingImportConfirmation();

        var failureMessage = string.IsNullOrWhiteSpace(exception.Message)
            ? fallbackMessage
            : $"{fallbackMessage} {exception.Message}";

        SetImportDataStatus(failureMessage, ImportStatusStates.Error);
        StatusMessage = failureMessage;
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
