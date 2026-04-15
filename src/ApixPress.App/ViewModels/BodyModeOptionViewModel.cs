using CommunityToolkit.Mvvm.ComponentModel;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class BodyModeOptionViewModel : ViewModelBase
{
    [ObservableProperty]
    private string mode = string.Empty;

    [ObservableProperty]
    private string displayName = string.Empty;
}
