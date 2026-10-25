using System.Windows.Input;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using ApixPress.App.Helpers;
using ApixPress.App.Models.DTOs;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class ExplorerItemViewModel : ViewModelBase
{
    private Func<IReadOnlyList<ExplorerItemViewModel>>? _childLoader;
    private bool _childrenLoaded = true;

    public ExplorerItemViewModel()
    {
        Children.CollectionChanged += OnChildrenCollectionChanged;
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

    /// <summary>接口节点关联的用例数（对齐原型 08 的 "(N 个用例)" 提示）；点击行内芯片打开用例抽屉</summary>
    [ObservableProperty]
    private int caseCount;

    public bool HasCases => CaseCount > 0;

    /// <summary>保存/改名用例后由目录定向调用：更新计数并广播 HasCases，避免整树重建。</summary>
    public void UpdateCaseCount(int count)
    {
        CaseCount = count;
        OnPropertyChanged(nameof(HasCases));
    }

    [ObservableProperty]
    private bool isExpanded;

    /// <summary>子节点集合：延迟加载的大组整体替换时只发一次 Reset（否则每个 Add 都向所有订阅者广播一次）。</summary>
    public BatchObservableCollection<ExplorerItemViewModel> Children { get; } = [];

    public bool HasChildren => Children.Count > 0 || HasDeferredChildren;

    public bool HasDeferredChildren => _childLoader is not null && !_childrenLoaded;

    public bool AreChildrenLoaded => _childrenLoaded || _childLoader is null;

    public bool IsClickable => CanLoad || HasChildren;

    public bool CanDelete => SourceCase is not null
        || (string.Equals(NodeType, "folder", StringComparison.OrdinalIgnoreCase) && HasChildren)
        || Children.Any(child => child.CanDelete);

    public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);

    public string DisplayTitle => !string.IsNullOrWhiteSpace(Title)
        ? Title
        : NodeType switch
        {
            "http-interface" => "未命名接口",
            "folder" => "未命名目录",
            "quick-request" => "未命名请求",
            _ => "未命名项"
        };

    // 目录节点在标题后追加子项计数（对标原型的 "EfCoreSample (7)" 展示）
    public bool HasChildrenCount => HasChildren && AreChildrenLoaded && Children.Count > 0;

    public string ChildrenCountSuffix => HasChildrenCount ? $" ({Children.Count})" : string.Empty;

    // 目录/接口标题由树构建器统一附带 "(n)" 计数；接口名称若已带方法前缀，侧栏只保留一份
    public string SidebarDisplayText => ShowMethodBadge
        ? RemoveMethodPrefix(DisplayTitle, MethodBadgeText)
        : DisplayTitle;

    public bool ShowMethodBadge => string.Equals(NodeType, "http-interface", StringComparison.OrdinalIgnoreCase)
        || IsRequestCaseNode;

    public bool ShowLeadingGlyph => !ShowMethodBadge;

    public bool IsQuickRequestNode => string.Equals(NodeType, "quick-request", StringComparison.OrdinalIgnoreCase);

    public bool IsRequestCaseNode => string.Equals(NodeType, ProjectTabRequestEntryTypes.HttpCase, StringComparison.OrdinalIgnoreCase);

    public string MethodBadgeText => SourceCase?.RequestSnapshot.Method?.ToUpperInvariant()
        ?? Endpoint?.Method?.ToUpperInvariant()
        ?? string.Empty;

    public string MethodBadgeClass => MethodBadgeText switch
    {
        "GET" => "HttpMethodTextLabel HttpMethodGet",
        "POST" => "HttpMethodTextLabel HttpMethodPost",
        "PUT" => "HttpMethodTextLabel HttpMethodPut",
        "DELETE" => "HttpMethodTextLabel HttpMethodDelete",
        "PATCH" => "HttpMethodTextLabel HttpMethodPatch",
        _ => "HttpMethodTextLabel HttpMethodOther"
    };

    private static string RemoveMethodPrefix(string title, string method)
    {
        if (string.IsNullOrWhiteSpace(method))
        {
            return title;
        }

        var prefix = $"{method} ";
        return title.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? title[prefix.Length..]
            : title;
    }

    public string NodeGlyph => NodeType switch
    {
        "module" => "\uE8B7",
        "interface-root" => "\uE71D",
        "folder" => "\uE8B7",
        "quick-root" => "\uE945",
        "quick-request" => "\uE8A5",
        _ => "\uE8A5"
    };

    public ICommand? DeleteCommand { get; set; }

    /// <summary>请求用例节点的改名命令，直接注入节点以跨 ContextMenu PopupRoot 保持绑定可用。</summary>
    public ICommand? RenameCommand { get; set; }

    /// <summary>请求用例节点的复制命令，直接注入节点以跨 ContextMenu PopupRoot 保持绑定可用。</summary>
    public ICommand? DuplicateCommand { get; set; }

    /// <summary>在当前目录下新建子目录的命令；仅一级目录节点会携带并展示。</summary>
    public ICommand? CreateSubfolderCommand { get; set; }

    /// <summary>在当前目录下新建 HTTP 接口的命令，新建的接口会归属该目录。</summary>
    public ICommand? CreateInterfaceInFolderCommand { get; set; }

    /// <summary>目录节点的完整路径（如 "a/b"），根级目录为单段名称。</summary>
    public string FolderFullPath { get; set; } = string.Empty;

    /// <summary>目录层级：1 为一级目录，2 为二级目录；非目录节点为 0。</summary>
    public int FolderDepth { get; set; }

    /// <summary>扁平列表展示层级：根级为 0，子级递增；由扁平化逻辑在行入列时写入。</summary>
    public int DisplayDepth { get; set; }

    /// <summary>扁平列表行缩进（接口列表用 ListBox+VirtualizingStackPanel 替代 TreeView 后的层级视觉）。</summary>
    public Thickness RowIndentMargin => new(DisplayDepth * 14, 0, 0, 0);

    public bool IsFolderNode => string.Equals(NodeType, "folder", StringComparison.OrdinalIgnoreCase);

    /// <summary>接口树最多两级目录，仅一级目录允许继续新建子目录。</summary>
    public bool CanCreateSubfolder => IsFolderNode && FolderDepth < ProjectWorkspaceTreeBuilder.MaxFolderDepth;

    public RequestCaseDto? SourceCase { get; set; }

    /// <summary>树上请求用例节点对应的列表项，用于复用改名和复制命令的参数契约。</summary>
    public RequestCaseItemViewModel? SourceCaseItem { get; set; }

    public ApiEndpointDto? Endpoint { get; set; }

    protected override void DisposeManaged()
    {
        Children.CollectionChanged -= OnChildrenCollectionChanged;

        foreach (var child in Children.ToList())
        {
            child.Dispose();
        }
    }

    public void SyncFrom(ExplorerItemViewModel source)
    {
        var wasChildrenLoaded = _childrenLoaded;
        var previousChildLoader = _childLoader;
        var previousSourceCase = SourceCase;
        var previousSourceCaseItem = SourceCaseItem;
        var previousEndpoint = Endpoint;
        var previousRenameCommand = RenameCommand;
        var previousDuplicateCommand = DuplicateCommand;

        NodeKey = source.NodeKey;
        Title = source.Title;
        Subtitle = source.Subtitle;
        IsGroup = source.IsGroup;
        NodeType = source.NodeType;
        CanLoad = source.CanLoad;
        CaseCount = source.CaseCount;
        DeleteCommand = source.DeleteCommand;
        RenameCommand = source.RenameCommand;
        DuplicateCommand = source.DuplicateCommand;
        CreateSubfolderCommand = source.CreateSubfolderCommand;
        CreateInterfaceInFolderCommand = source.CreateInterfaceInFolderCommand;
        FolderFullPath = source.FolderFullPath;
        FolderDepth = source.FolderDepth;
        SourceCase = source.SourceCase;
        SourceCaseItem = source.SourceCaseItem;
        Endpoint = source.Endpoint;
        _childLoader = source._childLoader;
        if (_childLoader is null)
        {
            _childrenLoaded = true;
        }
        else if (!wasChildrenLoaded)
        {
            _childrenLoaded = false;
        }

        // 树重建时匹配节点多数数据未变：无实质变化直接返回。
        // 侧栏树行全部实例化后，无条件通知会让每次重建都对所有行重估绑定，是卡顿放大器；
        // Title/NodeType/CanLoad 等可观察属性自身相等时不发通知，这里只兜底普通属性派生的聚合通知。
        var hasMeaningfulChange = !ReferenceEquals(previousChildLoader, _childLoader)
            || wasChildrenLoaded != _childrenLoaded
            || !ReferenceEquals(previousSourceCase, SourceCase)
            || !ReferenceEquals(previousSourceCaseItem, SourceCaseItem)
            || !ReferenceEquals(previousEndpoint, Endpoint)
            || !ReferenceEquals(previousRenameCommand, RenameCommand)
            || !ReferenceEquals(previousDuplicateCommand, DuplicateCommand);
        if (!hasMeaningfulChange)
        {
            return;
        }

        OnPropertyChanged(nameof(HasChildren));
        OnPropertyChanged(nameof(HasDeferredChildren));
        OnPropertyChanged(nameof(AreChildrenLoaded));
        OnPropertyChanged(nameof(IsClickable));
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(RenameCommand));
        OnPropertyChanged(nameof(DuplicateCommand));
        OnPropertyChanged(nameof(MethodBadgeText));
        OnPropertyChanged(nameof(MethodBadgeClass));
        OnPropertyChanged(nameof(IsRequestCaseNode));
        OnPropertyChanged(nameof(HasChildrenCount));
        OnPropertyChanged(nameof(ChildrenCountSuffix));
        OnPropertyChanged(nameof(SidebarDisplayText));
    }

    public void SetDeferredChildren(Func<IReadOnlyList<ExplorerItemViewModel>> childLoader)
    {
        _childLoader = childLoader;
        _childrenLoaded = false;
        OnPropertyChanged(nameof(HasChildren));
        OnPropertyChanged(nameof(HasDeferredChildren));
        OnPropertyChanged(nameof(AreChildrenLoaded));
        OnPropertyChanged(nameof(IsClickable));
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(HasChildrenCount));
        OnPropertyChanged(nameof(ChildrenCountSuffix));
        OnPropertyChanged(nameof(SidebarDisplayText));
    }

    public void EnsureChildrenLoaded()
    {
        if (IsDisposed || _childrenLoaded || _childLoader is null)
        {
            return;
        }

        ReplaceChildren(_childLoader());
        _childrenLoaded = true;
        OnPropertyChanged(nameof(HasChildren));
        OnPropertyChanged(nameof(HasDeferredChildren));
        OnPropertyChanged(nameof(AreChildrenLoaded));
        OnPropertyChanged(nameof(IsClickable));
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(HasChildrenCount));
        OnPropertyChanged(nameof(ChildrenCountSuffix));
        OnPropertyChanged(nameof(SidebarDisplayText));
    }

    private void OnChildrenCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        OnPropertyChanged(nameof(HasChildren));
        OnPropertyChanged(nameof(IsClickable));
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(HasChildrenCount));
        OnPropertyChanged(nameof(ChildrenCountSuffix));
        OnPropertyChanged(nameof(SidebarDisplayText));
    }

    partial void OnCanLoadChanged(bool value)
    {
        OnPropertyChanged(nameof(IsClickable));
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if (value)
        {
            EnsureChildrenLoaded();
        }
    }

    partial void OnTitleChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayTitle));
        OnPropertyChanged(nameof(SidebarDisplayText));
    }

    partial void OnNodeTypeChanged(string value)
    {
        OnPropertyChanged(nameof(ShowMethodBadge));
        OnPropertyChanged(nameof(ShowLeadingGlyph));
        OnPropertyChanged(nameof(IsQuickRequestNode));
        OnPropertyChanged(nameof(IsRequestCaseNode));
        OnPropertyChanged(nameof(IsFolderNode));
        OnPropertyChanged(nameof(CanCreateSubfolder));
        OnPropertyChanged(nameof(NodeGlyph));
        OnPropertyChanged(nameof(DisplayTitle));
        OnPropertyChanged(nameof(SidebarDisplayText));
    }

    private void ReplaceChildren(IReadOnlyList<ExplorerItemViewModel> items)
    {
        foreach (var child in Children.ToList())
        {
            child.Dispose();
        }

        // 大组（59+ 子节点）展开时逐条 Add 会触发 N 次集合事件、每次再派生 7 个属性通知；
        // ReplaceWith 单次 Reset 把通知成本从 8N 压到常数
        Children.ReplaceWith(items);
    }
}
