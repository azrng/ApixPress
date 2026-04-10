using ApixPress.App.Models.DTOs;
using ApixPress.App.Repositories.Implementations;
using ApixPress.App.Services.Implementations;
using Azrng.Core.Json;
using Microsoft.Extensions.Options;

namespace ApixPress.App.Tests.Repositories;

public sealed class WorkspacePersistenceTests
{
    private static SysTextJsonSerializer CreateSerializer()
    {
        return new SysTextJsonSerializer(Options.Create(new DefaultJsonSerializerOptions()));
    }

    [Fact]
    public async Task ProjectWorkspaceService_ShouldCreateDefaultProjectAndInitialEnvironment()
    {
        using var factory = new TestSqliteConnectionFactory();
        await factory.InitializeAsync();

        var projectRepository = new ProjectWorkspaceRepository(factory);
        var environmentRepository = new ProjectEnvironmentRepository(factory);
        var service = new ProjectWorkspaceService(projectRepository, environmentRepository);
        var environmentService = new EnvironmentVariableService(new EnvironmentVariableRepository(factory), environmentRepository);

        var saveResult = await service.SaveAsync(new ProjectWorkspaceDto
        {
            Name = "支付中心",
            Description = "默认项目"
        }, CancellationToken.None);

        Assert.True(saveResult.IsSuccess);
        Assert.NotNull(saveResult.Data);
        Assert.True(saveResult.Data!.IsDefault);

        var startupProject = await service.GetStartupProjectAsync(CancellationToken.None);
        Assert.NotNull(startupProject);
        Assert.Equal(saveResult.Data.Id, startupProject!.Id);

        var environments = await environmentService.GetEnvironmentsAsync(saveResult.Data.Id, CancellationToken.None);
        var environment = Assert.Single(environments);
        Assert.Equal("开发", environment.Name);
        Assert.True(environment.IsActive);
    }

    [Fact]
    public async Task RequestCaseService_ShouldPersistAndLoadCaseWithinProject()
    {
        using var factory = new TestSqliteConnectionFactory();
        await factory.InitializeAsync();

        var projectRepository = new ProjectWorkspaceRepository(factory);
        var environmentRepository = new ProjectEnvironmentRepository(factory);
        var projectService = new ProjectWorkspaceService(projectRepository, environmentRepository);
        var serializer = CreateSerializer();
        var caseRepository = new RequestCaseRepository(factory);
        var caseService = new RequestCaseService(caseRepository, serializer);

        var projectA = (await projectService.SaveAsync(new ProjectWorkspaceDto
        {
            Name = "项目 A"
        }, CancellationToken.None)).Data!;
        var projectB = (await projectService.SaveAsync(new ProjectWorkspaceDto
        {
            Name = "项目 B"
        }, CancellationToken.None)).Data!;

        var saveResult = await caseService.SaveAsync(new RequestCaseDto
        {
            ProjectId = projectA.Id,
            Name = "获取用户详情",
            GroupName = "用户",
            Tags = ["smoke", "demo"],
            Description = "保存当前 GET 请求",
            RequestSnapshot = new RequestSnapshotDto
            {
                Method = "GET",
                Url = "https://api.example.com/users/42"
            },
            UpdatedAt = DateTime.UtcNow
        }, CancellationToken.None);

        Assert.True(saveResult.IsSuccess);

        var projectACases = await caseService.GetCasesAsync(projectA.Id, CancellationToken.None);
        var saved = Assert.Single(projectACases);
        Assert.Equal("获取用户详情", saved.Name);
        Assert.Equal("用户", saved.GroupName);
        Assert.Equal("https://api.example.com/users/42", saved.RequestSnapshot.Url);
        Assert.Equal(2, saved.Tags.Count);

        var projectBCases = await caseService.GetCasesAsync(projectB.Id, CancellationToken.None);
        Assert.Empty(projectBCases);
    }

    [Fact]
    public async Task EnvironmentVariableService_ShouldReturnEnabledDictionaryForActiveEnvironment()
    {
        using var factory = new TestSqliteConnectionFactory();
        await factory.InitializeAsync();

        var projectRepository = new ProjectWorkspaceRepository(factory);
        var environmentRepository = new ProjectEnvironmentRepository(factory);
        var projectService = new ProjectWorkspaceService(projectRepository, environmentRepository);
        var environmentService = new EnvironmentVariableService(new EnvironmentVariableRepository(factory), environmentRepository);

        var project = (await projectService.SaveAsync(new ProjectWorkspaceDto
        {
            Name = "环境测试"
        }, CancellationToken.None)).Data!;

        var environments = await environmentService.GetEnvironmentsAsync(project.Id, CancellationToken.None);
        var environment = Assert.Single(environments);

        await environmentService.SaveEnvironmentAsync(new ProjectEnvironmentDto
        {
            Id = environment.Id,
            ProjectId = project.Id,
            Name = environment.Name,
            BaseUrl = "https://api.example.com",
            IsActive = true,
            SortOrder = environment.SortOrder
        }, CancellationToken.None);

        await environmentService.SaveVariableAsync(new EnvironmentVariableDto
        {
            EnvironmentId = environment.Id,
            Key = "tenantId",
            Value = "demo",
            IsEnabled = true
        }, CancellationToken.None);

        await environmentService.SaveVariableAsync(new EnvironmentVariableDto
        {
            EnvironmentId = environment.Id,
            Key = "token",
            Value = "secret",
            IsEnabled = false
        }, CancellationToken.None);

        var active = await environmentService.GetActiveDictionaryAsync(environment.Id, CancellationToken.None);

        Assert.Single(active);
        Assert.Equal("demo", active["tenantId"]);
        Assert.False(active.ContainsKey("token"));
    }

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
}
