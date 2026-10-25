using ApixPress.App.Models.DTOs;
using ApixPress.App.Repositories.Implementations;
using ApixPress.App.Services.Implementations;

namespace ApixPress.App.Tests.Repositories;

public sealed partial class WorkspacePersistenceTests
{
    [Fact]
    public async Task FailedRequestHistory_ShouldPreserveErrorAfterReload()
    {
        using var factory = new TestSqliteConnectionFactory();
        await factory.InitializeAsync();
        var projects = new ProjectWorkspaceService(new ProjectWorkspaceRepository(factory), new ProjectEnvironmentRepository(factory));
        var project = (await projects.SaveAsync(new ProjectWorkspaceDto { Name = "失败历史巡检" }, CancellationToken.None)).Data
            ?? throw new InvalidOperationException("项目创建失败");
        var history = new RequestHistoryService(new RequestHistoryRepository(factory), CreateSerializer());
        var saved = await history.AddAsync(project.Id,
            new RequestSnapshotDto { Method = "GET", Url = "https://example.test/failure" },
            new ResponseSnapshotDto { ErrorMessage = "连接超时" }, CancellationToken.None);
        var id = saved.Data?.Id ?? throw new InvalidOperationException("历史记录写入失败");

        var summary = Assert.Single(await history.GetHistoryAsync(project.Id, CancellationToken.None));
        Assert.Null(summary.StatusCode);
        var detail = await history.GetDetailAsync(project.Id, id, CancellationToken.None);
        Assert.NotNull(detail?.ResponseSnapshot);
        Assert.Equal("连接超时", detail.ResponseSnapshot.ErrorMessage);
        Assert.Equal("https://example.test/failure", detail.RequestSnapshot.Url);
    }
}
