using CommunityToolkit.Mvvm.ComponentModel;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class ProjectWorkspaceItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private string id = string.Empty;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string description = string.Empty;

    [ObservableProperty]
    private bool isDefault;

    public string DisplayName => IsDefault ? $"{Name}（默认）" : Name;
    public string SummaryText => string.IsNullOrWhiteSpace(Description) ? "暂无项目说明" : Description;
    public string CategoryText => "HTTP";

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayName));
    }

    partial void OnDescriptionChanged(string value)
    {
        OnPropertyChanged(nameof(SummaryText));
    }

    partial void OnIsDefaultChanged(bool value)
    {
        OnPropertyChanged(nameof(DisplayName));
    }
}
