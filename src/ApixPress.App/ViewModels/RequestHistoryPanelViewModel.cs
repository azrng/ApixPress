using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApixPress.App.Helpers;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels.Base;
using Azrng.Core.Results;

namespace ApixPress.App.ViewModels;

public partial class RequestHistoryPanelViewModel : ViewModelBase
{
    private const int SearchDebounceMilliseconds = 250;

    private readonly IRequestHistoryService _requestHistoryService;
    private CancellationTokenSource? _loadHistoryCancellationTokenSource;
    private CancellationTokenSource? _searchDebounceCancellationTokenSource;
    private string _currentProjectId = string.Empty;
    private bool _hasLoadedHistory;

    public BatchObservableCollection<RequestHistoryItemViewModel> HistoryItems { get; } = [];
    public BatchObservableCollection<RequestHistoryItemViewModel> VisibleHistoryItems { get; } = [];

    public bool HasHistory => HistoryItems.Count > 0;
    public bool HasVisibleHistory => VisibleHistoryItems.Count > 0;
    public bool HasSearchText => !string.IsNullOrWhiteSpace(SearchText);
    public bool ShowHistoryEmptyState => !HasHistory;
    public bool ShowHistorySearchEmptyState => HasHistory && HasSearchText && !HasVisibleHistory;
    public string HistoryEmptyStateText => HasSearchText ? "没有匹配的请求历史" : "还没有发送记录";
    public bool HasSelectedHistory => SelectedHistoryItem is not null;

    public ResponseSectionViewModel SelectedResponseSection { get; } = new();

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private bool isHistoryLoading;

    [ObservableProperty]
    private RequestHistoryItemViewModel? selectedHistoryItem;

    public RequestHistoryPanelViewModel(IRequestHistoryService requestHistoryService)
    {
        _requestHistoryService = requestHistoryService;
        HistoryItems.CollectionChanged += OnHistoryItemsCollectionChanged;
    }

    protected override void DisposeManaged()
    {
        HistoryItems.CollectionChanged -= OnHistoryItemsCollectionChanged;
        CancellationTokenSourceHelper.CancelAndDispose(ref _loadHistoryCancellationTokenSource);
        CancellationTokenSourceHelper.CancelAndDispose(ref _searchDebounceCancellationTokenSource);
        SelectedResponseSection.Dispose();
    }

    public void SetProjectContext(string projectId)
    {
        _currentProjectId = projectId;
        _hasLoadedHistory = false;
        SearchText = string.Empty;
        SelectedHistoryItem = null;
    }

    public void ClearProjectContext()
    {
        _currentProjectId = string.Empty;
        _hasLoadedHistory = false;
        SearchText = string.Empty;
        SelectedHistoryItem = null;
        HistoryItems.Clear();
        VisibleHistoryItems.Clear();
        NotifyHistoryVisibilityChanged();
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
        IsHistoryLoading = true;
        try
        {
            HistoryItems.Clear();
            if (string.IsNullOrWhiteSpace(_currentProjectId))
            {
                RefreshVisibleHistoryItems();
                return;
            }

            var history = await _requestHistoryService.GetHistoryAsync(_currentProjectId, cancellationToken);
            HistoryItems.ReplaceWith(history.Select(CreateHistoryItem));
            _hasLoadedHistory = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            IsHistoryLoading = false;
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
        SearchText = string.Empty;
        SelectedHistoryItem = null;
        HistoryItems.Clear();
    }

    partial void OnSelectedHistoryItemChanged(RequestHistoryItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelectedHistory));
        SelectedResponseSection.Reset();

        if (value is not null)
        {
            _ = LoadSelectedHistoryDetailAsync(value);
        }
    }

    private async Task LoadSelectedHistoryDetailAsync(RequestHistoryItemViewModel item)
    {
        var detail = await EnsureHistoryDetailLoadedAsync(item);
        if (IsDisposed || !ReferenceEquals(SelectedHistoryItem, item) || detail?.ResponseSnapshot is null)
        {
            return;
        }

        SelectedResponseSection.ApplyResult(
            ResultModel<ResponseSnapshotDto>.Success(detail.ResponseSnapshot),
            detail.RequestSnapshot);
    }

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasSearchText));
        OnPropertyChanged(nameof(HistoryEmptyStateText));
        _ = DebounceSearchAsync();
    }

    private async Task DebounceSearchAsync()
    {
        var cancellationToken = CancellationTokenSourceHelper
            .Refresh(ref _searchDebounceCancellationTokenSource)
            .Token;

        try
        {
            await Task.Delay(SearchDebounceMilliseconds, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (IsDisposed)
        {
            return;
        }

        RefreshVisibleHistoryItems();
    }

    private void OnHistoryItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        RefreshVisibleHistoryItems();
    }

    private void RefreshVisibleHistoryItems()
    {
        var keyword = SearchText.Trim();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            VisibleHistoryItems.ReplaceWith(HistoryItems);
        }
        else
        {
            VisibleHistoryItems.ReplaceWith(HistoryItems.Where(item => MatchesSearch(item, keyword)));
        }

        NotifyHistoryVisibilityChanged();
    }

    private void NotifyHistoryVisibilityChanged()
    {
        OnPropertyChanged(nameof(HasHistory));
        OnPropertyChanged(nameof(HasVisibleHistory));
        OnPropertyChanged(nameof(ShowHistoryEmptyState));
        OnPropertyChanged(nameof(ShowHistorySearchEmptyState));
        OnPropertyChanged(nameof(HistoryEmptyStateText));
    }

    private static bool MatchesSearch(RequestHistoryItemViewModel item, string keyword)
    {
        return Contains(item.Method, keyword)
            || Contains(item.Url, keyword)
            || Contains(item.StatusText, keyword)
            || Contains(item.TimestampText, keyword);
    }

    private static bool Contains(string value, string keyword)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
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
