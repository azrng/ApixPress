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
        var items = await Task.Run(async () => await connection.QueryAsync<EnvironmentVariableEntity>(
            new CommandDefinition(sql, new { EnvironmentId = environmentId }, cancellationToken: cancellationToken)));
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
        await Task.Run(async () => await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: cancellationToken)));
    }

    public async Task UpsertRangeAsync(IReadOnlyList<EnvironmentVariableEntity> entities, CancellationToken cancellationToken)
    {
        if (entities.Count == 0)
        {
            return;
        }

        const string insertSql = """
                                 insert into environment_variables (
                                     id, environment_id, environment_name, key, value, is_enabled
                                 ) values (
                                     @Id, @EnvironmentId, @EnvironmentName, @Key, @Value, @IsEnabled
                                 )
                                 """;

        // 连接打开、事务与提交整体放入线程池：Commit 不阻塞 UI 线程。
        // 变量按整环境全量保存：先删后插。不能对 (environment_id, key) 做 on conflict 向上插入——
        // 变量改名时旧行的 id 与新插入行的 id 相同但 key 不同，冲突落在主键 id 上会直接抛
        // UNIQUE constraint failed: environment_variables.id（改名保存即崩溃）
        await Task.Run(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();
            await connection.ExecuteAsync(new CommandDefinition(
                "delete from environment_variables where environment_id = @EnvironmentId",
                new { EnvironmentId = entities[0].EnvironmentId },
                transaction,
                cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new CommandDefinition(
                insertSql,
                entities,
                transaction,
                cancellationToken: cancellationToken));
            transaction.Commit();
        });
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
        var items = await Task.Run(async () => await connection.QueryAsync<EnvironmentVariableKeyValueRow>(
            new CommandDefinition(sql, new { EnvironmentId = environmentId }, cancellationToken: cancellationToken)));
        return items
            .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.OrdinalIgnoreCase);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await Task.Run(async () => await connection.ExecuteAsync(new CommandDefinition(
            "delete from environment_variables where id = @Id",
            new { Id = id },
            cancellationToken: cancellationToken)));
    }

    private sealed class EnvironmentVariableKeyValueRow
    {
        public string Key { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;
    }
}
