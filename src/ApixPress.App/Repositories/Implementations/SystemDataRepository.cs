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
