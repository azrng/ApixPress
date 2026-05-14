using Dapper;
using ApixPress.App.Data.Context;
using ApixPress.App.Models.Entities;
using ApixPress.App.Repositories.Interfaces;
using Azrng.Core.DependencyInjection;

namespace ApixPress.App.Repositories.Implementations;

public sealed class ProjectHttpSettingsRepository : IProjectHttpSettingsRepository, ITransientDependency
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ProjectHttpSettingsRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<ProjectHttpAuthSettingsEntity?> GetAuthSettingsAsync(string projectId, CancellationToken cancellationToken)
    {
        const string sql = """
                           select
                               project_id ProjectId,
                               auth_mode AuthMode,
                               bearer_token BearerToken,
                               basic_username BasicUsername,
                               basic_password BasicPassword,
                               updated_at UpdatedAt
                           from project_http_settings
                           where project_id = @ProjectId
                           limit 1
                           """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<ProjectHttpAuthSettingsEntity>(
            new CommandDefinition(sql, new { ProjectId = projectId }, cancellationToken: cancellationToken));
    }

    public async Task UpsertAuthSettingsAsync(ProjectHttpAuthSettingsEntity entity, CancellationToken cancellationToken)
    {
        const string sql = """
                           insert into project_http_settings (
                               project_id, auth_mode, bearer_token, basic_username, basic_password, updated_at
                           ) values (
                               @ProjectId, @AuthMode, @BearerToken, @BasicUsername, @BasicPassword, @UpdatedAt
                           )
                           on conflict(project_id) do update set
                               auth_mode = excluded.auth_mode,
                               bearer_token = excluded.bearer_token,
                               basic_username = excluded.basic_username,
                               basic_password = excluded.basic_password,
                               updated_at = excluded.updated_at
                           """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: cancellationToken));
    }
}
