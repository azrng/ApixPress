using System.Collections.ObjectModel;
using System.Windows.Input;
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
            OnPropertyChanged(nameof(CanDelete));
            OnPropertyChanged(nameof(ShowTrailingDot));
        };
    }

    public string NodeKey { get; set; } = string.Empty;

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

    public bool CanDelete => SourceCase is not null || Children.Any(child => child.CanDelete);

    public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);

    public string DisplayTitle => !string.IsNullOrWhiteSpace(Title)
        ? Title
        : NodeType switch
        {
            "http-interface" => "未命名接口",
            "http-case" => "未命名用例",
            "folder" => "未命名目录",
            "quick-request" => "未命名请求",
            _ => "未命名项"
        };

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

    public ICommand? DeleteCommand { get; set; }

    public RequestCaseDto? SourceCase { get; set; }

    public ApiEndpointDto? Endpoint { get; set; }

    public void SyncFrom(ExplorerItemViewModel source)
    {
        NodeKey = source.NodeKey;
        Title = source.Title;
        Subtitle = source.Subtitle;
        IsGroup = source.IsGroup;
        NodeType = source.NodeType;
        CanLoad = source.CanLoad;
        DeleteCommand = source.DeleteCommand;
        SourceCase = source.SourceCase;
        Endpoint = source.Endpoint;

        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(MethodBadgeText));
        OnPropertyChanged(nameof(MethodBadgeClass));
    }

    partial void OnCanLoadChanged(bool value)
    {
        OnPropertyChanged(nameof(IsClickable));
    }

    partial void OnTitleChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayTitle));
    }

    partial void OnNodeTypeChanged(string value)
    {
        OnPropertyChanged(nameof(ShowMethodBadge));
        OnPropertyChanged(nameof(ShowLeadingGlyph));
        OnPropertyChanged(nameof(IsHttpCaseNode));
        OnPropertyChanged(nameof(IsQuickRequestNode));
        OnPropertyChanged(nameof(ShowTrailingDot));
        OnPropertyChanged(nameof(NodeGlyph));
        OnPropertyChanged(nameof(DisplayTitle));
    }
}
