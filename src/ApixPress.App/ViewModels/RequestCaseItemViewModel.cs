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

    // 用例抽屉内：该卡片是否被选中加载
    [ObservableProperty]
    private bool isSelected;

    public RequestCaseDto SourceCase { get; set; } = new();

    public string UpdatedAtText => $"更新于 {UpdatedAt:MM-dd HH:mm}";
    public string Method => SourceCase.RequestSnapshot.Method.ToUpperInvariant();
    public string RequestUrlText => SourceCase.RequestSnapshot.Url;
    public string MethodBadgeClass => Method switch
    {
        "GET" => "Light Success",
        "POST" => "Light Warning",
        "PUT" => "Light Primary",
        "DELETE" => "Light Danger",
        "PATCH" => "Light Success",
        _ => "Light Secondary"
    };

    partial void OnUpdatedAtChanged(DateTime value)
    {
        OnPropertyChanged(nameof(UpdatedAtText));
    }

    public void ApplyDetail(RequestCaseDto detail)
    {
        SourceCase = detail;
        Name = detail.Name;
        GroupName = detail.GroupName;
        TagsText = string.Join(", ", detail.Tags);
        Description = detail.Description;
        UpdatedAt = detail.UpdatedAt.ToLocalTime();
        OnPropertyChanged(nameof(Method));
        OnPropertyChanged(nameof(RequestUrlText));
        OnPropertyChanged(nameof(MethodBadgeClass));
    }
}
