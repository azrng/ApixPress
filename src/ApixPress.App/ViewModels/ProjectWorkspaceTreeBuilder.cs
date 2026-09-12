using System.Windows.Input;
using ApixPress.App.Models.DTOs;

namespace ApixPress.App.ViewModels;

/// <summary>接口树节点可触发的命令集合，由目录节点与接口节点按需绑定。</summary>
public sealed record WorkspaceTreeItemCommands(
    ICommand Delete,
    ICommand CreateSubfolder,
    ICommand CreateInterfaceInFolder);

public static class ProjectWorkspaceTreeBuilder
{
    /// <summary>接口树最多展示两级目录，更深的导入路径会合并到第二级目录下。</summary>
    public const int MaxFolderDepth = 2;

    public static (ExplorerItemViewModel InterfaceRoot, List<ExplorerItemViewModel> QuickRequests) Build(
        IEnumerable<RequestCaseItemViewModel> savedRequests,
        WorkspaceTreeItemCommands commands)
    {
        return (BuildInterfaceRoot(savedRequests, commands), BuildQuickRequests(savedRequests, commands.Delete));
    }

    public static ExplorerItemViewModel BuildInterfaceRoot(
        IEnumerable<RequestCaseItemViewModel> savedRequests,
        WorkspaceTreeItemCommands commands,
        bool expandAll = false)
    {
        var requestItems = savedRequests.ToList();
        var httpInterfaces = requestItems
            .Where(item => string.Equals(item.SourceCase.EntryType, ProjectTabRequestEntryTypes.HttpInterface, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.SourceCase.FolderPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var httpCases = requestItems
            .Where(item => string.Equals(item.SourceCase.EntryType, ProjectTabRequestEntryTypes.HttpCase, StringComparison.OrdinalIgnoreCase))
            .GroupBy(item => item.SourceCase.ParentId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.UpdatedAt).ToList(), StringComparer.OrdinalIgnoreCase);
        var folderRows = requestItems
            .Where(item => string.Equals(item.SourceCase.EntryType, ProjectTabRequestEntryTypes.Folder, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var folderCounts = BuildFolderDescendantCounts(httpInterfaces.Select(item => item.SourceCase.FolderPath));

        var interfaceRoot = new ExplorerItemViewModel
        {
            NodeKey = "interface-root",
            Title = "接口",
            Subtitle = string.Empty,
            IsGroup = true,
            NodeType = "interface-root",
            DeleteCommand = commands.Delete
        };

        // 目录节点先落位，空的用户目录也能展示；接口路径再合并进同一批目录规格
        var rootFolderSpecs = new Dictionary<string, FolderNodeSpec>(StringComparer.OrdinalIgnoreCase);
        foreach (var folderRow in folderRows)
        {
            var parentPath = NormalizeFolderPath(folderRow.SourceCase.FolderPath);
            var folderDepth = CountPathDepth(parentPath) + 1;
            var fullPath = string.IsNullOrWhiteSpace(parentPath) ? folderRow.SourceCase.Name : $"{parentPath}/{folderRow.SourceCase.Name}";
            var spec = EnsureFolderSpec(rootFolderSpecs, fullPath, folderDepth);
            spec.SourceCase = folderRow.SourceCase;
        }

        var rootInterfaces = new List<InterfaceNodeSpec>();
        foreach (var item in httpInterfaces)
        {
            FolderNodeSpec? parentFolder = null;
            var folderPath = NormalizeFolderPath(item.SourceCase.FolderPath);
            if (!string.IsNullOrWhiteSpace(folderPath))
            {
                var segments = folderPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                // 超过两级的导入路径合并到第二级目录，避免树无限加深
                foreach (var segment in segments.Take(MaxFolderDepth))
                {
                    var currentPath = string.IsNullOrWhiteSpace(parentFolder?.Path)
                        ? segment
                        : $"{parentFolder.Path}/{segment}";
                    var collection = parentFolder is null
                        ? rootFolderSpecs
                        : parentFolder.Children;

                    parentFolder = EnsureFolderSpec(collection, currentPath, 0);
                    if (parentFolder.Depth == 0)
                    {
                        parentFolder.Depth = CountPathDepth(currentPath);
                    }
                }
            }

            var interfaceSpec = new InterfaceNodeSpec(
                item,
                httpCases.TryGetValue(item.SourceCase.Id, out var interfaceCases) ? interfaceCases : []);
            if (parentFolder is null)
            {
                rootInterfaces.Add(interfaceSpec);
            }
            else
            {
                parentFolder.Interfaces.Add(interfaceSpec);
            }
        }

        // 标题计数需在目录节点构建前就位，节点创建时会读取 spec.Title
        ApplyFolderTitleCounts(rootFolderSpecs.Values, folderCounts);

        foreach (var folderSpec in rootFolderSpecs.Values.OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase))
        {
            interfaceRoot.Children.Add(BuildFolderNode(folderSpec, commands, expandAll));
        }

        foreach (var interfaceSpec in rootInterfaces.OrderBy(item => item.Item.Name, StringComparer.OrdinalIgnoreCase))
        {
            interfaceRoot.Children.Add(BuildInterfaceNode(interfaceSpec, commands.Delete, expandAll));
        }

        return interfaceRoot;
    }

    public static List<ExplorerItemViewModel> BuildQuickRequests(
        IEnumerable<RequestCaseItemViewModel> savedRequests,
        ICommand deleteCommand)
    {
        return savedRequests
            .Where(item => string.Equals(item.SourceCase.EntryType, ProjectTabRequestEntryTypes.QuickRequest, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.UpdatedAt)
            .Select(item => BuildQuickRequestNode(item, deleteCommand))
            .ToList();
    }

    public static IEnumerable<RequestCaseDto> CollectDeletableSourceCases(ExplorerItemViewModel item)
    {
        item.EnsureChildrenLoaded();

        if (item.SourceCase is not null)
        {
            yield return item.SourceCase;
        }

        foreach (var child in item.Children)
        {
            foreach (var descendant in CollectDeletableSourceCases(child))
            {
                yield return descendant;
            }
        }
    }

    public static string NormalizeFolderPath(string folderPath)
    {
        var normalized = folderPath.Replace('\\', '/').Trim('/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        return string.Join('/',
            normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    public static int ResolveDeletePriority(string entryType)
    {
        return entryType switch
        {
            ProjectTabRequestEntryTypes.Folder => 0,
            ProjectTabRequestEntryTypes.HttpCase => 1,
            ProjectTabRequestEntryTypes.QuickRequest => 2,
            ProjectTabRequestEntryTypes.HttpInterface => 3,
            _ => 4
        };
    }

    /// <summary>按二级截断后的路径，把目录内接口数量追加到目录标题（如 "WeatherForecast (6)"）。</summary>
    private static void ApplyFolderTitleCounts(IEnumerable<FolderNodeSpec> specs, IReadOnlyDictionary<string, int> folderCounts)
    {
        foreach (var spec in specs)
        {
            spec.Title = folderCounts.TryGetValue(spec.Path, out var count) && count > 0
                ? $"{ResolveFolderTitle(spec.Path)} ({count})"
                : ResolveFolderTitle(spec.Path);
            ApplyFolderTitleCounts(spec.Children.Values, folderCounts);
        }
    }

    private static FolderNodeSpec EnsureFolderSpec(
        Dictionary<string, FolderNodeSpec> collection,
        string path,
        int depth)
    {
        if (collection.TryGetValue(path, out var existing))
        {
            return existing;
        }

        var spec = new FolderNodeSpec(path, ResolveFolderTitle(path), depth);
        collection[path] = spec;
        return spec;
    }

    private static string ResolveFolderTitle(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 0 ? segments[^1] : path;
    }

    private static int CountPathDepth(string path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? 0
            : path.Split('/', StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static ExplorerItemViewModel BuildFolderNode(FolderNodeSpec spec, WorkspaceTreeItemCommands commands, bool expandAll)
    {
        var node = new ExplorerItemViewModel
        {
            NodeKey = $"folder:{spec.Path}",
            Title = spec.Title,
            Subtitle = string.Empty,
            IsGroup = true,
            NodeType = "folder",
            IsExpanded = expandAll,
            DeleteCommand = commands.Delete,
            CreateSubfolderCommand = commands.CreateSubfolder,
            CreateInterfaceInFolderCommand = commands.CreateInterfaceInFolder,
            FolderFullPath = spec.Path,
            FolderDepth = spec.Depth,
            SourceCase = spec.SourceCase
        };
        if (spec.HasChildren)
        {
            if (expandAll)
            {
                foreach (var child in BuildFolderChildren(spec, commands, expandAll))
                {
                    node.Children.Add(child);
                }
            }
            else
            {
                node.SetDeferredChildren(() => BuildFolderChildren(spec, commands, expandAll));
            }
        }

        return node;
    }

    private static IReadOnlyList<ExplorerItemViewModel> BuildFolderChildren(FolderNodeSpec spec, WorkspaceTreeItemCommands commands, bool expandAll)
    {
        var children = new List<ExplorerItemViewModel>();
        children.AddRange(spec.Children.Values
            .OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .Select(item => BuildFolderNode(item, commands, expandAll)));
        children.AddRange(spec.Interfaces
            .OrderBy(item => item.Item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => BuildInterfaceNode(item, commands.Delete, expandAll)));
        return children;
    }

    private static ExplorerItemViewModel BuildInterfaceNode(InterfaceNodeSpec spec, ICommand deleteCommand, bool expandAll)
    {
        var interfaceNode = new ExplorerItemViewModel
        {
            NodeKey = $"http-interface:{spec.Item.SourceCase.Id}",
            Title = BuildInterfaceTitle(spec.Item.Name, spec.Cases.Count),
            Subtitle = string.Empty,
            NodeType = ProjectTabRequestEntryTypes.HttpInterface,
            CanLoad = true,
            IsExpanded = expandAll,
            DeleteCommand = deleteCommand,
            SourceCase = spec.Item.SourceCase
        };

        if (spec.Cases.Count > 0)
        {
            var caseChildren = spec.Cases.Select(caseItem => new ExplorerItemViewModel
            {
                NodeKey = $"http-case:{caseItem.SourceCase.Id}",
                Title = caseItem.Name,
                Subtitle = string.Empty,
                NodeType = ProjectTabRequestEntryTypes.HttpCase,
                CanLoad = true,
                DeleteCommand = deleteCommand,
                SourceCase = caseItem.SourceCase
            }).ToList();
            if (expandAll)
            {
                foreach (var child in caseChildren)
                {
                    interfaceNode.Children.Add(child);
                }
            }
            else
            {
                interfaceNode.SetDeferredChildren(() => caseChildren);
            }
        }

        return interfaceNode;
    }

    private static ExplorerItemViewModel BuildQuickRequestNode(RequestCaseItemViewModel item, ICommand deleteCommand)
    {
        return new ExplorerItemViewModel
        {
            NodeKey = $"quick-request:{item.SourceCase.Id}",
            Title = item.Name,
            Subtitle = string.Empty,
            NodeType = ProjectTabRequestEntryTypes.QuickRequest,
            CanLoad = true,
            DeleteCommand = deleteCommand,
            SourceCase = item.SourceCase
        };
    }

    private static Dictionary<string, int> BuildFolderDescendantCounts(IEnumerable<string> folderPaths)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var folderPathValue in folderPaths)
        {
            var folderPath = NormalizeFolderPath(folderPathValue);
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                continue;
            }

            // 与展示一致的二级截断：深层路径的计数归入第二级目录
            var segments = folderPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var currentPath = string.Empty;
            foreach (var segment in segments.Take(MaxFolderDepth))
            {
                currentPath = string.IsNullOrWhiteSpace(currentPath) ? segment : $"{currentPath}/{segment}";
                counts[currentPath] = counts.TryGetValue(currentPath, out var count) ? count + 1 : 1;
            }
        }

        return counts;
    }

    private static string BuildInterfaceTitle(string name, int caseCount)
    {
        return caseCount > 0 ? $"{name} ({caseCount})" : name;
    }

    private sealed class FolderNodeSpec
    {
        public FolderNodeSpec(string path, string title, int depth)
        {
            Path = path;
            Title = title;
            Depth = depth;
        }

        public string Path { get; }

        public string Title { get; set; }

        /// <summary>目录层级：1 为一级目录，2 为二级目录；0 表示尚未定级，由接口路径合并时补齐。</summary>
        public int Depth { get; set; }

        /// <summary>目录行对应的实体；虚拟目录（仅由接口路径推导）为 null。</summary>
        public RequestCaseDto? SourceCase { get; set; }

        public Dictionary<string, FolderNodeSpec> Children { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<InterfaceNodeSpec> Interfaces { get; } = [];

        public bool HasChildren => Children.Count > 0 || Interfaces.Count > 0;
    }

    private sealed class InterfaceNodeSpec
    {
        public InterfaceNodeSpec(RequestCaseItemViewModel item, IReadOnlyList<RequestCaseItemViewModel> cases)
        {
            Item = item;
            Cases = cases;
        }

        public RequestCaseItemViewModel Item { get; }

        public IReadOnlyList<RequestCaseItemViewModel> Cases { get; }
    }
}
