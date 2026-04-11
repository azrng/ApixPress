using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ApixPress.App.Models.DTOs;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class ExplorerItemViewModel : ViewModelBase
{
    public ExplorerItemViewModel()
    {
        Children.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasChildren));
    }

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string subtitle = string.Empty;

    [ObservableProperty]
    private bool isGroup;

    [ObservableProperty]
    private string nodeType = string.Empty;

    [ObservableProperty]
    private bool canLoad;

    [ObservableProperty]
    private bool isExpanded = true;

    public ObservableCollection<ExplorerItemViewModel> Children { get; } = [];
    public bool HasChildren => Children.Count > 0;

    public RequestCaseDto? SourceCase { get; init; }
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
    public string UpdatedAtText => $"更新于 {UpdatedAt:MM-dd HH:mm}";

    partial void OnUpdatedAtChanged(DateTime value)
    {
        OnPropertyChanged(nameof(UpdatedAtText));
    }
}

public partial class EnvironmentVariableItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private string id = string.Empty;

    [ObservableProperty]
    private string environmentId = string.Empty;

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
