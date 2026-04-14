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
        var cancellationToken = CancellationTokenSourceHelper.Refresh(ref _loadHistoryCancellationTokenSource).Token;
        try
        {
            HistoryItems.Clear();
            if (string.IsNullOrWhiteSpace(_currentProjectId))
            {
                return;
            }

            var history = await _requestHistoryService.GetHistoryAsync(_currentProjectId, cancellationToken);
            HistoryItems.ReplaceWith(history.Select(item =>
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
            }));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
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
        await LoadHistoryAsync();
    }

    partial void OnSearchTextChanged(string value)
    {
        // Trigger re-filter if needed
    }
}
