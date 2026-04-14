using Dapper;
using ApixPress.App.Data.Context;
using ApixPress.App.Models.Entities;
using ApixPress.App.Repositories.Interfaces;
using Azrng.Core.DependencyInjection;

namespace ApixPress.App.Repositories.Implementations;

public sealed class ProjectWorkspaceRepository : IProjectWorkspaceRepository, ITransientDependency
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ProjectWorkspaceRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<ProjectWorkspaceEntity>> GetProjectsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
                           select
                               id Id,
                               name Name,
                               description Description,
                               is_default IsDefault,
                               created_at CreatedAt,
                               updated_at UpdatedAt
                           from projects
                           order by is_default desc, updated_at desc, name
                           """;

        using var connection = _connectionFactory.CreateConnection();
        var items = await connection.QueryAsync<ProjectWorkspaceEntity>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
        return items.ToList();
    }

    public async Task<ProjectWorkspaceEntity?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        const string sql = """
                           select
                               id Id,
                               name Name,
                               description Description,
                               is_default IsDefault,
                               created_at CreatedAt,
                               updated_at UpdatedAt
                           from projects
                           where id = @Id
                           limit 1
                           """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<ProjectWorkspaceEntity>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<ProjectWorkspaceEntity?> GetByNameAsync(string name, CancellationToken cancellationToken)
    {
        const string sql = """
                           select
                               id Id,
                               name Name,
                               description Description,
                               is_default IsDefault,
                               created_at CreatedAt,
                               updated_at UpdatedAt
                           from projects
                           where name = @Name
                           limit 1
                           """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<ProjectWorkspaceEntity>(
            new CommandDefinition(sql, new { Name = name }, cancellationToken: cancellationToken));
    }

    public async Task UpsertAsync(ProjectWorkspaceEntity entity, CancellationToken cancellationToken)
    {
        const string sql = """
                           insert into projects (
                               id, name, description, is_default, created_at, updated_at
                           ) values (
                               @Id, @Name, @Description, @IsDefault, @CreatedAt, @UpdatedAt
                           )
                           on conflict(id) do update set
                               name = excluded.name,
                               description = excluded.description,
                               is_default = excluded.is_default,
                               updated_at = excluded.updated_at
                           """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: cancellationToken));
    }

    public async Task SetDefaultAsync(string id, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition(
            "update projects set is_default = 0 where is_default = 1",
            transaction: transaction,
            cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(
            "update projects set is_default = 1, updated_at = @UpdatedAt where id = @Id",
            new { Id = id, UpdatedAt = DateTime.UtcNow },
            transaction,
            cancellationToken: cancellationToken));

        transaction.Commit();
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "delete from projects where id = @Id",
            new { Id = id },
            cancellationToken: cancellationToken));
    }
}
