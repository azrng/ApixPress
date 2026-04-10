using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ApixPress.App.Models.DTOs;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class ExplorerItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string subtitle = string.Empty;

    [ObservableProperty]
    private bool isGroup;

    public ObservableCollection<ExplorerItemViewModel> Children { get; } = [];

    public ApiEndpointDto? Endpoint { get; init; }
}

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

    public RequestParameterKind ParameterType { get; init; }
}

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

    public RequestCaseDto SourceCase { get; init; } = new();
}

public partial class EnvironmentVariableItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private string id = string.Empty;

    [ObservableProperty]
    private string environmentName = "Default";

    [ObservableProperty]
    private string key = string.Empty;

    [ObservableProperty]
    private string value = string.Empty;

    [ObservableProperty]
    private bool isEnabled = true;
}

public partial class BodyModeOptionViewModel : ViewModelBase
{
    [ObservableProperty]
    private string mode = string.Empty;

    [ObservableProperty]
    private string displayName = string.Empty;
}
