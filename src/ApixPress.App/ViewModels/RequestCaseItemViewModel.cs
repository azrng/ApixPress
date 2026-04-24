using CommunityToolkit.Mvvm.ComponentModel;
using ApixPress.App.Models.DTOs;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class RequestCaseItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private string id = string.Empty;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string groupName = string.Empty;

    [ObservableProperty]
    private string tagsText = string.Empty;

    [ObservableProperty]
    private string description = string.Empty;

    [ObservableProperty]
    private DateTime updatedAt;

    public RequestCaseDto SourceCase { get; set; } = new();

    public string UpdatedAtText => $"更新于 {UpdatedAt:MM-dd HH:mm}";

    partial void OnUpdatedAtChanged(DateTime value)
    {
        OnPropertyChanged(nameof(UpdatedAtText));
    }

    public void ApplyDetail(RequestCaseDto detail)
    {
        SourceCase = detail;
    }
}
