using ApixPress.App.Models.DTOs;
using System.Text;

namespace ApixPress.App.ViewModels;

public partial class ProjectTabViewModel
{
    private async Task SaveQuickRequestAsync(RequestWorkspaceTabViewModel workspaceTab, string? requestNameOverride = null)
    {
        if (!HasAbsoluteHttpUrl(workspaceTab.RequestUrl))
        {
            StatusMessage = "快捷请求仅支持完整地址，请输入 http:// 或 https:// 开头的 URL。";
            NotifyShellState();
            return;
        }

        var requestName = string.IsNullOrWhiteSpace(requestNameOverride)
            ? workspaceTab.ResolveRequestName()
            : requestNameOverride.Trim();
        var snapshot = workspaceTab.BuildSnapshot(requestName);
        var result = await _requestCaseService.SaveAsync(new RequestCaseDto
        {
            Id = workspaceTab.EditingQuickRequestId,
            ProjectId = ProjectId,
            EntryType = RequestEntryTypes.QuickRequest,
            Name = requestName,
            GroupName = "快捷请求",
            Description = workspaceTab.ConfigTab.RequestDescription,
            RequestSnapshot = snapshot,
            UpdatedAt = DateTime.UtcNow
        }, CancellationToken.None);

        if (result.IsSuccess && result.Data is not null)
        {
            workspaceTab.EditingQuickRequestId = result.Data.Id;
            await ReloadSavedRequestsAsync();
            StatusMessage = "快捷请求已保存到左侧目录。";
        }
        else
        {
            StatusMessage = result.Message;
        }

        NotifyShellState();
    }

    private async Task SaveHttpInterfaceAsync(RequestWorkspaceTabViewModel workspaceTab)
    {
        var interfaceId = await EnsureHttpInterfaceSavedAsync(workspaceTab, reloadAfterSave: true);
        if (!string.IsNullOrWhiteSpace(interfaceId))
        {
            StatusMessage = "HTTP 接口已保存到默认模块。";
            NotifyShellState();
        }
    }

    private async Task<string?> EnsureHttpInterfaceSavedAsync(RequestWorkspaceTabViewModel workspaceTab, bool reloadAfterSave)
    {
        var snapshot = workspaceTab.BuildSnapshot();
        var result = await _requestCaseService.SaveAsync(new RequestCaseDto
        {
            Id = workspaceTab.EditingInterfaceId,
            ProjectId = ProjectId,
            EntryType = RequestEntryTypes.HttpInterface,
            Name = workspaceTab.ResolveRequestName(),
            GroupName = "接口",
            FolderPath = NormalizeFolderPath(workspaceTab.InterfaceFolderPath),
            Description = workspaceTab.ConfigTab.RequestDescription,
            RequestSnapshot = snapshot,
            UpdatedAt = DateTime.UtcNow
        }, CancellationToken.None);

        if (!result.IsSuccess || result.Data is null)
        {
            StatusMessage = result.Message;
            NotifyShellState();
            return null;
        }

        workspaceTab.EditingInterfaceId = result.Data.Id;
        workspaceTab.SourceEndpointId = result.Data.RequestSnapshot.EndpointId;
        if (reloadAfterSave)
        {
            await ReloadSavedRequestsAsync();
        }

        return result.Data.Id;
    }

    private async Task ReloadSavedRequestsAsync()
    {
        await UseCasesPanel.LoadCasesAsync();
        RebuildWorkspaceNavigation();
    }

