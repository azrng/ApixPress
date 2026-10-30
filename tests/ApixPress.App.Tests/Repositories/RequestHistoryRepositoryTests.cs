using ApixPress.App.Models.Entities;
using ApixPress.App.Models.DTOs;
using Azrng.Core.Json;
using Microsoft.Extensions.Options;
using ApixPress.App.Repositories.Implementations;
using ApixPress.App.Services.Implementations;
using ApixPress.App.Services.Interfaces;

namespace ApixPress.App.Tests.Repositories;

public sealed class RequestHistoryRepositoryTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetHistoryAsync_ShouldReadMixedNullAndIntegerStatusCodes(bool failureFirst)
    {
        using var factory = new TestSqliteConnectionFactory();
        await factory.InitializeAsync();
        var projectId = await CreateProjectAsync(factory);
        var repository = new RequestHistoryRepository(factory);
        var success = CreateEntity(projectId, "success", DateTime.UtcNow.AddSeconds(failureFirst ? -1 : 1));
        success.ResponseSnapshotJson = "{\"statusCode\":200,\"durationMs\":12}";
        var failure = CreateEntity(projectId, "failure", DateTime.UtcNow);
        failure.ResponseSnapshotJson = "{\"errorMessage\":\"连接失败\"}";
        await repository.UpsertAsync(success, CancellationToken.None);
        await repository.UpsertAsync(failure, CancellationToken.None);

        var rows = await repository.GetHistoryAsync(projectId, 10, CancellationToken.None);

        Assert.Equal(2, rows.Count);
        Assert.Equal(200, rows.Single(row => row.Id == "success").StatusCode);
        Assert.Null(rows.Single(row => row.Id == "failure").StatusCode);
    }

    [Fact]
    public async Task GetHistoryPageAsync_ShouldUseStableIdCursorWhenTimestampsMatch()
    {
        using var factory = new TestSqliteConnectionFactory();
        await factory.InitializeAsync();
        var projectId = await CreateProjectAsync(factory);
        var repository = new RequestHistoryRepository(factory);
        var timestamp = new DateTime(2026, 4, 24, 10, 0, 0, DateTimeKind.Utc);
        foreach (var id in new[] { "history-a", "history-b", "history-c", "history-d", "history-e" })
        {
            await repository.UpsertAsync(CreateEntity(projectId, id, timestamp), CancellationToken.None);
        }

        var firstPage = await repository.GetHistoryPageAsync(projectId, null, null, 2, CancellationToken.None);
        var secondPage = await repository.GetHistoryPageAsync(
            projectId,
            firstPage[^1].Timestamp,
            firstPage[^1].Id,
            2,
            CancellationToken.None);
        var thirdPage = await repository.GetHistoryPageAsync(
            projectId,
            secondPage[^1].Timestamp,
            secondPage[^1].Id,
            2,
            CancellationToken.None);

        Assert.Equal(["history-e", "history-d"], firstPage.Select(item => item.Id));
        Assert.Equal(["history-c", "history-b"], secondPage.Select(item => item.Id));
        Assert.Equal("history-a", Assert.Single(thirdPage).Id);
    }

    [Fact]
    public async Task GetAllHistoryAsync_ShouldIncludeMoreThanFiftySnapshots()
    {
        using var factory = new TestSqliteConnectionFactory();
        await factory.InitializeAsync();
        var sourceProject = await CreateProjectAsync(factory);
        var repository = new RequestHistoryRepository(factory);
        var serializer = new SysTextJsonSerializer(Options.Create(new DefaultJsonSerializerOptions()));
        var service = new RequestHistoryService(repository, serializer);
        var timestamp = DateTime.UtcNow.AddDays(-1);
        var items = Enumerable.Range(0, 51).Select(i => new RequestHistoryItemDto
        {
            Timestamp = timestamp.AddSeconds(i),
            RequestSnapshot = new RequestSnapshotDto
            {
                Method = "POST", Url = "/orders", BodyContent = $"body-{i}",
                Headers = [new RequestKeyValueDto { Name = "X-Test", Value = "value" }]
            },
            ResponseSnapshot = new ResponseSnapshotDto { StatusCode = 200, Content = $"response-{i}" }
        }).ToList();

        foreach (var item in items)
        {
            await repository.UpsertAsync(new RequestHistoryEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                ProjectId = sourceProject,
                Timestamp = item.Timestamp,
                RequestSnapshotJson = serializer.ToJson(item.RequestSnapshot),
                ResponseSnapshotJson = serializer.ToJson(item.ResponseSnapshot!)
            }, CancellationToken.None);
        }

        var exported = await service.GetAllHistoryAsync(sourceProject, CancellationToken.None);
        Assert.Equal(51, exported.Count);
        Assert.Equal(50, (await service.GetHistoryAsync(sourceProject, CancellationToken.None)).Count);
        var first = exported.Single(item => item.RequestSnapshot.BodyContent == "body-0");
        Assert.Equal("response-0", first.ResponseSnapshot!.Content);
        Assert.Equal("value", Assert.Single(first.RequestSnapshot.Headers).Value);
        Assert.Equal(timestamp, first.Timestamp);
    }

    [Fact]
    public async Task DeleteOlderThanAsync_ShouldRemoveOnlyExpiredRows()
    {
        using var factory = new TestSqliteConnectionFactory();
        await factory.InitializeAsync();
        var projectId = await CreateProjectAsync(factory);
        var repository = new RequestHistoryRepository(factory);

        await repository.UpsertAsync(CreateEntity(projectId, "old-1", DateTime.UtcNow.AddDays(-8)), CancellationToken.None);
        await repository.UpsertAsync(CreateEntity(projectId, "old-2", DateTime.UtcNow.AddDays(-30)), CancellationToken.None);
        await repository.UpsertAsync(CreateEntity(projectId, "new-1", DateTime.UtcNow.AddHours(-1)), CancellationToken.None);

        var deleted = await repository.DeleteOlderThanAsync(DateTime.UtcNow.AddDays(-7), CancellationToken.None);

        Assert.Equal(2, deleted);
        var remaining = await repository.GetHistoryAsync(projectId, 10, CancellationToken.None);
        var remainingId = Assert.Single(remaining).Id;
        Assert.Equal("new-1", remainingId);
    }

    [Fact]
    public async Task DeleteOlderThanAsync_WhenNothingExpired_ShouldDeleteNothing()
    {
        using var factory = new TestSqliteConnectionFactory();
        await factory.InitializeAsync();
        var projectId = await CreateProjectAsync(factory);
        var repository = new RequestHistoryRepository(factory);
        await repository.UpsertAsync(CreateEntity(projectId, "fresh-1", DateTime.UtcNow), CancellationToken.None);

        var deleted = await repository.DeleteOlderThanAsync(DateTime.UtcNow.AddDays(-7), CancellationToken.None);

        Assert.Equal(0, deleted);
        Assert.Single(await repository.GetHistoryAsync(projectId, 10, CancellationToken.None));
    }

    /// <summary>request_history.project_id 有外键约束：先按既有模式落一个真实项目。</summary>
    private static async Task<string> CreateProjectAsync(TestSqliteConnectionFactory factory)
    {
        var service = new ProjectWorkspaceService(new ProjectWorkspaceRepository(factory), new ProjectEnvironmentRepository(factory));
        var saveResult = await service.SaveAsync(new Models.DTOs.ProjectWorkspaceDto
        {
            Name = $"保留策略测试项目-{Guid.NewGuid():N}"
        }, CancellationToken.None);
        Assert.True(saveResult.IsSuccess);
        return saveResult.Data!.Id;
    }

    private static RequestHistoryEntity CreateEntity(string projectId, string id, DateTime timestamp)
    {
        return new RequestHistoryEntity
        {
            Id = id,
            ProjectId = projectId,
            Timestamp = timestamp,
            RequestSnapshotJson = "{}",
            ResponseSnapshotJson = "{}"
        };
    }
}

