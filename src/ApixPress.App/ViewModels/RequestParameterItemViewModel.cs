using CommunityToolkit.Mvvm.ComponentModel;
using ApixPress.App.Models.DTOs;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class RequestParameterItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string value = string.Empty;

    [ObservableProperty]
    private string description = string.Empty;

    [ObservableProperty]
    private bool isRequired;

    [ObservableProperty]
    private bool isEnabled = true;

    public RequestParameterKind ParameterType { get; init; }
}
