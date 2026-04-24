using System.Windows.Input;
using ApixPress.App.Models.DTOs;

namespace ApixPress.App.ViewModels;

public static class ProjectWorkspaceTreeBuilder
{
    public static (ExplorerItemViewModel InterfaceRoot, List<ExplorerItemViewModel> QuickRequests) Build(
        IEnumerable<RequestCaseItemViewModel> savedRequests,
        ICommand deleteCommand)
    {
        return (BuildInterfaceRoot(savedRequests, deleteCommand), BuildQuickRequests(savedRequests, deleteCommand));
    }

    public static ExplorerItemViewModel BuildInterfaceRoot(
        IEnumerable<RequestCaseItemViewModel> savedRequests,
        ICommand deleteCommand)
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
        var folderCounts = BuildFolderDescendantCounts(httpInterfaces.Select(item => item.SourceCase.FolderPath));

        var interfaceRoot = new ExplorerItemViewModel
        {
            NodeKey = "interface-root",
            Title = "接口",
            Subtitle = string.Empty,
            IsGroup = true,
            NodeType = "interface-root",
            DeleteCommand = deleteCommand
        };

        var rootFolderSpecs = new Dictionary<string, FolderNodeSpec>(StringComparer.OrdinalIgnoreCase);
        var rootInterfaces = new List<InterfaceNodeSpec>();

        foreach (var item in httpInterfaces)
        {
            FolderNodeSpec? parentFolder = null;
            var folderPath = NormalizeFolderPath(item.SourceCase.FolderPath);
            if (!string.IsNullOrWhiteSpace(folderPath))
            {
                var segments = folderPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var currentPath = string.Empty;
                foreach (var segment in segments)
                {
                    currentPath = string.IsNullOrWhiteSpace(currentPath) ? segment : $"{currentPath}/{segment}";
                    var collection = parentFolder is null
                        ? rootFolderSpecs
                        : parentFolder.Children;

                    if (!collection.TryGetValue(currentPath, out var folderNode))
                    {
                        folderNode = new FolderNodeSpec(
                            currentPath,
                            BuildFolderTitle(segment, currentPath, folderCounts));
                        collection[currentPath] = folderNode;
                    }

                    parentFolder = folderNode;
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

        foreach (var folderSpec in rootFolderSpecs.Values.OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase))
        {
            interfaceRoot.Children.Add(BuildFolderNode(folderSpec, deleteCommand));
        }

        foreach (var interfaceSpec in rootInterfaces.OrderBy(item => item.Item.Name, StringComparer.OrdinalIgnoreCase))
        {
            interfaceRoot.Children.Add(BuildInterfaceNode(interfaceSpec, deleteCommand));
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
            ProjectTabRequestEntryTypes.HttpCase => 0,
            ProjectTabRequestEntryTypes.QuickRequest => 1,
            ProjectTabRequestEntryTypes.HttpInterface => 2,
            _ => 3
        };
    }

    private static ExplorerItemViewModel BuildFolderNode(FolderNodeSpec spec, ICommand deleteCommand)
    {
        var node = new ExplorerItemViewModel
        {
            NodeKey = $"folder:{spec.Path}",
            Title = spec.Title,
            Subtitle = string.Empty,
            IsGroup = true,
            NodeType = "folder",
            IsExpanded = false,
            DeleteCommand = deleteCommand
        };
        if (spec.HasChildren)
        {
            node.SetDeferredChildren(() => BuildFolderChildren(spec, deleteCommand));
        }

        return node;
    }

    private static IReadOnlyList<ExplorerItemViewModel> BuildFolderChildren(FolderNodeSpec spec, ICommand deleteCommand)
    {
        var children = new List<ExplorerItemViewModel>();
        children.AddRange(spec.Children.Values
            .OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .Select(item => BuildFolderNode(item, deleteCommand)));
        children.AddRange(spec.Interfaces
            .OrderBy(item => item.Item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => BuildInterfaceNode(item, deleteCommand)));
        return children;
    }

    private static ExplorerItemViewModel BuildInterfaceNode(InterfaceNodeSpec spec, ICommand deleteCommand)
    {
        var interfaceNode = new ExplorerItemViewModel
        {
            NodeKey = $"http-interface:{spec.Item.SourceCase.Id}",
            Title = BuildInterfaceTitle(spec.Item.Name, spec.Cases.Count),
            Subtitle = string.Empty,
            NodeType = ProjectTabRequestEntryTypes.HttpInterface,
            CanLoad = true,
            IsExpanded = false,
            DeleteCommand = deleteCommand,
            SourceCase = spec.Item.SourceCase
        };

        if (spec.Cases.Count > 0)
        {
            interfaceNode.SetDeferredChildren(() => spec.Cases.Select(caseItem => new ExplorerItemViewModel
            {
                NodeKey = $"http-case:{caseItem.SourceCase.Id}",
                Title = caseItem.Name,
                Subtitle = string.Empty,
                NodeType = ProjectTabRequestEntryTypes.HttpCase,
                CanLoad = true,
                DeleteCommand = deleteCommand,
                SourceCase = caseItem.SourceCase
            }).ToList());
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

            var segments = folderPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var currentPath = string.Empty;
            foreach (var segment in segments)
            {
                currentPath = string.IsNullOrWhiteSpace(currentPath) ? segment : $"{currentPath}/{segment}";
                counts[currentPath] = counts.TryGetValue(currentPath, out var count) ? count + 1 : 1;
            }
        }

        return counts;
    }

    private static string BuildFolderTitle(string segment, string path, IReadOnlyDictionary<string, int> folderCounts)
    {
        return folderCounts.TryGetValue(path, out var count) && count > 0
            ? $"{segment} ({count})"
            : segment;
    }

    private static string BuildInterfaceTitle(string name, int caseCount)
    {
        return caseCount > 0 ? $"{name} ({caseCount})" : name;
    }

    private sealed class FolderNodeSpec
    {
        public FolderNodeSpec(string path, string title)
        {
            Path = path;
            Title = title;
        }

        public string Path { get; }

        public string Title { get; }

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
