using ApixPress.App.Models.Entities;
using ApixPress.App.Repositories.Implementations;
using ApixPress.App.Services.Implementations;

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
            Name = "保留策略测试项目"
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

