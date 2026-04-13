using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ApixPress.App.Models.DTOs;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class ExplorerItemViewModel : ViewModelBase
{
    public ExplorerItemViewModel()
    {
        Children.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasChildren));
            OnPropertyChanged(nameof(IsClickable));
        };
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
    public bool IsClickable => CanLoad || HasChildren;
    public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);
    public bool ShowMethodBadge => string.Equals(NodeType, "http-interface", StringComparison.OrdinalIgnoreCase);
    public bool ShowLeadingGlyph => !ShowMethodBadge && !IsHttpCaseNode;
    public bool IsHttpCaseNode => string.Equals(NodeType, "http-case", StringComparison.OrdinalIgnoreCase);
    public bool IsQuickRequestNode => string.Equals(NodeType, "quick-request", StringComparison.OrdinalIgnoreCase);
    public bool ShowTrailingDot => string.Equals(NodeType, "http-interface", StringComparison.OrdinalIgnoreCase) && HasChildren;
    public string MethodBadgeText => SourceCase?.RequestSnapshot.Method?.ToUpperInvariant()
        ?? Endpoint?.Method?.ToUpperInvariant()
        ?? string.Empty;
    public string MethodBadgeClass => MethodBadgeText switch
    {
        "GET" => "Light Success",
        "POST" => "Light Primary",
        "PUT" => "Light Warning",
        "DELETE" => "Light Danger",
        "PATCH" => "Light Secondary",
        _ => "Light Secondary"
    };
    public string NodeGlyph => NodeType switch
    {
        "module" => "\uE8B7",
        "interface-root" => "\uE71D",
        "folder" => "\uE8B7",
        "http-case" => "\uE7C3",
        "quick-root" => "\uE945",
        "quick-request" => "\uE8A5",
        _ => "\uE8A5"
    };

    public RequestCaseDto? SourceCase { get; init; }
    public ApiEndpointDto? Endpoint { get; init; }

    partial void OnCanLoadChanged(bool value)
    {
        OnPropertyChanged(nameof(IsClickable));
    }

    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsExpanded));
    }
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
