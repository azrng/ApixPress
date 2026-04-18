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

        var folderNodes = new Dictionary<string, ExplorerItemViewModel>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in httpInterfaces)
        {
            var parentNode = interfaceRoot;
            var folderPath = NormalizeFolderPath(item.SourceCase.FolderPath);
            if (!string.IsNullOrWhiteSpace(folderPath))
            {
                var segments = folderPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var currentPath = string.Empty;
                foreach (var segment in segments)
                {
                    currentPath = string.IsNullOrWhiteSpace(currentPath) ? segment : $"{currentPath}/{segment}";
                    if (!folderNodes.TryGetValue(currentPath, out var folderNode))
                    {
                        folderNode = new ExplorerItemViewModel
                        {
                            NodeKey = $"folder:{currentPath}",
                            Title = BuildFolderTitle(segment, currentPath, folderCounts),
                            Subtitle = string.Empty,
                            IsGroup = true,
                            NodeType = "folder",
                            DeleteCommand = deleteCommand
                        };
                        folderNodes[currentPath] = folderNode;
                        parentNode.Children.Add(folderNode);
                    }

                    parentNode = folderNode;
                }
            }

            var interfaceNode = new ExplorerItemViewModel
            {
                NodeKey = $"http-interface:{item.SourceCase.Id}",
                Title = BuildInterfaceTitle(item.Name, httpCases.TryGetValue(item.SourceCase.Id, out var interfaceCases) ? interfaceCases.Count : 0),
                Subtitle = string.Empty,
                NodeType = ProjectTabRequestEntryTypes.HttpInterface,
                CanLoad = true,
                DeleteCommand = deleteCommand,
                SourceCase = item.SourceCase
            };
            parentNode.Children.Add(interfaceNode);

            if (httpCases.TryGetValue(item.Id, out var cases))
            {
                foreach (var caseItem in cases)
                {
                    interfaceNode.Children.Add(new ExplorerItemViewModel
                    {
                        NodeKey = $"http-case:{caseItem.SourceCase.Id}",
                        Title = caseItem.Name,
                        Subtitle = string.Empty,
                        NodeType = ProjectTabRequestEntryTypes.HttpCase,
                        CanLoad = true,
                        DeleteCommand = deleteCommand,
                        SourceCase = caseItem.SourceCase
                    });
                }
            }
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
}
