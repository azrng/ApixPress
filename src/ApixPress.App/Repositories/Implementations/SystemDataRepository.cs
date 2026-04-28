using Dapper;
using ApixPress.App.Data.Context;
using ApixPress.App.Repositories.Interfaces;
using Azrng.Core.DependencyInjection;

namespace ApixPress.App.Repositories.Implementations;

public sealed class SystemDataRepository : ISystemDataRepository, ITransientDependency
{
    private readonly IDbConnectionFactory _connectionFactory;

    public SystemDataRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<bool> ClearProjectAsync(string projectId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var exists = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                "select count(1) from projects where id = @ProjectId",
                new { ProjectId = projectId },
                transaction,
                cancellationToken: cancellationToken));
            if (exists == 0)
            {
                transaction.Rollback();
                return false;
            }

            const string deleteSql = """
                                     delete from request_history
                                     where project_id = @ProjectId;

                                     delete from request_cases
                                     where project_id = @ProjectId;

                                     delete from request_parameters
                                     where endpoint_id in (
                                         select ep.id
                                         from api_endpoints ep
                                         inner join api_documents ad on ad.id = ep.document_id
                                         where ad.project_id = @ProjectId
                                     );

                                     delete from api_endpoints
                                     where document_id in (
                                         select id
                                         from api_documents
                                         where project_id = @ProjectId
                                     );

                                     delete from api_documents
                                     where project_id = @ProjectId;

                                     delete from environment_variables
                                     where environment_id in (
                                         select id
                                         from project_environments
                                         where project_id = @ProjectId
                                     );

                                     delete from project_environments
                                     where project_id = @ProjectId;
                                     """;

            await connection.ExecuteAsync(new CommandDefinition(
                deleteSql,
                new { ProjectId = projectId },
                transaction,
                cancellationToken: cancellationToken));

            var now = DateTime.UtcNow;
            await connection.ExecuteAsync(new CommandDefinition(
                """
                insert into project_environments (
                    id, project_id, name, base_url, is_active, sort_order, created_at, updated_at
                ) values (
                    @Id, @ProjectId, @Name, @BaseUrl, 1, 1, @CreatedAt, @UpdatedAt
                );

                update projects
                set updated_at = @UpdatedAt
                where id = @ProjectId;
                """,
                new
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ProjectId = projectId,
                    Name = "开发",
                    BaseUrl = string.Empty,
                    CreatedAt = now,
                    UpdatedAt = now
                },
                transaction,
                cancellationToken: cancellationToken));

            transaction.Commit();
            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task ClearAllAsync(CancellationToken cancellationToken)
    {
        const string sql = """
                           delete from request_history;
                           delete from request_cases;
                           delete from request_parameters;
                           delete from api_endpoints;
                           delete from api_documents;
                           delete from environment_variables;
                           delete from project_environments;
                           delete from projects;
                           """;

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                sql,
                transaction: transaction,
                cancellationToken: cancellationToken));
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
