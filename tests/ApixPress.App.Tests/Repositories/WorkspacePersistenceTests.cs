using ApixPress.App.Models.DTOs;
using ApixPress.App.Repositories.Implementations;
using ApixPress.App.Services.Implementations;
using Azrng.Core.Json;
using Microsoft.Extensions.Options;

namespace ApixPress.App.Tests.Repositories;

public sealed class WorkspacePersistenceTests
{
    [Fact]
    public async Task RequestCaseService_ShouldPersistAndLoadCase()
    {
        using var factory = new TestSqliteConnectionFactory();
        await factory.InitializeAsync();

        var repository = new RequestCaseRepository(factory);
        var service = new RequestCaseService(
            repository,
            new SysTextJsonSerializer(Options.Create(new DefaultJsonSerializerOptions())));

        var saveResult = await service.SaveAsync(new RequestCaseDto
        {
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

        var cases = await service.GetCasesAsync(CancellationToken.None);
        var saved = Assert.Single(cases);
        Assert.Equal("获取用户详情", saved.Name);
        Assert.Equal("用户", saved.GroupName);
        Assert.Equal("https://api.example.com/users/42", saved.RequestSnapshot.Url);
        Assert.Equal(2, saved.Tags.Count);
    }

    [Fact]
    public async Task EnvironmentVariableService_ShouldReturnEnabledDictionary()
    {
        using var factory = new TestSqliteConnectionFactory();
        await factory.InitializeAsync();

        var repository = new EnvironmentVariableRepository(factory);
        var service = new EnvironmentVariableService(repository);

        await service.SaveAsync(new EnvironmentVariableDto
        {
            EnvironmentName = "Default",
            Key = "baseUrl",
            Value = "https://api.example.com",
            IsEnabled = true
        }, CancellationToken.None);

        await service.SaveAsync(new EnvironmentVariableDto
        {
            EnvironmentName = "Default",
            Key = "token",
            Value = "secret",
            IsEnabled = false
        }, CancellationToken.None);

        var active = await service.GetActiveDictionaryAsync("Default", CancellationToken.None);

        Assert.Single(active);
        Assert.Equal("https://api.example.com", active["baseUrl"]);
        Assert.False(active.ContainsKey("token"));
    }

    [Fact]
    public async Task ApiWorkspaceService_ShouldImportSwaggerFileAndLoadEndpoints()
    {
        using var factory = new TestSqliteConnectionFactory();
        await factory.InitializeAsync();

        var repository = new ApiDocumentRepository(factory);
        var service = new ApiWorkspaceService(repository);
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

            var importResult = await service.ImportFromFileAsync(tempFile, CancellationToken.None);

            Assert.True(importResult.IsSuccess);
            var documents = await service.GetDocumentsAsync(CancellationToken.None);
            var document = Assert.Single(documents);
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
