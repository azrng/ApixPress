using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApixPress.App.Helpers;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class RequestHistoryPanelViewModel : ViewModelBase
{
    private readonly IRequestHistoryService _requestHistoryService;
    private CancellationTokenSource? _loadHistoryCancellationTokenSource;
    private string _currentProjectId = string.Empty;
    private bool _hasLoadedHistory;

    public ObservableCollection<RequestHistoryItemViewModel> HistoryItems { get; } = [];

    [ObservableProperty]
    private string searchText = string.Empty;

    public RequestHistoryPanelViewModel(IRequestHistoryService requestHistoryService)
    {
        _requestHistoryService = requestHistoryService;
    }

    protected override void DisposeManaged()
    {
        CancellationTokenSourceHelper.CancelAndDispose(ref _loadHistoryCancellationTokenSource);
    }

    public void SetProjectContext(string projectId)
    {
        _currentProjectId = projectId;
        _hasLoadedHistory = false;
    }

    public void ClearProjectContext()
    {
        _currentProjectId = string.Empty;
        _hasLoadedHistory = false;
        HistoryItems.Clear();
    }

    public async Task EnsureHistoryLoadedAsync()
    {
        if (_hasLoadedHistory)
        {
            return;
        }

        await LoadHistoryAsync();
    }

    public async Task LoadHistoryAsync()
    {
        if (IsDisposed)
        {
            return;
        }

        var cancellationToken = CancellationTokenSourceHelper.Refresh(ref _loadHistoryCancellationTokenSource).Token;
        try
        {
            HistoryItems.Clear();
            if (string.IsNullOrWhiteSpace(_currentProjectId))
            {
                return;
            }

            var history = await _requestHistoryService.GetHistoryAsync(_currentProjectId, cancellationToken);
            HistoryItems.ReplaceWith(history.Select(CreateHistoryItem));
            _hasLoadedHistory = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    public async Task<RequestHistoryItemDto?> EnsureHistoryDetailLoadedAsync(RequestHistoryItemViewModel item)
    {
        if (item.HasLoadedDetail)
        {
            return new RequestHistoryItemDto
            {
                Id = item.Id,
                Timestamp = item.Timestamp,
                HasResponse = item.HasResponse,
                StatusCode = ParseStatusCode(item.StatusText),
                DurationMs = ParseDurationMilliseconds(item.DurationText),
                SizeBytes = 0,
                RequestSnapshot = item.RequestSnapshot,
                ResponseSnapshot = item.ResponseSnapshot
            };
        }

        var cancellationToken = CancellationTokenSourceHelper.Refresh(ref _loadHistoryCancellationTokenSource).Token;
        try
        {
            var detail = await _requestHistoryService.GetDetailAsync(_currentProjectId, item.Id, cancellationToken);
            if (detail is null)
            {
                return null;
            }

            item.ApplyDetail(detail);
            return detail;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    public void PrependHistoryItem(RequestHistoryItemDto item)
    {
        var viewModel = CreateHistoryItem(item);
        var existing = HistoryItems.FirstOrDefault(historyItem => string.Equals(historyItem.Id, item.Id, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            HistoryItems.Remove(existing);
        }

        HistoryItems.Insert(0, viewModel);
        while (HistoryItems.Count > 50)
        {
            HistoryItems.RemoveAt(HistoryItems.Count - 1);
        }
    }

    [RelayCommand]
    public async Task ClearHistoryAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentProjectId))
        {
            return;
        }

        await _requestHistoryService.ClearAsync(_currentProjectId, CancellationToken.None);
        _hasLoadedHistory = true;
        HistoryItems.Clear();
    }

    partial void OnSearchTextChanged(string value)
    {
        // Trigger re-filter if needed
    }

    private static RequestHistoryItemViewModel CreateHistoryItem(RequestHistoryItemDto item)
    {
        var snapshot = item.RequestSnapshot;
        return new RequestHistoryItemViewModel
        {
            Id = item.Id,
            Method = snapshot.Method,
            Url = snapshot.Url,
            Timestamp = item.Timestamp,
            HasResponse = item.HasResponse,
            StatusText = item.StatusCode?.ToString() ?? "-",
            DurationText = item.DurationMs > 0 ? $"{item.DurationMs}ms" : "-",
            SizeText = item.SizeBytes > 0 ? UiFormatHelper.FormatBytes(item.SizeBytes) : "-",
            RequestSnapshot = snapshot,
            ResponseSnapshot = null
        };
    }

    private static int? ParseStatusCode(string value)
    {
        return int.TryParse(value, out var statusCode) ? statusCode : null;
    }

    private static long ParseDurationMilliseconds(string value)
    {
        return value.EndsWith("ms", StringComparison.OrdinalIgnoreCase)
               && long.TryParse(value[..^2], out var duration)
            ? duration
            : 0;
    }
}
