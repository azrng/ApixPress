using System.Collections.Generic;
using ApixPress.App.Models.DTOs;
using ApixPress.App.ViewModels;
using ApixPress.App.Services.Interfaces;
using Azrng.Core.Results;

namespace ApixPress.App.Tests.ViewModels;

public sealed class ProjectTabViewModelTests
{
    [Fact]
    public async Task InitializeAsync_ShouldLoadImportedSwaggerDocuments()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService();
        apiWorkspaceService.SeedDocument("project-1", "支付服务", "FILE", @"C:\temp\pay-swagger.json", "https://pay.demo.local", 3);

        var viewModel = CreateViewModel(apiWorkspaceService);

        await viewModel.InitializeAsync();

        var imported = Assert.Single(viewModel.ImportedApiDocuments);
        Assert.Equal("支付服务", imported.Name);
        Assert.Equal("3", imported.EndpointCountText);
        Assert.True(viewModel.HasImportedApiDocuments);
        Assert.Equal("1", viewModel.ImportedApiDocumentCountText);
    }

    [Fact]
    public async Task InitializeAsync_ShouldShowImportedEndpointsInInterfaceCatalog()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService();
        var requestCaseService = new FakeRequestCaseService();
        apiWorkspaceService.SeedDocument("project-1", "支付服务", "FILE", @"C:\temp\pay-swagger.json", "https://pay.demo.local", 2);

        var viewModel = CreateViewModel(apiWorkspaceService, requestCaseService);

        await viewModel.InitializeAsync();

        var titles = FlattenExplorerTitles(viewModel.InterfaceCatalogItems).ToList();

        Assert.Contains("默认分组 (2)", titles);
        Assert.Contains("接口 1", titles);
        Assert.Contains("接口 2", titles);
        Assert.Equal(2, requestCaseService.Cases.Count(item => item.EntryType == "http-interface"));
    }

    [Fact]
    public async Task ImportSwaggerUrlCommand_ShouldRefreshImportedDocumentList()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService();
        var viewModel = CreateViewModel(apiWorkspaceService);
        await viewModel.InitializeAsync();

        viewModel.ShowProjectImportDataSettingsCommand.Execute(null);
        viewModel.ImportUrl = "https://demo.local/swagger.json";

        await viewModel.ImportSwaggerUrlCommand.ExecuteAsync(null);

        var imported = Assert.Single(viewModel.ImportedApiDocuments);
        Assert.Equal("Swagger URL 导入成功：远程订单服务", viewModel.StatusMessage);
        Assert.Equal("远程订单服务", imported.Name);
        Assert.Equal("URL 导入", imported.SourceTypeText);
        Assert.True(viewModel.ShowImportStatusSuccess);
        Assert.Equal("https://demo.local/swagger.json", apiWorkspaceService.LastImportedUrl);
    }

    [Fact]
    public async Task ImportSwaggerUrlCommand_ShouldAppendImportedDocumentWhenNoPathConflict()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService();
        var requestCaseService = new FakeRequestCaseService();
        apiWorkspaceService.SeedDocument("project-1", "旧文档", "FILE", @"C:\temp\legacy-swagger.json", "https://legacy.demo.local", 1);
        var viewModel = CreateViewModel(apiWorkspaceService, requestCaseService);
        await viewModel.InitializeAsync();

        viewModel.ShowProjectImportDataSettingsCommand.Execute(null);
        viewModel.ImportUrl = "https://demo.local/swagger.json";

        await viewModel.ImportSwaggerUrlCommand.ExecuteAsync(null);

        Assert.Equal("2", viewModel.ImportedApiDocumentCountText);
        Assert.Equal(3, requestCaseService.Cases.Count(item => item.EntryType == "http-interface"));
        Assert.Contains(requestCaseService.Cases, item => item.Name == "接口 1" && item.RequestSnapshot.Url == "/endpoint-1");
        Assert.Contains(requestCaseService.Cases, item => item.Name == "查询订单列表" && item.RequestSnapshot.Url == "/orders");
    }

    [Fact]
    public async Task ImportSwaggerUrlCommand_ShouldRequireConfirmationBeforeOverwritingConflictingEndpoints()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService();
        var requestCaseService = new FakeRequestCaseService();
        apiWorkspaceService.SeedDocument("project-1", "旧文档", "FILE", @"C:\temp\legacy-swagger.json", "https://legacy.demo.local",
        [
            ("订单", "旧的查询订单列表", "GET", "/orders")
        ]);
        var viewModel = CreateViewModel(apiWorkspaceService, requestCaseService);
        await viewModel.InitializeAsync();

        viewModel.ShowProjectImportDataSettingsCommand.Execute(null);
        viewModel.ImportUrl = "https://demo.local/swagger.json";

        await viewModel.ImportSwaggerUrlCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsImportOverwriteConfirmDialogOpen);
        Assert.Contains("覆盖", viewModel.PendingImportOverwriteSummary);
        Assert.Contains("GET /orders", viewModel.PendingImportOverwriteDetailText);

        await viewModel.ConfirmImportOverwriteCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsImportOverwriteConfirmDialogOpen);
        Assert.Equal("1", viewModel.ImportedApiDocumentCountText);
        Assert.Contains(requestCaseService.Cases, item => item.Name == "查询订单列表" && item.RequestSnapshot.Url == "/orders");
        Assert.DoesNotContain(requestCaseService.Cases, item => item.Name == "旧的查询订单列表");
    }

    [Fact]
    public async Task SaveCurrentEditorAsync_ShouldKeepImportedEndpointIdentity()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService();
        var requestCaseService = new FakeRequestCaseService();
        apiWorkspaceService.SeedDocument("project-1", "支付服务", "FILE", @"C:\temp\pay-swagger.json", "https://pay.demo.local", 1);
        var viewModel = CreateViewModel(apiWorkspaceService, requestCaseService);

        await viewModel.InitializeAsync();

        var interfaceItem = FindExplorerItemByTitle(viewModel.InterfaceCatalogItems, "接口 1");
        Assert.NotNull(interfaceItem);

        viewModel.LoadWorkspaceItem(interfaceItem);
        viewModel.CurrentHttpInterfaceName = "接口 1 已编辑";

        await viewModel.SaveCurrentEditorAsync();

        var savedInterface = Assert.Single(requestCaseService.Cases, item => item.EntryType == "http-interface");
        Assert.Equal("接口 1 已编辑", savedInterface.Name);
        Assert.Equal("swagger-import:GET /endpoint-1", savedInterface.RequestSnapshot.EndpointId);

        await requestCaseService.SyncImportedHttpInterfacesAsync("project-1",
        [
            new ApiEndpointDto
            {
                GroupName = "默认分组",
                Name = "接口 1",
                Method = "GET",
                Path = "/endpoint-1"
            }
        ], CancellationToken.None);

        Assert.Equal(1, requestCaseService.Cases.Count(item => item.EntryType == "http-interface"));
    }

    [Fact]
    public void OpenHttpInterfaceWorkspace_ShouldUseDefaultModuleFolder()
    {
        var viewModel = CreateViewModel(new FakeApiWorkspaceService());

        viewModel.OpenHttpInterfaceWorkspaceCommand.Execute(null);

        Assert.Equal("默认模块", viewModel.CurrentInterfaceFolderPath);
    }

    [Fact]
    public async Task DeleteWorkspaceItemAsync_ShouldDeleteFolderAndImportedEndpoints()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService();
        var requestCaseService = new FakeRequestCaseService();
        apiWorkspaceService.SeedDocument("project-1", "支付服务", "FILE", @"C:\temp\pay-swagger.json", "https://pay.demo.local", 2);
        var viewModel = CreateViewModel(apiWorkspaceService, requestCaseService);

        await viewModel.InitializeAsync();

        var folderItem = FindExplorerItemByTitle(viewModel.InterfaceCatalogItems, "默认分组 (2)");
        Assert.NotNull(folderItem);

        await viewModel.DeleteWorkspaceItemAsync(folderItem);

        Assert.Empty(viewModel.InterfaceCatalogItems);
        Assert.Empty(requestCaseService.Cases);
        var remainingDocument = Assert.Single(viewModel.ImportedApiDocuments);
        Assert.Equal("0", remainingDocument.EndpointCountText);
    }

    [Fact]
    public async Task RequestDeleteWorkspaceTreeItemCommand_ShouldOpenDeleteConfirmDialog()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService();
        var requestCaseService = new FakeRequestCaseService();
        apiWorkspaceService.SeedDocument("project-1", "支付服务", "FILE", @"C:\temp\pay-swagger.json", "https://pay.demo.local", 1);
        var viewModel = CreateViewModel(apiWorkspaceService, requestCaseService);

        await viewModel.InitializeAsync();

        var interfaceItem = FindExplorerItemByTitle(viewModel.InterfaceCatalogItems, "接口 1");
        Assert.NotNull(interfaceItem);

        viewModel.RequestDeleteWorkspaceTreeItemCommand.Execute(interfaceItem);

        Assert.True(viewModel.IsWorkspaceDeleteConfirmDialogOpen);
        Assert.Equal("接口 1", viewModel.PendingWorkspaceDeleteTitle);
        Assert.Contains("删除后无法恢复", viewModel.PendingWorkspaceDeleteDescription);
    }

    [Fact]
    public async Task ConfirmWorkspaceItemDeleteCommand_ShouldDeleteAfterConfirmation()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService();
        var requestCaseService = new FakeRequestCaseService();
        apiWorkspaceService.SeedDocument("project-1", "支付服务", "FILE", @"C:\temp\pay-swagger.json", "https://pay.demo.local", 1);
        var viewModel = CreateViewModel(apiWorkspaceService, requestCaseService);

        await viewModel.InitializeAsync();

        var interfaceItem = FindExplorerItemByTitle(viewModel.InterfaceCatalogItems, "接口 1");
        Assert.NotNull(interfaceItem);

        viewModel.RequestDeleteWorkspaceTreeItemCommand.Execute(interfaceItem);
        await viewModel.ConfirmWorkspaceItemDeleteCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsWorkspaceDeleteConfirmDialogOpen);
        Assert.Null(viewModel.PendingDeleteWorkspaceItem);
        Assert.Empty(viewModel.InterfaceCatalogItems);
        Assert.Empty(requestCaseService.Cases);
    }

    [Fact]
    public void ProjectSettingsCommands_ShouldSwitchBetweenOverviewAndImportDataSections()
    {
        var viewModel = CreateViewModel(new FakeApiWorkspaceService());

        viewModel.ShowProjectSettingsCommand.Execute(null);
        viewModel.ShowProjectOverviewSettingsCommand.Execute(null);

        Assert.True(viewModel.IsProjectSettingsSection);
        Assert.True(viewModel.IsProjectSettingsOverviewSelected);
        Assert.False(viewModel.IsProjectSettingsImportDataSelected);
        Assert.Equal("基本设置", viewModel.CurrentProjectSettingsTitle);

        viewModel.ShowProjectImportDataSettingsCommand.Execute(null);

        Assert.True(viewModel.IsProjectSettingsSection);
        Assert.False(viewModel.IsProjectSettingsOverviewSelected);
        Assert.True(viewModel.IsProjectSettingsImportDataSelected);
        Assert.Equal("导入数据", viewModel.CurrentProjectSettingsTitle);

        viewModel.ShowProjectSettingsCommand.Execute(null);

        Assert.True(viewModel.IsProjectSettingsSection);
        Assert.True(viewModel.IsProjectSettingsOverviewSelected);
        Assert.False(viewModel.IsProjectSettingsImportDataSelected);
        Assert.Equal("基本设置", viewModel.CurrentProjectSettingsTitle);
    }

    [Fact]
    public void SelectingWorkspaceNavigationItem_ShouldSwitchWorkspaceSection()
    {
        var viewModel = CreateViewModel(new FakeApiWorkspaceService());
        var projectSettingsItem = Assert.Single(viewModel.WorkspaceNavigationItems, item => item.SectionKey == "project-settings");

        viewModel.SelectedWorkspaceNavigationItem = projectSettingsItem;

        Assert.True(viewModel.IsProjectSettingsSection);
        Assert.Equal(projectSettingsItem, viewModel.SelectedWorkspaceNavigationItem);
    }

    [Fact]
    public void ExecutingProjectSettingsNavCommand_ShouldSwitchWorkspaceSection()
    {
        var viewModel = CreateViewModel(new FakeApiWorkspaceService());
        var projectSettingsItem = Assert.Single(viewModel.WorkspaceNavigationItems, item => item.SectionKey == "project-settings");

        projectSettingsItem.Command.Execute(null);

        Assert.True(viewModel.IsProjectSettingsSection);
        Assert.Equal(projectSettingsItem, viewModel.SelectedWorkspaceNavigationItem);
        Assert.True(projectSettingsItem.IsSelected);
    }

    private static ProjectTabViewModel CreateViewModel(FakeApiWorkspaceService apiWorkspaceService, FakeRequestCaseService? requestCaseService = null)
    {
        return new ProjectTabViewModel(
            new ProjectWorkspaceItemViewModel
            {
                Id = "project-1",
                Name = "测试项目",
                Description = "用于验证项目设置数据管理"
            },
            new FakeRequestExecutionService(),
            requestCaseService ?? new FakeRequestCaseService(),
            new FakeRequestHistoryService(),
            new FakeEnvironmentVariableService(),
            apiWorkspaceService,
            new FakeFilePickerService());
    }

    private static IEnumerable<string> FlattenExplorerTitles(IEnumerable<ExplorerItemViewModel> items)
    {
        foreach (var item in items)
        {
            yield return item.Title;
            foreach (var child in FlattenExplorerTitles(item.Children))
            {
                yield return child;
            }
        }
    }

    private static ExplorerItemViewModel? FindExplorerItemByTitle(IEnumerable<ExplorerItemViewModel> items, string title)
    {
        foreach (var item in items)
        {
            if (string.Equals(item.Title, title, StringComparison.Ordinal))
            {
                return item;
            }

            var child = FindExplorerItemByTitle(item.Children, title);
            if (child is not null)
            {
                return child;
            }
        }

        return null;
    }

    private sealed class FakeApiWorkspaceService : IApiWorkspaceService
    {
        private readonly Dictionary<string, List<ApiDocumentDto>> _documentsByProject = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<ApiEndpointDto>> _endpointsByDocument = new(StringComparer.OrdinalIgnoreCase);

        public string LastImportedUrl { get; private set; } = string.Empty;

        public void SeedDocument(string projectId, string name, string sourceType, string sourceValue, string baseUrl, int endpointCount)
        {
            SeedDocument(projectId, name, sourceType, sourceValue, baseUrl,
                Enumerable.Range(1, endpointCount).Select(index => ("默认分组", $"接口 {index}", "GET", $"/endpoint-{index}")).ToArray());
        }

        public void SeedDocument(
            string projectId,
            string name,
            string sourceType,
            string sourceValue,
            string baseUrl,
            IReadOnlyList<(string GroupName, string Name, string Method, string Path)> endpoints)
        {
            var document = new ApiDocumentDto
            {
                Id = Guid.NewGuid().ToString("N"),
                ProjectId = projectId,
                Name = name,
                SourceType = sourceType,
                SourceValue = sourceValue,
                BaseUrl = baseUrl,
                ImportedAt = DateTime.UtcNow
            };

            if (!_documentsByProject.TryGetValue(projectId, out var documents))
            {
                documents = [];
                _documentsByProject[projectId] = documents;
            }

            documents.Add(document);
            _endpointsByDocument[document.Id] = endpoints
                .Select((endpoint, index) => new ApiEndpointDto
                {
                    Id = $"{document.Id}-{index}",
                    DocumentId = document.Id,
                    GroupName = endpoint.GroupName,
                    Name = endpoint.Name,
                    Method = endpoint.Method,
                    Path = endpoint.Path
                })
                .ToList();
        }

        public Task<IReadOnlyList<ApiDocumentDto>> GetDocumentsAsync(string projectId, CancellationToken cancellationToken)
        {
            IReadOnlyList<ApiDocumentDto> documents = _documentsByProject.TryGetValue(projectId, out var items)
                ? items.ToList()
                : [];
            return Task.FromResult(documents);
        }

        public Task<IReadOnlyList<ApiEndpointDto>> GetEndpointsAsync(string documentId, CancellationToken cancellationToken)
        {
            IReadOnlyList<ApiEndpointDto> endpoints = _endpointsByDocument.TryGetValue(documentId, out var items)
                ? items.ToList()
                : [];
            return Task.FromResult(endpoints);
        }

        public Task<ApiDocumentDto?> GetDocumentAsync(string projectId, string documentId, CancellationToken cancellationToken)
        {
            var document = _documentsByProject.TryGetValue(projectId, out var items)
                ? items.FirstOrDefault(item => string.Equals(item.Id, documentId, StringComparison.OrdinalIgnoreCase))
                : null;
            return Task.FromResult(document);
        }

        public Task<IResultModel<ApiImportPreviewDto>> PreviewImportFromUrlAsync(string projectId, string url, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<ApiImportPreviewDto>>(ResultModel<ApiImportPreviewDto>.Success(
                BuildPreview(projectId, "URL", url,
                [
                    new ApiEndpointDto
                    {
                        GroupName = "订单",
                        Name = "查询订单列表",
                        Method = "GET",
                        Path = "/orders"
                    },
                    new ApiEndpointDto
                    {
                        GroupName = "订单",
                        Name = "创建订单",
                        Method = "POST",
                        Path = "/orders"
                    }
                ])));
        }

        public Task<IResultModel<ApiImportPreviewDto>> PreviewImportFromFileAsync(string projectId, string filePath, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<ApiImportPreviewDto>>(ResultModel<ApiImportPreviewDto>.Success(
                BuildPreview(projectId, "FILE", filePath,
                [
                    new ApiEndpointDto
                    {
                        GroupName = "本地订单",
                        Name = "本地下单",
                        Method = "POST",
                        Path = "/local-orders"
                    }
                ])));
        }

        public Task<IResultModel<ApiDocumentDto>> ImportFromUrlAsync(string projectId, string url, CancellationToken cancellationToken)
        {
            LastImportedUrl = url;
            var document = SaveImportedDocument(projectId, "远程订单服务", "URL", url, "https://order.demo.local",
            [
                ("订单", "查询订单列表", "GET", "/orders"),
                ("订单", "创建订单", "POST", "/orders")
            ]);
            return Task.FromResult<IResultModel<ApiDocumentDto>>(ResultModel<ApiDocumentDto>.Success(document));
        }

        public Task<IResultModel<ApiDocumentDto>> ImportFromFileAsync(string projectId, string filePath, CancellationToken cancellationToken)
        {
            var document = SaveImportedDocument(projectId, "本地订单服务", "FILE", filePath, "https://order.demo.local",
            [
                ("本地订单", "本地下单", "POST", "/local-orders")
            ]);
            return Task.FromResult<IResultModel<ApiDocumentDto>>(ResultModel<ApiDocumentDto>.Success(document));
        }

        public Task DeleteImportedHttpInterfacesAsync(string projectId, IReadOnlyList<RequestCaseDto> requestCases, CancellationToken cancellationToken)
        {
            if (!_documentsByProject.TryGetValue(projectId, out var documents))
            {
                return Task.CompletedTask;
            }

            foreach (var document in documents)
            {
                if (!_endpointsByDocument.TryGetValue(document.Id, out var endpoints))
                {
                    continue;
                }

                endpoints.RemoveAll(endpoint => requestCases.Any(requestCase =>
                    string.Equals(requestCase.RequestSnapshot.Method, endpoint.Method, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(requestCase.RequestSnapshot.Url, endpoint.Path, StringComparison.OrdinalIgnoreCase)));
            }

            return Task.CompletedTask;
        }

        private ApiDocumentDto SaveImportedDocument(
            string projectId,
            string name,
            string sourceType,
            string sourceValue,
            string baseUrl,
            IReadOnlyList<(string GroupName, string Name, string Method, string Path)> endpoints)
        {
            if (!_documentsByProject.TryGetValue(projectId, out var documents))
            {
                documents = [];
                _documentsByProject[projectId] = documents;
            }

            var incomingKeys = endpoints
                .Select(endpoint => BuildImportedEndpointKey(endpoint.Method, endpoint.Path))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var document in documents.ToList())
            {
                if (!_endpointsByDocument.TryGetValue(document.Id, out var existingEndpoints))
                {
                    continue;
                }

                existingEndpoints.RemoveAll(endpoint => incomingKeys.Contains(BuildImportedEndpointKey(endpoint.Method, endpoint.Path)));
                if (existingEndpoints.Count == 0)
                {
                    _endpointsByDocument.Remove(document.Id);
                    documents.Remove(document);
                }
            }

            var importedDocument = new ApiDocumentDto
            {
                Id = Guid.NewGuid().ToString("N"),
                ProjectId = projectId,
                Name = name,
                SourceType = sourceType,
                SourceValue = sourceValue,
                BaseUrl = baseUrl,
                ImportedAt = DateTime.UtcNow
            };

            documents.Add(importedDocument);
            _endpointsByDocument[importedDocument.Id] = endpoints.Select((endpoint, index) => new ApiEndpointDto
            {
                Id = $"{importedDocument.Id}-{index}",
                DocumentId = importedDocument.Id,
                GroupName = endpoint.GroupName,
                Name = endpoint.Name,
                Method = endpoint.Method,
                Path = endpoint.Path
            }).ToList();
            return importedDocument;
        }

        private ApiImportPreviewDto BuildPreview(
            string projectId,
            string sourceType,
            string sourceValue,
            IReadOnlyList<ApiEndpointDto> endpoints)
        {
            var existingEndpoints = _documentsByProject.TryGetValue(projectId, out var documents)
                ? documents.SelectMany(document =>
                {
                    var items = _endpointsByDocument.TryGetValue(document.Id, out var documentEndpoints)
                        ? documentEndpoints
                        : [];
                    return items.Select(endpoint => new { Document = document, Endpoint = endpoint });
                }).ToList()
                : [];
            var conflicts = endpoints
                .Select(endpoint => new
                {
                    Endpoint = endpoint,
                    Existing = existingEndpoints.FirstOrDefault(item =>
                        string.Equals(item.Endpoint.Method, endpoint.Method, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(item.Endpoint.Path, endpoint.Path, StringComparison.OrdinalIgnoreCase))
                })
                .Where(item => item.Existing is not null)
                .Select(item => new ApiImportConflictDto
                {
                    ExistingDocumentId = item.Existing!.Document.Id,
                    ExistingDocumentName = item.Existing.Document.Name,
                    ExistingEndpointId = item.Existing.Endpoint.Id,
                    ExistingEndpointName = item.Existing.Endpoint.Name,
                    ImportedEndpointName = item.Endpoint.Name,
                    Method = item.Endpoint.Method,
                    Path = item.Endpoint.Path
                })
                .ToList();
            return new ApiImportPreviewDto
            {
                DocumentName = sourceType == "URL" ? "远程订单服务" : "本地订单服务",
                SourceType = sourceType,
                SourceValue = sourceValue,
                TotalEndpointCount = endpoints.Count,
                NewEndpointCount = endpoints.Count - conflicts.Count,
                ConflictCount = conflicts.Count,
                ConflictItems = conflicts
            };
        }

        private static string BuildImportedEndpointKey(string method, string path)
        {
            return $"swagger-import:{method.ToUpperInvariant()} {path}";
        }
    }

    private sealed class FakeEnvironmentVariableService : IEnvironmentVariableService
    {
        public Task<IReadOnlyList<ProjectEnvironmentDto>> GetEnvironmentsAsync(string projectId, CancellationToken cancellationToken)
        {
            IReadOnlyList<ProjectEnvironmentDto> environments =
            [
                new ProjectEnvironmentDto
                {
                    Id = "env-1",
                    ProjectId = projectId,
                    Name = "开发",
                    BaseUrl = "https://api.demo.local",
                    IsActive = true,
                    SortOrder = 1
                }
            ];
            return Task.FromResult(environments);
        }

        public Task<IResultModel<ProjectEnvironmentDto>> SaveEnvironmentAsync(ProjectEnvironmentDto environment, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<ProjectEnvironmentDto>>(ResultModel<ProjectEnvironmentDto>.Success(environment));
        }

        public Task<IResultModel<ProjectEnvironmentDto>> SetActiveEnvironmentAsync(string projectId, string environmentId, CancellationToken cancellationToken)
        {
            var environment = new ProjectEnvironmentDto
            {
                Id = environmentId,
                ProjectId = projectId,
                Name = "开发",
                BaseUrl = "https://api.demo.local",
                IsActive = true,
                SortOrder = 1
            };
            return Task.FromResult<IResultModel<ProjectEnvironmentDto>>(ResultModel<ProjectEnvironmentDto>.Success(environment));
        }

        public Task<IResultModel<bool>> DeleteEnvironmentAsync(string projectId, string environmentId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<bool>>(ResultModel<bool>.Success(true));
        }

        public Task<IReadOnlyList<EnvironmentVariableDto>> GetVariablesAsync(string environmentId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<EnvironmentVariableDto>>([]);
        }

        public Task<IResultModel<EnvironmentVariableDto>> SaveVariableAsync(EnvironmentVariableDto variable, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<EnvironmentVariableDto>>(ResultModel<EnvironmentVariableDto>.Success(variable));
        }

        public Task<IResultModel<bool>> DeleteVariableAsync(string id, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<bool>>(ResultModel<bool>.Success(true));
        }

        public Task<IReadOnlyDictionary<string, string>> GetActiveDictionaryAsync(string environmentId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
        }
    }

    private sealed class FakeRequestCaseService : IRequestCaseService
    {
        private const string ImportedEndpointKeyPrefix = "swagger-import:";

        public List<RequestCaseDto> Cases { get; } = [];

        public Task<IReadOnlyList<RequestCaseDto>> GetCasesAsync(string projectId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<RequestCaseDto>>(Cases
                .Where(item => string.Equals(item.ProjectId, projectId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.EntryType, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.FolderPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList());
        }

        public Task<IResultModel<RequestCaseDto>> SaveAsync(RequestCaseDto requestCase, CancellationToken cancellationToken)
        {
            var saved = new RequestCaseDto
            {
                Id = requestCase.Id,
                ProjectId = requestCase.ProjectId,
                EntryType = requestCase.EntryType,
                Name = requestCase.Name,
                GroupName = requestCase.GroupName,
                FolderPath = requestCase.FolderPath,
                ParentId = requestCase.ParentId,
                Tags = requestCase.Tags.ToList(),
                Description = requestCase.Description,
                RequestSnapshot = new RequestSnapshotDto
                {
                    EndpointId = requestCase.RequestSnapshot.EndpointId,
                    Name = requestCase.RequestSnapshot.Name,
                    Method = requestCase.RequestSnapshot.Method,
                    Url = requestCase.RequestSnapshot.Url,
                    Description = requestCase.RequestSnapshot.Description,
                    BodyMode = requestCase.RequestSnapshot.BodyMode,
                    BodyContent = requestCase.RequestSnapshot.BodyContent,
                    IgnoreSslErrors = requestCase.RequestSnapshot.IgnoreSslErrors,
                    QueryParameters = requestCase.RequestSnapshot.QueryParameters.ToList(),
                    PathParameters = requestCase.RequestSnapshot.PathParameters.ToList(),
                    Headers = requestCase.RequestSnapshot.Headers.ToList()
                },
                UpdatedAt = requestCase.UpdatedAt
            };
            if (string.IsNullOrWhiteSpace(saved.Id))
            {
                saved = new RequestCaseDto
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ProjectId = saved.ProjectId,
                    EntryType = saved.EntryType,
                    Name = saved.Name,
                    GroupName = saved.GroupName,
                    FolderPath = saved.FolderPath,
                    ParentId = saved.ParentId,
                    Tags = saved.Tags.ToList(),
                    Description = saved.Description,
                    RequestSnapshot = saved.RequestSnapshot,
                    UpdatedAt = saved.UpdatedAt
                };
            }

            Cases.RemoveAll(item => string.Equals(item.Id, saved.Id, StringComparison.OrdinalIgnoreCase));
            Cases.Add(saved);
            return Task.FromResult<IResultModel<RequestCaseDto>>(ResultModel<RequestCaseDto>.Success(saved));
        }

        public async Task SyncImportedHttpInterfacesAsync(string projectId, IReadOnlyList<ApiEndpointDto> endpoints, CancellationToken cancellationToken)
        {
            var existingImported = Cases
                .Where(item => string.Equals(item.ProjectId, projectId, StringComparison.OrdinalIgnoreCase))
                .Where(item => string.Equals(item.EntryType, "http-interface", StringComparison.OrdinalIgnoreCase))
                .Where(item => item.RequestSnapshot.EndpointId.StartsWith(ImportedEndpointKeyPrefix, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(item => item.RequestSnapshot.EndpointId, StringComparer.OrdinalIgnoreCase);
            var targetKeys = endpoints
                .Select(BuildImportedEndpointKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            Cases.RemoveAll(item =>
                string.Equals(item.ProjectId, projectId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.EntryType, "http-interface", StringComparison.OrdinalIgnoreCase)
                && item.RequestSnapshot.EndpointId.StartsWith(ImportedEndpointKeyPrefix, StringComparison.OrdinalIgnoreCase)
                && !targetKeys.Contains(item.RequestSnapshot.EndpointId));

            foreach (var endpoint in endpoints)
            {
                var key = BuildImportedEndpointKey(endpoint);
                existingImported.TryGetValue(key, out var existing);
                await SaveAsync(new RequestCaseDto
                {
                    Id = existing?.Id ?? string.Empty,
                    ProjectId = projectId,
                    EntryType = "http-interface",
                    Name = endpoint.Name,
                    GroupName = "接口",
                    FolderPath = endpoint.GroupName,
                    Description = endpoint.Description,
                    RequestSnapshot = new RequestSnapshotDto
                    {
                        EndpointId = key,
                        Name = endpoint.Name,
                        Method = endpoint.Method,
                        Url = endpoint.Path,
                        Description = endpoint.Description
                    },
                    UpdatedAt = DateTime.UtcNow
                }, cancellationToken);
            }
        }

        public Task<IResultModel<RequestCaseDto>> DuplicateAsync(string projectId, string id, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<RequestCaseDto>>(ResultModel<RequestCaseDto>.Failure("未实现"));
        }

        public Task<IResultModel<bool>> DeleteAsync(string projectId, string id, CancellationToken cancellationToken)
        {
            Cases.RemoveAll(item => string.Equals(item.ProjectId, projectId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult<IResultModel<bool>>(ResultModel<bool>.Success(true));
        }

        private static string BuildImportedEndpointKey(ApiEndpointDto endpoint)
        {
            return $"{ImportedEndpointKeyPrefix}{endpoint.Method.ToUpperInvariant()} {endpoint.Path}";
        }
    }

    private sealed class FakeRequestExecutionService : IRequestExecutionService
    {
        public Task<IResultModel<ResponseSnapshotDto>> SendAsync(RequestSnapshotDto request, ProjectEnvironmentDto environment, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<ResponseSnapshotDto>>(ResultModel<ResponseSnapshotDto>.Success(new ResponseSnapshotDto()));
        }
    }

    private sealed class FakeRequestHistoryService : IRequestHistoryService
    {
        public Task<IReadOnlyList<RequestHistoryItemDto>> GetHistoryAsync(string projectId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<RequestHistoryItemDto>>([]);
        }

        public Task<IResultModel<RequestHistoryItemDto>> AddAsync(string projectId, RequestSnapshotDto request, ResponseSnapshotDto? response, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<RequestHistoryItemDto>>(ResultModel<RequestHistoryItemDto>.Success(new RequestHistoryItemDto()));
        }

        public Task<IResultModel<bool>> ClearAsync(string projectId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<bool>>(ResultModel<bool>.Success(true));
        }

        public Task<IResultModel<bool>> DeleteAsync(string projectId, string id, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<bool>>(ResultModel<bool>.Success(true));
        }
    }

    private sealed class FakeFilePickerService : IFilePickerService
    {
        public Task<string?> PickSwaggerJsonFileAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<string?>(null);
        }
    }
}
