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
    }

    public void ClearProjectContext()
    {
        _currentProjectId = string.Empty;
        HistoryItems.Clear();
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
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
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
        HistoryItems.Clear();
    }

    partial void OnSearchTextChanged(string value)
    {
        // Trigger re-filter if needed
    }

    private static RequestHistoryItemViewModel CreateHistoryItem(RequestHistoryItemDto item)
    {
        var snapshot = item.RequestSnapshot;
        var response = item.ResponseSnapshot;
        return new RequestHistoryItemViewModel
        {
            Id = item.Id,
            Method = snapshot.Method,
            Url = snapshot.Url,
            Timestamp = item.Timestamp,
            HasResponse = response is not null,
            StatusText = response?.StatusCode?.ToString() ?? "-",
            DurationText = response?.DurationMs > 0 ? $"{response.DurationMs}ms" : "-",
            SizeText = response?.SizeBytes > 0 ? UiFormatHelper.FormatBytes(response.SizeBytes) : "-",
            RequestSnapshot = snapshot,
            ResponseSnapshot = response
        };
    }
}
