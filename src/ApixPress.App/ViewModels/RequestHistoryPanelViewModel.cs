using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels.Base;
using ApixPress.App.Helpers;

namespace ApixPress.App.ViewModels;

public partial class RequestHistoryPanelViewModel : ViewModelBase
{
    private readonly IRequestHistoryService _requestHistoryService;
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
        HistoryItems.Clear();
        if (string.IsNullOrWhiteSpace(_currentProjectId))
        {
            return;
        }

        var history = await _requestHistoryService.GetHistoryAsync(_currentProjectId, CancellationToken.None);
        foreach (var item in history)
        {
            var snapshot = item.RequestSnapshot;
            var response = item.ResponseSnapshot;

            HistoryItems.Add(new RequestHistoryItemViewModel
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
            });
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
