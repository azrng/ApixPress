using Dapper;
using ApixPress.App.Data.Context;
using ApixPress.App.Models.Entities;
using ApixPress.App.Repositories.Interfaces;
using Azrng.Core.DependencyInjection;

namespace ApixPress.App.Repositories.Implementations;

public sealed class RequestHistoryRepository : IRequestHistoryRepository, ITransientDependency
{
    private readonly IDbConnectionFactory _connectionFactory;

    public RequestHistoryRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<RequestHistoryEntity>> GetHistoryAsync(string projectId, int limit, CancellationToken cancellationToken)
    {
        const string sql = """
                           select
                               id Id,
                               project_id ProjectId,
                               timestamp Timestamp,
                               request_snapshot_json RequestSnapshotJson,
                               response_snapshot_json ResponseSnapshotJson,
                               case
                                   when trim(coalesce(response_snapshot_json, '')) = '' then 0
                                   when json_valid(response_snapshot_json) = 0 then 1
                                   when json_type(response_snapshot_json, '$') = 'object' and trim(response_snapshot_json) = '{}' then 0
                                   when json_type(response_snapshot_json, '$') = 'object' then 1
                                   else 0
                               end HasResponse,
                               case
                                   when json_valid(response_snapshot_json) = 1 and json_type(response_snapshot_json, '$') = 'object'
                                       then cast(coalesce(
                                           json_extract(response_snapshot_json, '$.statusCode'),
                                           json_extract(response_snapshot_json, '$.StatusCode')) as integer)
                                   else null
                               end StatusCode,
                               case
                                   when json_valid(response_snapshot_json) = 1 and json_type(response_snapshot_json, '$') = 'object'
                                       then cast(coalesce(
                                           json_extract(response_snapshot_json, '$.durationMs'),
                                           json_extract(response_snapshot_json, '$.DurationMs'),
                                           0) as integer)
                                   else 0
                               end DurationMs,
                               case
                                   when json_valid(response_snapshot_json) = 1 and json_type(response_snapshot_json, '$') = 'object'
                                       then cast(coalesce(
                                           json_extract(response_snapshot_json, '$.sizeBytes'),
                                           json_extract(response_snapshot_json, '$.SizeBytes'),
                                           0) as integer)
                                   else 0
                               end SizeBytes
                           from request_history
                           where project_id = @ProjectId
                           order by timestamp desc
                           limit @Limit
                           """;

        using var connection = _connectionFactory.CreateConnection();
        var items = await connection.QueryAsync<RequestHistoryEntity>(
            new CommandDefinition(sql, new { ProjectId = projectId, Limit = limit }, cancellationToken: cancellationToken));
        return items.ToList();
    }

    public async Task<RequestHistoryEntity?> GetByIdAsync(string projectId, string id, CancellationToken cancellationToken)
    {
        const string sql = """
                           select
                               id Id,
                               project_id ProjectId,
                               timestamp Timestamp,
                               request_snapshot_json RequestSnapshotJson,
                               response_snapshot_json ResponseSnapshotJson
                           from request_history
                           where project_id = @ProjectId and id = @Id
                           limit 1
                           """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<RequestHistoryEntity>(
            new CommandDefinition(sql, new { ProjectId = projectId, Id = id }, cancellationToken: cancellationToken));
    }

    public async Task UpsertAsync(RequestHistoryEntity entity, CancellationToken cancellationToken)
    {
        const string sql = """
                           insert into request_history (
                               id, project_id, timestamp, request_snapshot_json, response_snapshot_json
                           ) values (
                               @Id, @ProjectId, @Timestamp, @RequestSnapshotJson, @ResponseSnapshotJson
                           )
                           """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(string projectId, string id, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "delete from request_history where project_id = @ProjectId and id = @Id",
            new { ProjectId = projectId, Id = id },
            cancellationToken: cancellationToken));
    }

    public async Task ClearAsync(string projectId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "delete from request_history where project_id = @ProjectId",
            new { ProjectId = projectId },
            cancellationToken: cancellationToken));
    }
}
