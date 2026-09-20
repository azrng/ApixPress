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
    private const int MaxHistoryItems = 50;

    private readonly IRequestHistoryService _requestHistoryService;
    private CancellationTokenSource? _loadHistoryCancellationTokenSource;
    private CancellationTokenSource? _searchDebounceCancellationTokenSource;
    private string _currentProjectId = string.Empty;
    private bool _hasLoadedHistory;

    public BatchObservableCollection<RequestHistoryItemViewModel> HistoryItems { get; } = [];
    public BatchObservableCollection<RequestHistoryItemViewModel> VisibleHistoryItems { get; } = [];

    /// <summary>扁平化显示集合：日期分组行与记录行交替（对齐原型 h-group-cap 分组）</summary>
    public BatchObservableCollection<object> HistoryDisplayItems { get; } = [];

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

    [ObservableProperty]
    private bool isClearHistoryArmed;

    public RequestHistoryPanelViewModel(IRequestHistoryService requestHistoryService)
    {
        _requestHistoryService = requestHistoryService;
    }

    protected override void DisposeManaged()
    {
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
        RefreshVisibleHistoryItems();
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
            RefreshVisibleHistoryItems();
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

    /// <summary>
    /// 新请求写入历史：无搜索时走差量路径（头部插入 + 尾部裁剪），
    /// 避免每条新记录都触发可见与展示两条集合的全量 Reset 重建。
    /// </summary>
    public void PrependHistoryItem(RequestHistoryItemDto item)
    {
        var viewModel = CreateHistoryItem(item);
        var existing = HistoryItems.FirstOrDefault(historyItem => string.Equals(historyItem.Id, item.Id, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            HistoryItems.Remove(existing);
        }

        // 差量路径的收益监控点：发送请求后的历史列表 UI 同步耗时
        PerfLog.Measure("历史·前置插入", () =>
        {
            HistoryItems.Insert(0, viewModel);
            while (HistoryItems.Count > MaxHistoryItems)
            {
                HistoryItems.RemoveAt(HistoryItems.Count - 1);
            }

            if (HasSearchText)
            {
                RefreshVisibleHistoryItems();
                return;
            }

            if (existing is not null)
            {
                VisibleHistoryItems.Remove(existing);
            }

            VisibleHistoryItems.Insert(0, viewModel);
            while (VisibleHistoryItems.Count > MaxHistoryItems)
            {
                VisibleHistoryItems.RemoveAt(VisibleHistoryItems.Count - 1);
            }

            SyncHistoryDisplayItems();
            NotifyHistoryVisibilityChanged();
        });
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
        RefreshVisibleHistoryItems();
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

    /// <summary>可见/展示集合的全量重算入口：加载、清空、搜索场景走这里（展示集合仍按差量应用）。</summary>
    private void RefreshVisibleHistoryItems()
    {
        var keyword = SearchText.Trim();
        VisibleHistoryItems.ReplaceWith(string.IsNullOrWhiteSpace(keyword)
            ? HistoryItems
            : HistoryItems.Where(item => MatchesSearch(item, keyword)));

        SyncHistoryDisplayItems();
        NotifyHistoryVisibilityChanged();
    }

    /// <summary>按当前可见集合重算期望的分组展示行，并以差量方式应用到展示集合。</summary>
    private void SyncHistoryDisplayItems()
    {
        ApplyDisplayItemsDifferentially(BuildHistoryDisplayItems());
    }

    /// <summary>
    /// 按本地日期把可见记录切分为 今天 / 昨天 / 更早日期 的分组行 + 记录行
    /// </summary>
    private List<object> BuildHistoryDisplayItems()
    {
        var displayItems = new List<object>(VisibleHistoryItems.Count + 4);
        var today = DateTime.Today;
        var index = 0;
        while (index < VisibleHistoryItems.Count)
        {
            var localDate = VisibleHistoryItems[index].Timestamp.ToLocalTime().Date;
            var groupStart = index;
            while (index < VisibleHistoryItems.Count && VisibleHistoryItems[index].Timestamp.ToLocalTime().Date == localDate)
            {
                index++;
            }

            displayItems.Add(new HistoryDateGroupHeaderViewModel(ResolveDateGroupTitle(localDate, today), index - groupStart));
            for (var itemIndex = groupStart; itemIndex < index; itemIndex++)
            {
                displayItems.Add(VisibleHistoryItems[itemIndex]);
            }
        }

        return displayItems;
    }

    /// <summary>
    /// 期望行与现有展示行的指针合并：记录行按引用匹配、分组头按标题匹配，
    /// 只对差异位置做 Insert/Remove/Replace——高频的新增/删除单条历史不再整表 Reset。
    /// </summary>
    private void ApplyDisplayItemsDifferentially(IReadOnlyList<object> desired)
    {
        var target = HistoryDisplayItems;
        var consumed = 0;
        for (var index = 0; index < desired.Count; index++)
        {
            var desiredItem = desired[index];
            var matchIndex = FindDisplayMatch(target, desiredItem, consumed);
            if (matchIndex < 0)
            {
                target.Insert(consumed, desiredItem);
                consumed++;
                continue;
            }

            // 每删一行，匹配位置随之左移：matchIndex 同步递减，只移除真正过期的行
            while (consumed < matchIndex)
            {
                target.RemoveAt(consumed);
                matchIndex--;
            }

            // 分组头不可变：计数变化用新实例 Replace，避免引入可变状态
            if (desiredItem is HistoryDateGroupHeaderViewModel header
                && target[consumed] is HistoryDateGroupHeaderViewModel existing
                && existing.Count != header.Count)
            {
                target[consumed] = header;
            }

            consumed++;
        }

        while (target.Count > consumed)
        {
            target.RemoveAt(target.Count - 1);
        }
    }

    private static int FindDisplayMatch(BatchObservableCollection<object> target, object desiredItem, int startIndex)
    {
        if (desiredItem is HistoryDateGroupHeaderViewModel header)
        {
            for (var index = startIndex; index < target.Count; index++)
            {
                if (target[index] is HistoryDateGroupHeaderViewModel existing
                    && string.Equals(existing.Title, header.Title, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        for (var index = startIndex; index < target.Count; index++)
        {
            if (ReferenceEquals(target[index], desiredItem))
            {
                return index;
            }
        }

        return -1;
    }

    private static string ResolveDateGroupTitle(DateTime date, DateTime today)
    {
        if (date == today)
        {
            return "今天";
        }

        if (date == today.AddDays(-1))
        {
            return "昨天";
        }

        return date.ToString("yyyy-MM-dd");
    }

    [RelayCommand]
    private async Task ArmClearHistoryAsync()
    {
        // 两段式确认：第一次点击进入待确认态，3 秒未确认自动还原
        if (IsClearHistoryArmed)
        {
            return;
        }

        IsClearHistoryArmed = true;
        try
        {
            await Task.Delay(3000);
        }
        finally
        {
            if (!IsDisposed)
            {
                IsClearHistoryArmed = false;
            }
        }
    }

    [RelayCommand]
    public async Task DeleteHistoryItemAsync(RequestHistoryItemViewModel? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(_currentProjectId))
        {
            return;
        }

        var result = await _requestHistoryService.DeleteAsync(_currentProjectId, item.Id, CancellationToken.None);
        if (!result.IsSuccess)
        {
            return;
        }

        if (ReferenceEquals(SelectedHistoryItem, item))
        {
            SelectedHistoryItem = null;
        }

        HistoryItems.Remove(item);
        if (HasSearchText)
        {
            RefreshVisibleHistoryItems();
            return;
        }

        // 无搜索时单条删除走差量：展示集合只移除一行并更新所属分组头计数
        VisibleHistoryItems.Remove(item);
        SyncHistoryDisplayItems();
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
