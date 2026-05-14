using ApixPress.App.Data.Context;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Repositories.Implementations;
using ApixPress.App.Services.Implementations;
using Dapper;

namespace ApixPress.App.Tests.Services;

public sealed class ProjectHttpSettingsServiceTests
{
    [Fact]
    public async Task SaveAuthSettingsAsync_ShouldPersistBearerSettings()
    {
        using var factory = new TestSqliteConnectionFactory();
        var initializer = new DatabaseInitializer(factory);
        initializer.Initialize();
        var service = new ProjectHttpSettingsService(new ProjectHttpSettingsRepository(factory));
        var projectId = await CreateProjectAsync(factory);

        var result = await service.SaveAuthSettingsAsync(new ProjectHttpAuthSettingsDto
        {
            ProjectId = projectId,
            AuthMode = ProjectHttpAuthSettingsDto.ModeBearer,
            BearerToken = "{{apiKey}}"
        }, CancellationToken.None);
        var reloaded = await service.GetAuthSettingsAsync(projectId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ProjectHttpAuthSettingsDto.ModeBearer, reloaded.AuthMode);
        Assert.Equal("{{apiKey}}", reloaded.BearerToken);
    }

    private static async Task<string> CreateProjectAsync(TestSqliteConnectionFactory factory)
    {
        var projectId = Guid.NewGuid().ToString("N");
        using var connection = factory.CreateConnection();
        await connection.ExecuteAsync(
            """
            insert into projects (id, name, description, is_default, created_at, updated_at)
            values (@Id, @Name, '', 1, @Now, @Now)
            """,
            new
            {
                Id = projectId,
                Name = "Auth 项目",
                Now = DateTime.UtcNow
            });

        return projectId;
    }
}
