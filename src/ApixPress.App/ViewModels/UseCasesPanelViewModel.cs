using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class UseCasesPanelViewModel : ViewModelBase
{
    private readonly IRequestCaseService _requestCaseService;

    public event Action<RequestSnapshotDto>? CaseApplied;

    public UseCasesPanelViewModel(IRequestCaseService requestCaseService)
    {
        _requestCaseService = requestCaseService;
    }

    public ObservableCollection<RequestCaseItemViewModel> RequestCases { get; } = [];

    [ObservableProperty]
    private string caseName = string.Empty;

    [ObservableProperty]
    private string caseGroupName = "默认分组";

    [ObservableProperty]
    private string caseDescription = string.Empty;

    [ObservableProperty]
    private RequestCaseItemViewModel? selectedRequestCase;

    public ObservableCollection<string> CaseTags { get; } = [];

    public async Task LoadCasesAsync()
    {
        RequestCases.Clear();
        var cases = await _requestCaseService.GetCasesAsync(CancellationToken.None);
        foreach (var requestCase in cases)
        {
            RequestCases.Add(new RequestCaseItemViewModel
            {
                Id = requestCase.Id,
                Name = requestCase.Name,
                GroupName = requestCase.GroupName,
                TagsText = string.Join(", ", requestCase.Tags),
                Description = requestCase.Description,
                UpdatedAt = requestCase.UpdatedAt.ToLocalTime(),
                SourceCase = requestCase
            });
        }
    }

    public void SetDefaultsFromEndpoint(ApiEndpointDto endpoint)
    {
        CaseName = endpoint.Name;
        CaseDescription = endpoint.Description;
        CaseGroupName = endpoint.GroupName;
    }

    public RequestCaseDto BuildCaseDto(string caseId, string requestName, RequestSnapshotDto snapshot)
    {
        return new RequestCaseDto
        {
            Id = caseId,
            Name = string.IsNullOrWhiteSpace(CaseName) ? requestName : CaseName,
            GroupName = CaseGroupName,
            Tags = CaseTags.Where(tag => !string.IsNullOrWhiteSpace(tag)).ToList(),
            Description = CaseDescription,
            RequestSnapshot = snapshot,
            UpdatedAt = DateTime.UtcNow
        };
    }

    [RelayCommand]
    public async Task SaveCaseAsync(RequestSnapshotDto snapshot)
    {
        var dto = BuildCaseDto(SelectedRequestCase?.Id ?? string.Empty, snapshot.Name, snapshot);
        var result = await _requestCaseService.SaveAsync(dto, CancellationToken.None);
        if (result.IsSuccess)
            await LoadCasesAsync();
    }

    [RelayCommand]
    private void LoadSelectedCase()
    {
        if (SelectedRequestCase is null) return;
        var snapshot = SelectedRequestCase.SourceCase.RequestSnapshot;
        CaseName = SelectedRequestCase.SourceCase.Name;
        CaseGroupName = SelectedRequestCase.SourceCase.GroupName;
        ReplaceCaseTags(SelectedRequestCase.SourceCase.Tags);
        CaseDescription = SelectedRequestCase.SourceCase.Description;
        CaseApplied?.Invoke(snapshot);
    }

    [RelayCommand]
    private async Task DuplicateSelectedCaseAsync()
    {
        if (SelectedRequestCase is null) return;
        var result = await _requestCaseService.DuplicateAsync(SelectedRequestCase.Id, CancellationToken.None);
        if (result.IsSuccess)
            await LoadCasesAsync();
    }

    [RelayCommand]
    private async Task DeleteSelectedCaseAsync()
    {
        if (SelectedRequestCase is null) return;

        await _requestCaseService.DeleteAsync(SelectedRequestCase.Id, CancellationToken.None);
        SelectedRequestCase = null;
        await LoadCasesAsync();
    }

    private void ReplaceCaseTags(IEnumerable<string> tags)
    {
        CaseTags.Clear();
        foreach (var tag in tags.Where(tag => !string.IsNullOrWhiteSpace(tag)))
        {
            CaseTags.Add(tag);
        }
    }
}
