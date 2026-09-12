using ApixPress.App.Data.Context;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Repositories.Implementations;
using ApixPress.App.Services.Implementations;
using ApixPress.App.ViewModels;
using Azrng.Core.Json;
using Microsoft.Extensions.Options;

namespace ApixPress.App.Tests.Services;

public sealed class RequestCaseServiceFolderTests
{
    [Fact]
    public async Task CreateFolderAsync_ShouldCreateFolderAtRoot()
    {
        using var factory = CreateFactory();
        var projectId = await CreateProjectAsync(factory);
        var service = CreateService(factory);

        var result = await service.CreateFolderAsync(projectId, string.Empty, "WeatherForecast", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(ProjectTabRequestEntryTypes.Folder, result.Data!.EntryType);
        Assert.Equal("WeatherForecast", result.Data.Name);
        Assert.Equal(string.Empty, result.Data.FolderPath);

        var stored = await service.GetDetailAsync(projectId, result.Data.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(ProjectTabRequestEntryTypes.Folder, stored!.EntryType);
    }

    [Fact]
    public async Task CreateFolderAsync_ShouldNestUnderParentPath()
    {
        using var factory = CreateFactory();
        var projectId = await CreateProjectAsync(factory);
        var service = CreateService(factory);

        var result = await service.CreateFolderAsync(projectId, "a", "b", CancellationToken.None);

        Assert.True(result.IsSuccess);
        // 目录行的 FolderPath 存父路径，目录名存 Name，拼出完整路径 "a/b"
        Assert.Equal("a", result.Data!.FolderPath);
        Assert.Equal("b", result.Data.Name);
    }

    [Fact]
    public async Task CreateFolderAsync_ShouldRejectDepthBeyondTwoLevels()
    {
        using var factory = CreateFactory();
        var projectId = await CreateProjectAsync(factory);
        var service = CreateService(factory);

        var result = await service.CreateFolderAsync(projectId, "a/b", "c", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("目录最多支持两级", result.Message);
    }

    [Fact]
    public async Task CreateFolderAsync_ShouldRejectBlankName()
    {
        using var factory = CreateFactory();
        var projectId = await CreateProjectAsync(factory);
        var service = CreateService(factory);

        var result = await service.CreateFolderAsync(projectId, string.Empty, "   ", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("目录名称", result.Message);
    }

    [Fact]
    public async Task CreateFolderAsync_ShouldRejectDuplicateFolderInSameParent()
    {
        using var factory = CreateFactory();
        var projectId = await CreateProjectAsync(factory);
        var service = CreateService(factory);

        var first = await service.CreateFolderAsync(projectId, string.Empty, "目录A", CancellationToken.None);
        var second = await service.CreateFolderAsync(projectId, string.Empty, "目录A", CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.False(second.IsSuccess);
    }

    [Fact]
    public async Task CreateFolderAsync_ShouldAllowSameNameAsInterfaceInSameParent()
    {
        using var factory = CreateFactory();
        var projectId = await CreateProjectAsync(factory);
        var service = CreateService(factory);

        var interfaceResult = await service.SaveAsync(new RequestCaseDto
        {
            ProjectId = projectId,
            EntryType = ProjectTabRequestEntryTypes.HttpInterface,
            Name = "同名",
            GroupName = "接口",
            UpdatedAt = DateTime.UtcNow
        }, CancellationToken.None);
        var folderResult = await service.CreateFolderAsync(projectId, string.Empty, "同名", CancellationToken.None);

        Assert.True(interfaceResult.IsSuccess);
        // 唯一索引按 entry_type 区分作用域：目录与接口同名互不冲突
        Assert.True(folderResult.IsSuccess);
    }

    private static TestSqliteConnectionFactory CreateFactory()
    {
        var factory = new TestSqliteConnectionFactory();
        var initializer = new DatabaseInitializer(factory);
        initializer.Initialize();
        return factory;
    }

    private static RequestCaseService CreateService(TestSqliteConnectionFactory factory)
    {
        var serializer = new SysTextJsonSerializer(Options.Create(new DefaultJsonSerializerOptions()));
        return new RequestCaseService(new RequestCaseRepository(factory), serializer);
    }

    private static async Task<string> CreateProjectAsync(TestSqliteConnectionFactory factory)
    {
        var projectRepository = new ProjectWorkspaceRepository(factory);
        var environmentRepository = new ProjectEnvironmentRepository(factory);
        var service = new ProjectWorkspaceService(projectRepository, environmentRepository);
        var result = await service.SaveAsync(new ProjectWorkspaceDto
        {
            Name = $"目录测试项目-{Guid.NewGuid():N}"
        }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        return result.Data!.Id;
    }
}
