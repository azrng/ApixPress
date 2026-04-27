using System.Text.Json;
using FakeRequestCaseService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeRequestCaseService;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Models.Entities;
using ApixPress.App.Repositories.Interfaces;
using ApixPress.App.Services.Implementations;

namespace ApixPress.App.Tests.Services;

public sealed class ProjectDataExportServiceTests
{
    [Fact]
    public async Task ExportAsync_ShouldWriteReadableProjectPackageJson()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"project-export-{Guid.NewGuid():N}.apixpkg.json");
        var requestCaseService = new FakeRequestCaseService();
        requestCaseService.Cases.Add(new RequestCaseDto
        {
            Id = "interface-1",
            ProjectId = "project-1",
            EntryType = "http-interface",
            Name = "查询订单",
            GroupName = "接口",
            FolderPath = "订单",
            Description = "订单查询接口",
            Tags = ["orders", "read"],
            RequestSnapshot = new RequestSnapshotDto
            {
                EndpointId = "swagger-import:GET /orders",
                Name = "查询订单",
                Method = "GET",
                Url = "/orders",
                QueryParameters =
                [
                    new RequestKeyValueDto
                    {
                        Name = "page",
                        Value = "1",
                        IsEnabled = true
                    }
                ]
            },
            UpdatedAt = new DateTime(2026, 4, 27, 2, 0, 0, DateTimeKind.Utc)
        });
        requestCaseService.Cases.Add(new RequestCaseDto
        {
            Id = "case-1",
            ProjectId = "project-1",
            EntryType = "http-case",
            Name = "查询订单-成功",
            GroupName = "接口",
            FolderPath = "订单",
            ParentId = "interface-1",
            Description = "查询成功场景",
            Tags = ["happy-path"],
            RequestSnapshot = new RequestSnapshotDto
            {
                EndpointId = "swagger-import:GET /orders",
                Name = "查询订单-成功",
                Method = "GET",
                Url = "/orders",
                Headers =
                [
                    new RequestKeyValueDto
                    {
                        Name = "Authorization",
                        Value = "Bearer demo-token",
                        IsEnabled = true
                    }
                ]
            },
            UpdatedAt = new DateTime(2026, 4, 27, 3, 0, 0, DateTimeKind.Utc)
        });

        var service = new ProjectDataExportService(new FakeApiDocumentRepository(), requestCaseService);

        try
        {
            var result = await service.ExportAsync(new ProjectDataExportRequestDto
            {
                ProjectId = "project-1",
                ProjectName = "订单项目",
                ProjectDescription = "导出测试",
                OutputFilePath = tempFile
            }, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal(1, result.Data!.InterfaceCount);
            Assert.Equal(1, result.Data.TestCaseCount);

            var bytes = await File.ReadAllBytesAsync(tempFile);
            Assert.True(bytes.Length > 3);
            Assert.Equal(0xEF, bytes[0]);
            Assert.Equal(0xBB, bytes[1]);
            Assert.Equal(0xBF, bytes[2]);

            var json = await File.ReadAllTextAsync(tempFile);
            Assert.Contains(Environment.NewLine, json);
            Assert.Contains("\"订单项目\"", json);
            Assert.DoesNotContain("\\u8ba2\\u5355\\u9879\\u76ee", json);

            var package = JsonSerializer.Deserialize<ProjectDataExportPackageDto>(json);
            Assert.NotNull(package);
            Assert.Equal(ProjectDataExportPackageDto.CurrentSchemaVersion, package!.SchemaVersion);
            Assert.Equal("project-1", package.Project.Id);
            Assert.Equal("订单项目", package.Project.Name);
            Assert.Single(package.Interfaces);
            Assert.Single(package.TestCases);
            Assert.Equal("查询订单", package.Interfaces[0].Name);
            Assert.Equal("orders", package.Interfaces[0].Tags[0]);
            Assert.Equal("/orders", package.Interfaces[0].RequestSnapshot.Url);
            Assert.Equal("interface-1", package.TestCases[0].ParentId);
            Assert.Equal("Authorization", package.TestCases[0].RequestSnapshot.Headers[0].Name);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private sealed class FakeApiDocumentRepository : IApiDocumentRepository
    {
        public Task<IReadOnlyList<ApiDocumentEntity>> GetDocumentsAsync(string projectId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ApiDocumentEntity>>([]);
        }

        public Task<ApiDocumentEntity?> GetByIdAsync(string projectId, string documentId, CancellationToken cancellationToken)
        {
            return Task.FromResult<ApiDocumentEntity?>(null);
        }

        public Task<IReadOnlyList<ApiEndpointEntity>> GetEndpointsByDocumentIdAsync(string documentId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ApiEndpointEntity>>([]);
        }

        public Task<IReadOnlyList<ApiProjectEndpointEntity>> GetEndpointsByProjectIdAsync(string projectId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ApiProjectEndpointEntity>>([]);
        }

        public Task<IReadOnlyList<ApiEndpointEntity>> GetEndpointDetailsByProjectIdAsync(string projectId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ApiEndpointEntity>>([]);
        }

        public Task<IReadOnlyList<RequestParameterEntity>> GetParametersByEndpointIdsAsync(IEnumerable<string> endpointIds, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<RequestParameterEntity>>([]);
        }

        public Task DeleteEndpointsByIdsAsync(IEnumerable<string> endpointIds, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task SaveDocumentGraphAsync(ApiDocumentEntity document, IReadOnlyList<ApiEndpointEntity> endpoints, IReadOnlyList<RequestParameterEntity> parameters, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
