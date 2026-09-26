using Dapper;
using ApixPress.App.Data.Context;
using ApixPress.App.Models.Entities;
using ApixPress.App.Repositories.Interfaces;
using Azrng.Core.DependencyInjection;

namespace ApixPress.App.Repositories.Implementations;

public sealed class ProjectExportSettingsRepository : IProjectExportSettingsRepository, ITransientDependency
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ProjectExportSettingsRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<ProjectExportSettingsEntity?> GetAsync(string projectId, CancellationToken cancellationToken)
    {
        const string sql = """
                           select
                               project_id ProjectId,
                               format Format,
                               include_interfaces IncludeInterfaces,
                               include_request_cases IncludeRequestCases,
                               include_environments IncludeEnvironments,
                               include_request_history IncludeRequestHistory,
                               last_output_directory LastOutputDirectory,
                               updated_at UpdatedAt
                           from project_export_settings
                           where project_id = @ProjectId
                           limit 1
                           """;

        using var connection = _connectionFactory.CreateConnection();
        return await Task.Run(async () => await connection.QuerySingleOrDefaultAsync<ProjectExportSettingsEntity>(
            new CommandDefinition(sql, new { ProjectId = projectId }, cancellationToken: cancellationToken)));
    }

    public async Task UpsertAsync(ProjectExportSettingsEntity entity, CancellationToken cancellationToken)
    {
        const string sql = """
                           insert into project_export_settings (
                               project_id, format, include_interfaces, include_request_cases,
                               include_environments, include_request_history, last_output_directory, updated_at
                           ) values (
                               @ProjectId, @Format, @IncludeInterfaces, @IncludeRequestCases,
                               @IncludeEnvironments, @IncludeRequestHistory, @LastOutputDirectory, @UpdatedAt
                           )
                           on conflict(project_id) do update set
                               format = excluded.format,
                               include_interfaces = excluded.include_interfaces,
                               include_request_cases = excluded.include_request_cases,
                               include_environments = excluded.include_environments,
                               include_request_history = excluded.include_request_history,
                               last_output_directory = excluded.last_output_directory,
                               updated_at = excluded.updated_at
                           """;

        using var connection = _connectionFactory.CreateConnection();
        await Task.Run(async () => await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: cancellationToken)));
    }
}
