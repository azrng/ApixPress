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

    private static ProjectTabViewModel CreateViewModel(FakeApiWorkspaceService apiWorkspaceService)
    {
        return new ProjectTabViewModel(
            new ProjectWorkspaceItemViewModel
            {
                Id = "project-1",
                Name = "测试项目",
                Description = "用于验证项目设置数据管理"
            },
            new FakeRequestExecutionService(),
            new FakeRequestCaseService(),
            new FakeRequestHistoryService(),
            new FakeEnvironmentVariableService(),
            apiWorkspaceService,
            new FakeFilePickerService());
    }

    private sealed class FakeApiWorkspaceService : IApiWorkspaceService
    {
        private readonly Dictionary<string, List<ApiDocumentDto>> _documentsByProject = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<ApiEndpointDto>> _endpointsByDocument = new(StringComparer.OrdinalIgnoreCase);

        public string LastImportedUrl { get; private set; } = string.Empty;

        public void SeedDocument(string projectId, string name, string sourceType, string sourceValue, string baseUrl, int endpointCount)
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

            documents.Insert(0, document);
            _endpointsByDocument[document.Id] = Enumerable.Range(1, endpointCount)
                .Select(index => new ApiEndpointDto
                {
                    Id = $"{document.Id}-{index}",
                    DocumentId = document.Id,
                    GroupName = "默认分组",
                    Name = $"接口 {index}",
                    Method = "GET",
                    Path = $"/endpoint-{index}"
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

        public Task<IResultModel<ApiDocumentDto>> ImportFromUrlAsync(string projectId, string url, CancellationToken cancellationToken)
        {
            LastImportedUrl = url;
            SeedDocument(projectId, "远程订单服务", "URL", url, "https://order.demo.local", 2);
            var document = _documentsByProject[projectId][0];
            return Task.FromResult<IResultModel<ApiDocumentDto>>(ResultModel<ApiDocumentDto>.Success(document));
        }

        public Task<IResultModel<ApiDocumentDto>> ImportFromFileAsync(string projectId, string filePath, CancellationToken cancellationToken)
        {
            SeedDocument(projectId, "本地订单服务", "FILE", filePath, "https://order.demo.local", 1);
            var document = _documentsByProject[projectId][0];
            return Task.FromResult<IResultModel<ApiDocumentDto>>(ResultModel<ApiDocumentDto>.Success(document));
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
        public Task<IReadOnlyList<RequestCaseDto>> GetCasesAsync(string projectId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<RequestCaseDto>>([]);
        }

        public Task<IResultModel<RequestCaseDto>> SaveAsync(RequestCaseDto requestCase, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<RequestCaseDto>>(ResultModel<RequestCaseDto>.Success(requestCase));
        }

        public Task<IResultModel<RequestCaseDto>> DuplicateAsync(string projectId, string id, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<RequestCaseDto>>(ResultModel<RequestCaseDto>.Failure("未实现"));
        }

        public Task<IResultModel<bool>> DeleteAsync(string projectId, string id, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<bool>>(ResultModel<bool>.Success(true));
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
