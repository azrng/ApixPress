using Dapper;
using ApixPress.App.Data.Context;
using ApixPress.App.Models.Entities;
using ApixPress.App.Repositories.Interfaces;
using Azrng.Core.DependencyInjection;

namespace ApixPress.App.Repositories.Implementations;

public sealed class ProjectEnvironmentRepository : IProjectEnvironmentRepository, ITransientDependency
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ProjectEnvironmentRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<ProjectEnvironmentEntity>> GetByProjectAsync(string projectId, CancellationToken cancellationToken)
    {
        const string sql = """
                           select
                               id Id,
                               project_id ProjectId,
                               name Name,
                               base_url BaseUrl,
                               is_active IsActive,
                               sort_order SortOrder,
                               created_at CreatedAt,
                               updated_at UpdatedAt
                           from project_environments
                           where project_id = @ProjectId
                           order by is_active desc, sort_order, name
                           """;

        using var connection = _connectionFactory.CreateConnection();
        var items = await connection.QueryAsync<ProjectEnvironmentEntity>(
            new CommandDefinition(sql, new { ProjectId = projectId }, cancellationToken: cancellationToken));
        return items.ToList();
    }

    public async Task<ProjectEnvironmentEntity?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        const string sql = """
                           select
                               id Id,
                               project_id ProjectId,
                               name Name,
                               base_url BaseUrl,
                               is_active IsActive,
                               sort_order SortOrder,
                               created_at CreatedAt,
                               updated_at UpdatedAt
                           from project_environments
                           where id = @Id
                           limit 1
                           """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<ProjectEnvironmentEntity>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<ProjectEnvironmentEntity?> GetByNameAsync(string projectId, string name, CancellationToken cancellationToken)
    {
        const string sql = """
                           select
                               id Id,
                               project_id ProjectId,
                               name Name,
                               base_url BaseUrl,
                               is_active IsActive,
                               sort_order SortOrder,
                               created_at CreatedAt,
                               updated_at UpdatedAt
                           from project_environments
                           where project_id = @ProjectId and name = @Name
                           limit 1
                           """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<ProjectEnvironmentEntity>(
            new CommandDefinition(sql, new { ProjectId = projectId, Name = name }, cancellationToken: cancellationToken));
    }

    public async Task UpsertAsync(ProjectEnvironmentEntity entity, CancellationToken cancellationToken)
    {
        const string sql = """
                           insert into project_environments (
                               id, project_id, name, base_url, is_active, sort_order, created_at, updated_at
                           ) values (
                               @Id, @ProjectId, @Name, @BaseUrl, @IsActive, @SortOrder, @CreatedAt, @UpdatedAt
                           )
                           on conflict(id) do update set
                               name = excluded.name,
                               base_url = excluded.base_url,
                               is_active = excluded.is_active,
                               sort_order = excluded.sort_order,
                               updated_at = excluded.updated_at
                           """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: cancellationToken));
    }

    public async Task SetActiveAsync(string projectId, string environmentId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition(
            "update project_environments set is_active = 0 where project_id = @ProjectId",
            new { ProjectId = projectId },
            transaction,
            cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(
            "update project_environments set is_active = 1, updated_at = @UpdatedAt where project_id = @ProjectId and id = @EnvironmentId",
            new { ProjectId = projectId, EnvironmentId = environmentId, UpdatedAt = DateTime.UtcNow },
            transaction,
            cancellationToken: cancellationToken));

        transaction.Commit();
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition(
            "delete from environment_variables where environment_id = @Id",
            new { Id = id },
            transaction,
            cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(
            "delete from project_environments where id = @Id",
            new { Id = id },
            transaction,
            cancellationToken: cancellationToken));

        transaction.Commit();
    }
}
