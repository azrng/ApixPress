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

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayName));
    }

    partial void OnIsDefaultChanged(bool value)
    {
        OnPropertyChanged(nameof(DisplayName));
    }
}
