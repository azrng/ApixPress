using Dapper;
using ApixPress.App.Data.Context;
using ApixPress.App.Models.Entities;
using ApixPress.App.Repositories.Interfaces;
using Azrng.Core.DependencyInjection;

namespace ApixPress.App.Repositories.Implementations;

public sealed class EnvironmentVariableRepository : IEnvironmentVariableRepository, ITransientDependency
{
    private readonly IDbConnectionFactory _connectionFactory;

    public EnvironmentVariableRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<EnvironmentVariableEntity>> GetByEnvironmentAsync(string environmentId, CancellationToken cancellationToken)
    {
        const string sql = """
                           select
                               id Id,
                               environment_id EnvironmentId,
                               environment_name EnvironmentName,
                               key Key,
                               value Value,
                               is_enabled IsEnabled
                           from environment_variables
                           where environment_id = @EnvironmentId
                           order by key
                           """;

        using var connection = _connectionFactory.CreateConnection();
        var items = await connection.QueryAsync<EnvironmentVariableEntity>(
            new CommandDefinition(sql, new { EnvironmentId = environmentId }, cancellationToken: cancellationToken));
        return items.ToList();
    }

    public async Task UpsertAsync(EnvironmentVariableEntity entity, CancellationToken cancellationToken)
    {
        const string sql = """
                           insert into environment_variables (
                               id, environment_id, environment_name, key, value, is_enabled
                           ) values (
                               @Id, @EnvironmentId, @EnvironmentName, @Key, @Value, @IsEnabled
                           )
                           on conflict(environment_id, key) do update set
                               id = excluded.id,
                               environment_name = excluded.environment_name,
                               value = excluded.value,
                               is_enabled = excluded.is_enabled
                           """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: cancellationToken));
    }

    public async Task UpsertRangeAsync(IReadOnlyList<EnvironmentVariableEntity> entities, CancellationToken cancellationToken)
    {
        if (entities.Count == 0)
        {
            return;
        }

        const string sql = """
                           insert into environment_variables (
                               id, environment_id, environment_name, key, value, is_enabled
                           ) values (
                               @Id, @EnvironmentId, @EnvironmentName, @Key, @Value, @IsEnabled
                           )
                           on conflict(environment_id, key) do update set
                               id = excluded.id,
                               environment_name = excluded.environment_name,
                               value = excluded.value,
                               is_enabled = excluded.is_enabled
                           """;

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            entities,
            transaction,
            cancellationToken: cancellationToken));
        transaction.Commit();
    }

    public async Task<IReadOnlyDictionary<string, string>> GetEnabledDictionaryAsync(string environmentId, CancellationToken cancellationToken)
    {
        const string sql = """
                           select
                               key Key,
                               value Value
                           from environment_variables
                           where environment_id = @EnvironmentId
                             and is_enabled = 1
                           order by key
                           """;

        using var connection = _connectionFactory.CreateConnection();
        var items = await connection.QueryAsync<EnvironmentVariableKeyValueRow>(
            new CommandDefinition(sql, new { EnvironmentId = environmentId }, cancellationToken: cancellationToken));
        return items
            .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.OrdinalIgnoreCase);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "delete from environment_variables where id = @Id",
            new { Id = id },
            cancellationToken: cancellationToken));
    }

    private sealed class EnvironmentVariableKeyValueRow
    {
        public string Key { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;
    }
}