    private void RebuildWorkspaceNavigation()
    {
        InterfaceTreeItems.Clear();
        QuickRequestTreeItems.Clear();

        var httpInterfaces = SavedRequests
            .Where(item => string.Equals(item.SourceCase.EntryType, RequestEntryTypes.HttpInterface, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.SourceCase.FolderPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var httpCases = SavedRequests
            .Where(item => string.Equals(item.SourceCase.EntryType, RequestEntryTypes.HttpCase, StringComparison.OrdinalIgnoreCase))
            .GroupBy(item => item.SourceCase.ParentId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.UpdatedAt).ToList(), StringComparer.OrdinalIgnoreCase);
        var quickRequests = SavedRequests
            .Where(item => string.Equals(item.SourceCase.EntryType, RequestEntryTypes.QuickRequest, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.UpdatedAt)
            .ToList();
        var folderCounts = BuildFolderDescendantCounts(httpInterfaces.Select(item => item.SourceCase.FolderPath));

        var interfaceRoot = new ExplorerItemViewModel
        {
            Title = "接口",
            Subtitle = string.Empty,
            IsGroup = true,
            NodeType = "interface-root",
            DeleteCommand = RequestDeleteWorkspaceTreeItemCommand
        };
        InterfaceTreeItems.Add(interfaceRoot);

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
                            Title = BuildFolderTitle(segment, currentPath, folderCounts),
                            Subtitle = string.Empty,
                            IsGroup = true,
                            NodeType = "folder",
                            DeleteCommand = RequestDeleteWorkspaceTreeItemCommand
                        };
                        folderNodes[currentPath] = folderNode;
                        parentNode.Children.Add(folderNode);
                    }

                    parentNode = folderNode;
                }
            }

            var interfaceNode = new ExplorerItemViewModel
            {
                Title = BuildInterfaceTitle(item.Name, httpCases.TryGetValue(item.Id, out var interfaceCases) ? interfaceCases.Count : 0),
                Subtitle = string.Empty,
                NodeType = RequestEntryTypes.HttpInterface,
                CanLoad = true,
                DeleteCommand = RequestDeleteWorkspaceTreeItemCommand,
                SourceCase = item.SourceCase
            };
            parentNode.Children.Add(interfaceNode);

            if (httpCases.TryGetValue(item.Id, out var cases))
            {
                foreach (var caseItem in cases)
                {
                    interfaceNode.Children.Add(new ExplorerItemViewModel
                    {
                        Title = caseItem.Name,
                        Subtitle = string.Empty,
                        NodeType = RequestEntryTypes.HttpCase,
                        CanLoad = true,
                        DeleteCommand = RequestDeleteWorkspaceTreeItemCommand,
                        SourceCase = caseItem.SourceCase
                    });
                }
            }
        }

        foreach (var item in quickRequests)
        {
            QuickRequestTreeItems.Add(new ExplorerItemViewModel
            {
                Title = item.Name,
                Subtitle = string.Empty,
                NodeType = RequestEntryTypes.QuickRequest,
                CanLoad = true,
                DeleteCommand = RequestDeleteWorkspaceTreeItemCommand,
                SourceCase = item.SourceCase
            });
        }

        OnPropertyChanged(nameof(InterfaceCatalogItems));
    }

    private IEnumerable<RequestCaseDto> CollectDeletableSourceCases(ExplorerItemViewModel item)
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

    private void CloseWorkspaceTabsForDeletedCases(IReadOnlyCollection<RequestCaseDto> deletedCases)
    {
        var deletedIds = deletedCases
            .Select(item => item.Id)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (deletedIds.Count == 0)
        {
            return;
        }

        var tabsToClose = WorkspaceTabs
            .Where(tab => deletedIds.Contains(tab.EditingQuickRequestId)
                || deletedIds.Contains(tab.EditingInterfaceId)
                || deletedIds.Contains(tab.EditingCaseId))
            .ToList();
        foreach (var tab in tabsToClose)
        {
            CloseWorkspaceTab(tab);
        }
    }

    private ProjectEnvironmentDto BuildExecutionEnvironment()
    {
        var environment = EnvironmentPanel.GetSelectedEnvironmentDto();
        if (environment is not null)
        {
            return environment;
        }

        return new ProjectEnvironmentDto
        {
            Id = string.Empty,
            ProjectId = ProjectId,
            Name = "未配置环境",
            BaseUrl = string.Empty,
            IsActive = false,
            SortOrder = 0
        };
    }

    private void SetImportDataStatus(string message, string statusState)
    {
        ImportDataStatusText = message;
        ImportDataStatusState = statusState;
    }

    private void ClearPendingImportConfirmation()
    {
        _pendingImportRequest = null;
        PendingImportPreview = null;
        IsImportOverwriteConfirmDialogOpen = false;
    }

    private static string ResolveImportSourceTypeText(string sourceType)
    {
        return string.Equals(sourceType, "URL", StringComparison.OrdinalIgnoreCase)
            ? "URL 导入"
            : "文件上传";
    }

    private void OpenQuickRequestSaveDialog(RequestWorkspaceTabViewModel workspaceTab)
    {
        var fallbackName = string.IsNullOrWhiteSpace(workspaceTab.ConfigTab.RequestName)
            ? workspaceTab.ResolveRequestName()
            : workspaceTab.ConfigTab.RequestName.Trim();
        QuickRequestSaveName = fallbackName;
        QuickRequestSaveDescription = workspaceTab.ConfigTab.RequestDescription;
        IsQuickRequestSaveDialogOpen = true;
        StatusMessage = "请输入快捷请求名称后再保存。";
        NotifyShellState();
    }

    private RequestWorkspaceTabViewModel ReuseActiveLandingOrCreateWorkspace()
    {
        if (ActiveWorkspaceTab?.IsLandingTab == true)
        {
            return ActiveWorkspaceTab;
        }

        return CreateWorkspaceTab(activate: false);
    }

    private RequestWorkspaceTabViewModel CreateWorkspaceTab(bool activate, bool showInTabStrip = true)
    {
        var tab = new RequestWorkspaceTabViewModel();
        tab.ConfigureAsLanding();
        tab.ShowInTabStrip = showInTabStrip;
        AttachWorkspaceTab(tab);
        WorkspaceTabs.Add(tab);
        if (activate)
        {
            ActivateWorkspaceTabCore(tab);
        }

        return tab;
    }

    private void EnsureLandingWorkspaceTab()
    {
        if (WorkspaceTabs.Count == 0)
        {
            var tab = CreateWorkspaceTab(activate: false, showInTabStrip: false);
            tab.ConfigureAsLanding();
            tab.ShowInTabStrip = false;
            ActivateWorkspaceTabCore(tab);
            return;
        }

        if (ActiveWorkspaceTab is null)
        {
            ActivateWorkspaceTabCore(WorkspaceTabs[0]);
        }
    }

    private RequestWorkspaceTabViewModel? FindLandingWorkspaceTab()
    {
        return WorkspaceTabs
            .Where(item => item.IsLandingTab)
            .OrderByDescending(item => item.ShowInTabStrip)
            .FirstOrDefault();
    }

    private RequestWorkspaceTabViewModel? FindFirstQuickRequestTab()
    {
        return WorkspaceTabs.FirstOrDefault(item => item.IsQuickRequestTab);
    }

    private RequestWorkspaceTabViewModel? FindWorkspaceTabForSource(RequestCaseDto source)
    {
        return WorkspaceTabs.FirstOrDefault(item =>
            string.Equals(source.EntryType, RequestEntryTypes.QuickRequest, StringComparison.OrdinalIgnoreCase)
                ? string.Equals(item.EditingQuickRequestId, source.Id, StringComparison.OrdinalIgnoreCase)
                : string.Equals(source.EntryType, RequestEntryTypes.HttpInterface, StringComparison.OrdinalIgnoreCase)
                    ? string.Equals(item.EditingInterfaceId, source.Id, StringComparison.OrdinalIgnoreCase)
                    : string.Equals(item.EditingCaseId, source.Id, StringComparison.OrdinalIgnoreCase));
    }

    private void AttachWorkspaceTab(RequestWorkspaceTabViewModel tab)
    {
        tab.PropertyChanged += OnWorkspaceTabPropertyChanged;
        tab.ConfigTab.PropertyChanged += OnWorkspaceConfigPropertyChanged;
        tab.ConfigTab.QueryParameters.CollectionChanged += OnWorkspaceConfigCollectionChanged;
        tab.ConfigTab.Headers.CollectionChanged += OnWorkspaceConfigCollectionChanged;
        tab.ConfigTab.FormFields.CollectionChanged += OnWorkspaceConfigCollectionChanged;
    }

    private void DetachWorkspaceTab(RequestWorkspaceTabViewModel tab)
    {
        tab.PropertyChanged -= OnWorkspaceTabPropertyChanged;
        tab.ConfigTab.PropertyChanged -= OnWorkspaceConfigPropertyChanged;
        tab.ConfigTab.QueryParameters.CollectionChanged -= OnWorkspaceConfigCollectionChanged;
        tab.ConfigTab.Headers.CollectionChanged -= OnWorkspaceConfigCollectionChanged;
        tab.ConfigTab.FormFields.CollectionChanged -= OnWorkspaceConfigCollectionChanged;
    }

    private void ActivateWorkspaceTabCore(RequestWorkspaceTabViewModel tab)
    {
        ActiveWorkspaceTab = tab;
    }

    private RequestCaseDto? FindRequestById(string id)
    {
        return SavedRequests
            .Select(item => item.SourceCase)
            .FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    private string ResolveLatestCaseName(string interfaceId)
    {
        return SavedRequests
            .Where(item => string.Equals(item.SourceCase.EntryType, RequestEntryTypes.HttpCase, StringComparison.OrdinalIgnoreCase))
            .Where(item => string.Equals(item.SourceCase.ParentId, interfaceId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.UpdatedAt)
            .Select(item => item.Name)
            .FirstOrDefault()
            ?? "成功";
    }

    private static string BuildHttpCaseName(RequestWorkspaceTabViewModel workspaceTab)
    {
        return string.IsNullOrWhiteSpace(workspaceTab.HttpCaseName)
            ? "成功"
            : workspaceTab.HttpCaseName.Trim();
    }

    private static bool HasAbsoluteHttpUrl(string? value)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private string BuildHttpDocumentUrl()
    {
        var path = RequestUrl.Trim();
        string resolvedUrl;
        if (string.IsNullOrWhiteSpace(path))
        {
            resolvedUrl = string.IsNullOrWhiteSpace(CurrentHttpInterfaceBaseUrl)
                ? "未配置 BaseUrl / 未填写路径"
                : $"{CurrentHttpInterfaceBaseUrl.TrimEnd('/')}/";
        }
        else if (Uri.TryCreate(path, UriKind.Absolute, out _))
        {
            resolvedUrl = path;
        }
        else if (string.IsNullOrWhiteSpace(CurrentHttpInterfaceBaseUrl))
        {
            resolvedUrl = $"未配置 BaseUrl {path}";
        }
        else
        {
            resolvedUrl = $"{CurrentHttpInterfaceBaseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
        }

        var queryString = string.Join("&", ConfigTab.QueryParameters
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .Select(item =>
                $"{Uri.EscapeDataString(item.Name.Trim())}={Uri.EscapeDataString((item.Value ?? string.Empty).Trim())}"));

        if (string.IsNullOrWhiteSpace(queryString))
        {
            return resolvedUrl;
        }

        return resolvedUrl.Contains('?', StringComparison.Ordinal)
            ? $"{resolvedUrl}&{queryString}"
            : $"{resolvedUrl}?{queryString}";
    }

    private string BuildHttpDocumentCurlSnippet()
    {
        var url = BuildHttpDocumentUrl();
        var resolvedUrl = url.StartsWith("未配置", StringComparison.OrdinalIgnoreCase)
            ? RequestUrl.Trim()
            : url;

        var builder = new StringBuilder();
        builder.Append("curl --request ")
            .Append(SelectedMethod)
            .Append(" \"")
            .Append(EscapeCurlValue(resolvedUrl))
            .Append('"');

        foreach (var header in ConfigTab.Headers.Where(item => !string.IsNullOrWhiteSpace(item.Name)))
        {
            builder.Append(" \\\n  --header \"")
                .Append(EscapeCurlValue(header.Name.Trim()))
                .Append(": ")
                .Append(EscapeCurlValue((header.Value ?? string.Empty).Trim()))
                .Append('"');
        }

        var bodyContent = ResolveHttpDocumentBodyContent();
        if (!string.IsNullOrWhiteSpace(bodyContent))
        {
            builder.Append(" \\\n  --data-raw \"")
                .Append(EscapeCurlValue(bodyContent))
                .Append('"');
        }

        return builder.ToString();
    }

    private string ResolveHttpDocumentBodyContent()
    {
        if (ConfigTab.SelectedBodyMode is BodyModes.FormData or BodyModes.FormUrlEncoded)
        {
            return string.Join("&", ConfigTab.FormFields
                .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                .Select(item =>
                    $"{Uri.EscapeDataString(item.Name.Trim())}={Uri.EscapeDataString((item.Value ?? string.Empty).Trim())}"));
        }

        return ConfigTab.HasBodyContent ? ConfigTab.RequestBody.Trim() : string.Empty;
    }

    private static string EscapeCurlValue(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string NormalizeFolderPath(string folderPath)
    {
        var normalized = folderPath.Replace('\\', '/').Trim('/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        return string.Join('/',
            normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
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

    private static bool IsImportedInterface(RequestCaseDto requestCase)
    {
        return requestCase.RequestSnapshot.EndpointId.StartsWith(ImportedEndpointKeyPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static int ResolveDeletePriority(string entryType)
    {
        return entryType switch
        {
            RequestEntryTypes.HttpCase => 0,
            RequestEntryTypes.QuickRequest => 1,
            RequestEntryTypes.HttpInterface => 2,
            _ => 3
        };
    }
}
