using ApixPress.App.Models.DTOs;
using ApixPress.App.Models.Entities;
using ApixPress.App.Helpers;
using ApixPress.App.Repositories.Implementations;
using ApixPress.App.Services.Implementations;
using System.Text;

namespace ApixPress.App.Tests.Repositories;

public sealed partial class WorkspacePersistenceTests
{
    [Fact]
    public async Task ApiWorkspaceService_ShouldImportSwaggerFileAndLoadEndpointsByProject()
    {
        using var factory = new TestSqliteConnectionFactory();
        await factory.InitializeAsync();

        var projectRepository = new ProjectWorkspaceRepository(factory);
        var environmentRepository = new ProjectEnvironmentRepository(factory);
        var projectService = new ProjectWorkspaceService(projectRepository, environmentRepository);
        var repository = new ApiDocumentRepository(factory);
        var service = new ApiWorkspaceService(repository);
        var project = (await projectService.SaveAsync(new ProjectWorkspaceDto
        {
            Name = "导入项目"
        }, CancellationToken.None)).Data!;
        var anotherProject = (await projectService.SaveAsync(new ProjectWorkspaceDto
        {
            Name = "空项目"
        }, CancellationToken.None)).Data!;
        var tempFile = Path.Combine(Path.GetTempPath(), $"swagger-{Guid.NewGuid():N}.json");

        try
        {
            await File.WriteAllTextAsync(tempFile, """
                                                 {
                                                   "openapi": "3.0.1",
                                                   "info": { "title": "Import Demo" },
                                                   "servers": [{ "url": "https://api.demo.local" }],
                                                   "paths": {
                                                     "/health": {
                                                       "get": {
                                                         "summary": "健康检查",
                                                         "tags": ["系统"]
                                                       }
                                                     }
                                                   }
                                                 }
                                                 """);

            var importResult = await service.ImportFromFileAsync(project.Id, tempFile, CancellationToken.None);

            Assert.True(importResult.IsSuccess);
            var documents = await service.GetDocumentsAsync(project.Id, CancellationToken.None);
            var document = Assert.Single(documents);
            Assert.Equal(project.Id, document.ProjectId);

            var otherProjectDocuments = await service.GetDocumentsAsync(anotherProject.Id, CancellationToken.None);
            Assert.Empty(otherProjectDocuments);

            var endpoints = await service.GetEndpointsAsync(document.Id, CancellationToken.None);
            var endpoint = Assert.Single(endpoints);
            Assert.Equal("GET", endpoint.Method);
            Assert.Equal("/health", endpoint.Path);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ApiWorkspaceService_ShouldLoadProjectEndpointsWithParametersInSinglePass()
    {
        using var factory = new TestSqliteConnectionFactory();
        await factory.InitializeAsync();

        var projectRepository = new ProjectWorkspaceRepository(factory);
        var environmentRepository = new ProjectEnvironmentRepository(factory);
        var projectService = new ProjectWorkspaceService(projectRepository, environmentRepository);
        var repository = new ApiDocumentRepository(factory);
        var service = new ApiWorkspaceService(repository);
        var project = (await projectService.SaveAsync(new ProjectWorkspaceDto
        {
            Name = "项目级导入接口查询"
        }, CancellationToken.None)).Data!;
        var tempFile = Path.Combine(Path.GetTempPath(), $"swagger-project-endpoints-{Guid.NewGuid():N}.json");

        try
        {
            await File.WriteAllTextAsync(tempFile, """
                                                 {
                                                   "openapi": "3.0.1",
                                                   "info": { "title": "Project Endpoint Demo" },
                                                   "paths": {
                                                     "/orders/{id}": {
                                                       "get": {
                                                         "summary": "查询订单",
                                                         "parameters": [
                                                           {
                                                             "name": "id",
                                                             "in": "path",
                                                             "required": true,
                                                             "schema": { "type": "string" }
                                                           },
                                                           {
                                                             "name": "traceId",
                                                             "in": "header",
                                                             "schema": { "default": "demo-trace" }
                                                           }
                                                         ]
                                                       }
                                                     }
                                                   }
                                                 }
                                                 """);

            var importResult = await service.ImportFromFileAsync(project.Id, tempFile, CancellationToken.None);

            Assert.True(importResult.IsSuccess);

            var endpoints = await service.GetProjectEndpointsAsync(project.Id, CancellationToken.None);
            var endpoint = Assert.Single(endpoints);
            Assert.Equal("/orders/{id}", endpoint.Path);
            Assert.Equal(2, endpoint.Parameters.Count);
            Assert.Contains(endpoint.Parameters, item => item.ParameterType == RequestParameterKind.Path && item.Name == "id");
            Assert.Contains(endpoint.Parameters, item => item.ParameterType == RequestParameterKind.Header && item.Name == "traceId");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ApiWorkspaceService_ShouldKeepPreviousImportedDocumentsWhenPathsDoNotConflict()
    {
        using var factory = new TestSqliteConnectionFactory();
        await factory.InitializeAsync();

        var projectRepository = new ProjectWorkspaceRepository(factory);
        var environmentRepository = new ProjectEnvironmentRepository(factory);
        var projectService = new ProjectWorkspaceService(projectRepository, environmentRepository);
        var repository = new ApiDocumentRepository(factory);
        var service = new ApiWorkspaceService(repository);
        var project = (await projectService.SaveAsync(new ProjectWorkspaceDto
        {
            Name = "覆盖导入项目"
        }, CancellationToken.None)).Data!;
        var firstFile = Path.Combine(Path.GetTempPath(), $"swagger-first-{Guid.NewGuid():N}.json");
        var secondFile = Path.Combine(Path.GetTempPath(), $"swagger-second-{Guid.NewGuid():N}.json");

        try
        {
            await File.WriteAllTextAsync(firstFile, """
                                                  {
                                                    "openapi": "3.0.1",
                                                    "info": { "title": "First Import" },
                                                    "paths": {
                                                      "/health": {
                                                        "get": {
                                                          "summary": "健康检查"
                                                        }
                                                      }
                                                    }
                                                  }
                                                  """);
            await File.WriteAllTextAsync(secondFile, """
                                                   {
                                                     "openapi": "3.0.1",
                                                     "info": { "title": "Second Import" },
                                                     "paths": {
                                                       "/users": {
                                                         "post": {
                                                           "summary": "创建用户"
                                                         }
                                                       }
                                                     }
                                                   }
                                                   """);

            var firstResult = await service.ImportFromFileAsync(project.Id, firstFile, CancellationToken.None);
            var secondResult = await service.ImportFromFileAsync(project.Id, secondFile, CancellationToken.None);

            Assert.True(firstResult.IsSuccess);
            Assert.True(secondResult.IsSuccess);

            var documents = await service.GetDocumentsAsync(project.Id, CancellationToken.None);
            Assert.Equal(2, documents.Count);
            Assert.Contains(documents, item => item.Name == "First Import");
            Assert.Contains(documents, item => item.Name == "Second Import");

            var firstDocument = Assert.Single(documents, item => item.Name == "First Import");
            var secondDocument = Assert.Single(documents, item => item.Name == "Second Import");

            var firstEndpoints = await service.GetEndpointsAsync(firstDocument.Id, CancellationToken.None);
            var firstEndpoint = Assert.Single(firstEndpoints);
            Assert.Equal("GET", firstEndpoint.Method);
            Assert.Equal("/health", firstEndpoint.Path);

            var secondEndpoints = await service.GetEndpointsAsync(secondDocument.Id, CancellationToken.None);
            var secondEndpoint = Assert.Single(secondEndpoints);
            Assert.Equal("POST", secondEndpoint.Method);
            Assert.Equal("/users", secondEndpoint.Path);
        }
        finally
        {
            if (File.Exists(firstFile))
            {
                File.Delete(firstFile);
            }

            if (File.Exists(secondFile))
            {
                File.Delete(secondFile);
            }
        }
    }

    [Fact]
    public async Task ApiWorkspaceService_ShouldOverwriteOnlyConflictingImportedEndpoints()
    {
        using var factory = new TestSqliteConnectionFactory();
        await factory.InitializeAsync();

        var projectRepository = new ProjectWorkspaceRepository(factory);
        var environmentRepository = new ProjectEnvironmentRepository(factory);
        var projectService = new ProjectWorkspaceService(projectRepository, environmentRepository);
        var repository = new ApiDocumentRepository(factory);
        var service = new ApiWorkspaceService(repository);
        var project = (await projectService.SaveAsync(new ProjectWorkspaceDto
        {
            Name = "冲突覆盖项目"
        }, CancellationToken.None)).Data!;
        var firstFile = Path.Combine(Path.GetTempPath(), $"swagger-first-conflict-{Guid.NewGuid():N}.json");
        var secondFile = Path.Combine(Path.GetTempPath(), $"swagger-second-conflict-{Guid.NewGuid():N}.json");

        try
        {
            await File.WriteAllTextAsync(firstFile, """
                                                  {
                                                    "openapi": "3.0.1",
                                                    "info": { "title": "First Import" },
                                                    "paths": {
                                                      "/orders": {
                                                        "get": {
                                                          "summary": "旧的订单查询"
                                                        }
                                                      },
                                                      "/health": {
                                                        "get": {
                                                          "summary": "健康检查"
                                                        }
                                                      }
                                                    }
                                                  }
                                                  """);
            await File.WriteAllTextAsync(secondFile, """
                                                   {
                                                     "openapi": "3.0.1",
                                                     "info": { "title": "Second Import" },
                                                     "paths": {
                                                       "/orders": {
                                                         "get": {
                                                           "summary": "新的订单查询"
                                                         }
                                                       }
                                                     }
                                                   }
                                                   """);

            var firstResult = await service.ImportFromFileAsync(project.Id, firstFile, CancellationToken.None);
            var secondResult = await service.ImportFromFileAsync(project.Id, secondFile, CancellationToken.None);

            Assert.True(firstResult.IsSuccess);
            Assert.True(secondResult.IsSuccess);

            var documents = await service.GetDocumentsAsync(project.Id, CancellationToken.None);
            Assert.Equal(2, documents.Count);

            var firstDocument = Assert.Single(documents, item => item.Name == "First Import");
            var secondDocument = Assert.Single(documents, item => item.Name == "Second Import");

            var firstEndpoints = await service.GetEndpointsAsync(firstDocument.Id, CancellationToken.None);
            var remainingFirstEndpoint = Assert.Single(firstEndpoints);
            Assert.Equal("/health", remainingFirstEndpoint.Path);

            var secondEndpoints = await service.GetEndpointsAsync(secondDocument.Id, CancellationToken.None);
            var importedEndpoint = Assert.Single(secondEndpoints);
            Assert.Equal("/orders", importedEndpoint.Path);
            Assert.Equal("GET", importedEndpoint.Method);
        }
        finally
        {
            if (File.Exists(firstFile))
            {
                File.Delete(firstFile);
            }

            if (File.Exists(secondFile))
            {
                File.Delete(secondFile);
            }
        }
    }

    [Fact]
    public async Task ApiWorkspaceService_ShouldDeleteImportedEndpointsFromStoredDocument()
    {
        using var factory = new TestSqliteConnectionFactory();
        await factory.InitializeAsync();

        var projectRepository = new ProjectWorkspaceRepository(factory);
        var environmentRepository = new ProjectEnvironmentRepository(factory);
        var projectService = new ProjectWorkspaceService(projectRepository, environmentRepository);
        var repository = new ApiDocumentRepository(factory);
        var service = new ApiWorkspaceService(repository);
        var project = (await projectService.SaveAsync(new ProjectWorkspaceDto
        {
            Name = "删除导入接口项目"
        }, CancellationToken.None)).Data!;
        var tempFile = Path.Combine(Path.GetTempPath(), $"swagger-delete-{Guid.NewGuid():N}.json");

        try
        {
            await File.WriteAllTextAsync(tempFile, """
                                                 {
                                                   "openapi": "3.0.1",
                                                   "info": { "title": "Delete Demo" },
                                                   "paths": {
                                                     "/users": {
                                                       "get": {
                                                         "summary": "查询用户列表"
                                                       }
                                                     },
                                                     "/orders": {
                                                       "post": {
                                                         "summary": "创建订单"
                                                       }
                                                     }
                                                   }
                                                 }
                                                 """);

            var importResult = await service.ImportFromFileAsync(project.Id, tempFile, CancellationToken.None);
            Assert.True(importResult.IsSuccess);

            var document = Assert.Single(await service.GetDocumentsAsync(project.Id, CancellationToken.None));
            await service.DeleteImportedHttpInterfacesAsync(project.Id,
            [
                new RequestCaseDto
                {
                    ProjectId = project.Id,
                    EntryType = "http-interface",
                    Name = "查询用户列表",
                    RequestSnapshot = new RequestSnapshotDto
                    {
                        EndpointId = "swagger-import:GET /users",
                        Method = "GET",
                        Url = "/users"
                    }
                }
            ], CancellationToken.None);

            var endpoints = await service.GetEndpointsAsync(document.Id, CancellationToken.None);
            var endpoint = Assert.Single(endpoints);
            Assert.Equal("POST", endpoint.Method);
            Assert.Equal("/orders", endpoint.Path);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ApiWorkspaceService_ShouldFallbackBaseUrlForLegacyUrlImportedDocuments()
    {
        using var factory = new TestSqliteConnectionFactory();
        await factory.InitializeAsync();

        var projectRepository = new ProjectWorkspaceRepository(factory);
        var environmentRepository = new ProjectEnvironmentRepository(factory);
        var projectService = new ProjectWorkspaceService(projectRepository, environmentRepository);
        var repository = new ApiDocumentRepository(factory);
        var service = new ApiWorkspaceService(repository);
        var project = (await projectService.SaveAsync(new ProjectWorkspaceDto
        {
            Name = "旧文档回填项目"
        }, CancellationToken.None)).Data!;

        await repository.SaveDocumentGraphAsync(
            new ApiDocumentEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                ProjectId = project.Id,
                Name = "SwaggerAPI",
                SourceType = "URL",
                SourceValue = "http://localhost:5000/swagger/v1/swagger.json",
                BaseUrl = string.Empty,
                RawJson = "{}",
                ImportedAt = DateTime.UtcNow
            },
            [],
            [],
            CancellationToken.None);

        var documents = await service.GetDocumentsAsync(project.Id, CancellationToken.None);
        var document = Assert.Single(documents);

        Assert.Equal("http://localhost:5000", document.BaseUrl);
    }

    [Fact]
    public async Task ApiWorkspaceService_ShouldReusePreviewedUrlPayloadForImport()
    {
        using var factory = new TestSqliteConnectionFactory();
        await factory.InitializeAsync();

        var projectRepository = new ProjectWorkspaceRepository(factory);
        var environmentRepository = new ProjectEnvironmentRepository(factory);
        var projectService = new ProjectWorkspaceService(projectRepository, environmentRepository);
        var repository = new ApiDocumentRepository(factory);
        var downloadCount = 0;
        var parseCount = 0;
        var service = new ApiWorkspaceService(
            repository,
            (_, _) =>
            {
                downloadCount++;
                return Task.FromResult("""
                                       {
                                         "openapi": "3.0.1",
                                         "info": { "title": "Remote Import Demo" },
                                         "paths": {
                                           "/orders": {
                                             "get": {
                                               "summary": "查询订单"
                                             }
                                           }
                                           }
                                       }
                                       """);
            },
            (filePath, cancellationToken) => File.ReadAllTextAsync(filePath, cancellationToken),
            (json, sourceType, sourceValue) =>
            {
                parseCount++;
                return OpenApiJsonParser.Parse(json, sourceType, sourceValue);
            });
        var project = (await projectService.SaveAsync(new ProjectWorkspaceDto
        {
            Name = "URL 导入复用项目"
        }, CancellationToken.None)).Data!;
        const string url = "http://demo.local/swagger/v1/swagger.json";

        var previewResult = await service.PreviewImportFromUrlAsync(project.Id, url, CancellationToken.None);
        var importResult = await service.ImportFromUrlAsync(project.Id, url, CancellationToken.None);

        Assert.True(previewResult.IsSuccess);
        Assert.True(importResult.IsSuccess);
        Assert.Equal(1, downloadCount);
        Assert.Equal(1, parseCount);

        var document = Assert.Single(await service.GetDocumentsAsync(project.Id, CancellationToken.None));
        Assert.Equal("Remote Import Demo", document.Name);
    }

    [Fact]
    public async Task ApiWorkspaceService_ShouldReusePreviewedFilePreparationForImport()
    {
        using var factory = new TestSqliteConnectionFactory();
        await factory.InitializeAsync();

        var projectRepository = new ProjectWorkspaceRepository(factory);
        var environmentRepository = new ProjectEnvironmentRepository(factory);
        var projectService = new ProjectWorkspaceService(projectRepository, environmentRepository);
        var repository = new ApiDocumentRepository(factory);
        var fileReadCount = 0;
        var parseCount = 0;
        var service = new ApiWorkspaceService(
            repository,
            (_, _) => throw new InvalidOperationException("当前测试不应触发 URL 下载。"),
            async (filePath, cancellationToken) =>
            {
                fileReadCount++;
                return await File.ReadAllTextAsync(filePath, cancellationToken);
            },
            (json, sourceType, sourceValue) =>
            {
                parseCount++;
                return OpenApiJsonParser.Parse(json, sourceType, sourceValue);
            });
        var project = (await projectService.SaveAsync(new ProjectWorkspaceDto
        {
            Name = "文件导入复用项目"
        }, CancellationToken.None)).Data!;
        var tempFile = Path.Combine(Path.GetTempPath(), $"swagger-preview-import-{Guid.NewGuid():N}.json");

        try
        {
            await File.WriteAllTextAsync(tempFile, """
                                                 {
                                                   "openapi": "3.0.1",
                                                   "info": { "title": "File Import Demo" },
                                                   "paths": {
                                                     "/orders": {
                                                       "get": {
                                                         "summary": "查询订单"
                                                       }
                                                     }
                                                   }
                                                 }
                                                 """);

            var previewResult = await service.PreviewImportFromFileAsync(project.Id, tempFile, CancellationToken.None);
            var importResult = await service.ImportFromFileAsync(project.Id, tempFile, CancellationToken.None);

            Assert.True(previewResult.IsSuccess);
            Assert.True(importResult.IsSuccess);
            Assert.Equal(1, fileReadCount);
            Assert.Equal(1, parseCount);

            var document = Assert.Single(await service.GetDocumentsAsync(project.Id, CancellationToken.None));
            Assert.Equal("File Import Demo", document.Name);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ProjectDataExportService_ShouldImportProjectPackageIntoDocumentsAndCases()
    {
        using var factory = new TestSqliteConnectionFactory();
        await factory.InitializeAsync();

        var projectRepository = new ProjectWorkspaceRepository(factory);
        var environmentRepository = new ProjectEnvironmentRepository(factory);
        var projectService = new ProjectWorkspaceService(projectRepository, environmentRepository);
        var serializer = CreateSerializer();
        var caseRepository = new RequestCaseRepository(factory);
        var caseService = new RequestCaseService(caseRepository, serializer);
        var apiRepository = new ApiDocumentRepository(factory);
        var service = new ProjectDataExportService(apiRepository, caseService);
        var project = (await projectService.SaveAsync(new ProjectWorkspaceDto
        {
            Name = "项目数据包导入项目"
        }, CancellationToken.None)).Data!;
        var tempFile = Path.Combine(Path.GetTempPath(), $"project-package-{Guid.NewGuid():N}.apixpkg.json");

        try
        {
            await File.WriteAllTextAsync(tempFile, """
                                                 {
                                                   "SchemaVersion": "apixpress.project-data.v1",
                                                   "ExportedAt": "2026-04-27T03:00:00Z",
                                                   "Project": {
                                                     "Id": "source-project",
                                                     "Name": "中文项目",
                                                     "Description": "用于验证导入"
                                                   },
                                                   "Interfaces": [
                                                     {
                                                       "Id": "interface-1",
                                                       "Name": "查询订单",
                                                       "GroupName": "接口",
                                                       "FolderPath": "订单",
                                                       "ParentId": "",
                                                       "Tags": ["core"],
                                                       "Description": "查询订单接口",
                                                       "RequestSnapshot": {
                                                         "EndpointId": "swagger-import:GET /orders",
                                                         "Name": "查询订单",
                                                         "Method": "GET",
                                                         "Url": "/orders",
                                                         "Description": "查询订单接口",
                                                         "BodyMode": "None",
                                                         "BodyContent": "",
                                                         "IgnoreSslErrors": false,
                                                         "QueryParameters": [
                                                           {
                                                             "Name": "page",
                                                             "Value": "1",
                                                             "IsEnabled": true
                                                           }
                                                         ],
                                                         "PathParameters": [],
                                                         "Headers": [
                                                           {
                                                             "Name": "X-Tenant",
                                                             "Value": "demo",
                                                             "IsEnabled": true
                                                           }
                                                         ]
                                                       },
                                                       "UpdatedAt": "2026-04-27T03:10:00Z"
                                                     }
                                                   ],
                                                   "TestCases": [
                                                     {
                                                       "Id": "case-1",
                                                       "Name": "查询订单-成功",
                                                       "GroupName": "用例",
                                                       "FolderPath": "订单",
                                                       "ParentId": "interface-1",
                                                       "Tags": ["happy-path"],
                                                       "Description": "成功场景",
                                                       "RequestSnapshot": {
                                                         "EndpointId": "swagger-import:GET /orders",
                                                         "Name": "查询订单-成功",
                                                         "Method": "GET",
                                                         "Url": "/orders",
                                                         "Description": "成功场景",
                                                         "BodyMode": "None",
                                                         "BodyContent": "",
                                                         "IgnoreSslErrors": false,
                                                         "QueryParameters": [],
                                                         "PathParameters": [],
                                                         "Headers": []
                                                       },
                                                       "UpdatedAt": "2026-04-27T03:20:00Z"
                                                     }
                                                   ]
                                                 }
                                                 """, new UTF8Encoding(true));

            var previewResult = await service.PreviewImportPackageAsync(project.Id, tempFile, CancellationToken.None);
            var importResult = await service.ImportPackageAsync(project.Id, tempFile, CancellationToken.None);

            Assert.True(previewResult.IsSuccess);
            Assert.NotNull(previewResult.Data);
            Assert.Equal("中文项目", previewResult.Data!.DocumentName);
            Assert.Equal(1, previewResult.Data.TotalEndpointCount);

            Assert.True(importResult.IsSuccess);
            Assert.NotNull(importResult.Data);
            Assert.Equal("中文项目", importResult.Data!.Name);
            Assert.Equal("APIXPKG", importResult.Data.SourceType);

            var documents = await apiRepository.GetDocumentsAsync(project.Id, CancellationToken.None);
            var document = Assert.Single(documents);
            Assert.Equal("中文项目", document.Name);
            Assert.Equal("APIXPKG", document.SourceType);

            var endpoints = await apiRepository.GetEndpointDetailsByProjectIdAsync(project.Id, CancellationToken.None);
            var endpoint = Assert.Single(endpoints);
            Assert.Equal("订单", endpoint.GroupName);
            Assert.Equal("GET", endpoint.Method);
            Assert.Equal("/orders", endpoint.Path);

            var cases = await caseService.GetCaseDetailsAsync(project.Id, CancellationToken.None);
            var interfaceCase = Assert.Single(cases, item => item.EntryType == "http-interface");
            var testCase = Assert.Single(cases, item => item.EntryType == "http-case");
            Assert.Equal("查询订单", interfaceCase.Name);
            Assert.Equal("订单", interfaceCase.FolderPath);
            Assert.Equal("swagger-import:GET /orders", interfaceCase.RequestSnapshot.EndpointId);
            Assert.Equal(interfaceCase.Id, testCase.ParentId);
            Assert.Equal("查询订单-成功", testCase.Name);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
