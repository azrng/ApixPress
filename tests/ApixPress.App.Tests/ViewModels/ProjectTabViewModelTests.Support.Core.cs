using FakeAppNotificationService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeAppNotificationService;
using FakeEnvironmentVariableService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeEnvironmentVariableService;
using FakeFilePickerService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeFilePickerService;
using FakeRequestCaseService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeRequestCaseService;
using FakeRequestExecutionService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeRequestExecutionService;
using FakeRequestHistoryService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeRequestHistoryService;
using System.Collections.Generic;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels;
using Azrng.Core.Results;

namespace ApixPress.App.Tests.ViewModels;

public sealed partial class ProjectTabViewModelTests
{
    private static ProjectTabViewModel CreateViewModel(
        FakeApiWorkspaceService apiWorkspaceService,
        FakeRequestCaseService? requestCaseService = null,
        FakeAppNotificationService? appNotificationService = null)
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
            new FakeFilePickerService(),
            appNotificationService ?? new FakeAppNotificationService());
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
        public TaskCompletionSource<bool>? UrlPreviewGate { get; set; }
        public TaskCompletionSource<bool>? UrlImportGate { get; set; }

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

        public Task<IReadOnlyList<ApiEndpointDto>> GetProjectEndpointsAsync(string projectId, CancellationToken cancellationToken)
        {
            IReadOnlyList<ApiEndpointDto> endpoints = _documentsByProject.TryGetValue(projectId, out var documents)
                ? documents.SelectMany(document => _endpointsByDocument.TryGetValue(document.Id, out var items) ? items : []).ToList()
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

        public async Task<IResultModel<ApiImportPreviewDto>> PreviewImportFromUrlAsync(string projectId, string url, CancellationToken cancellationToken)
        {
            if (UrlPreviewGate is not null)
            {
                await UrlPreviewGate.Task.WaitAsync(cancellationToken);
            }

            return ResultModel<ApiImportPreviewDto>.Success(
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
                ]));
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

        public async Task<IResultModel<ApiDocumentDto>> ImportFromUrlAsync(string projectId, string url, CancellationToken cancellationToken)
        {
            if (UrlImportGate is not null)
            {
                await UrlImportGate.Task.WaitAsync(cancellationToken);
            }

            LastImportedUrl = url;
            var document = SaveImportedDocument(projectId, "远程订单服务", "URL", url, "https://order.demo.local",
            [
                ("订单", "查询订单列表", "GET", "/orders"),
                ("订单", "创建订单", "POST", "/orders")
            ]);
            return ResultModel<ApiDocumentDto>.Success(document);
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
}
