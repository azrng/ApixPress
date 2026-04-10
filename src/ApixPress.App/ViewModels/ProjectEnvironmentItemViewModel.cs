using CommunityToolkit.Mvvm.ComponentModel;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class ProjectEnvironmentItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private string id = string.Empty;

    [ObservableProperty]
    private string projectId = string.Empty;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string baseUrl = string.Empty;

    [ObservableProperty]
    private bool isActive;

    [ObservableProperty]
    private int sortOrder;

    public string DisplayName => IsActive ? $"{Name}（当前）" : Name;

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayName));
    }

    partial void OnIsActiveChanged(bool value)
    {
        OnPropertyChanged(nameof(DisplayName));
    }
}
