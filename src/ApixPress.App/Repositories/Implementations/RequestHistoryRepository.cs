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
                           -- 列表是摘要投影：不取 response_snapshot_json 原文（单条可达 MB 级），
                           -- 只用 JSON 函数算出 HasResponse/StatusCode/DurationMs/SizeBytes；详情走单条查询
                           select
                               id Id,
                               project_id ProjectId,
                               timestamp Timestamp,
                               coalesce(
                                   json_extract(request_snapshot_json, '$.name'),
                                   json_extract(request_snapshot_json, '$.Name'),
                                   ''
                               ) RequestName,
                               coalesce(
                                   json_extract(request_snapshot_json, '$.method'),
                                   json_extract(request_snapshot_json, '$.Method'),
                                   ''
                               ) RequestMethod,
                               coalesce(
                                   json_extract(request_snapshot_json, '$.url'),
                                   json_extract(request_snapshot_json, '$.Url'),
                                   ''
                               ) RequestUrl,
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
        var items = await Task.Run(async () => await connection.QueryAsync<HistorySummaryRow>(
            new CommandDefinition(sql, new { ProjectId = projectId, Limit = limit }, cancellationToken: cancellationToken)));
        return items.Select(row => new RequestHistoryEntity
        {
            Id = row.Id,
            ProjectId = row.ProjectId,
            Timestamp = row.Timestamp,
            RequestName = row.RequestName,
            RequestMethod = row.RequestMethod,
            RequestUrl = row.RequestUrl,
            HasResponse = row.HasResponse,
            StatusCode = row.StatusCode is { } code ? checked((int)code) : null,
            DurationMs = row.DurationMs,
            SizeBytes = row.SizeBytes
        }).ToList();
    }

    // SQLite INTEGER 实际返回 Int64。首行状态为空时 Dapper 无法推断后续值的 Int32 转换，
    // 投影先按数据库整数类型读取，再显式转为领域状态码，避免依赖行顺序与映射缓存。
    private sealed class HistorySummaryRow
    {
        public string Id { get; set; } = string.Empty;
        public string ProjectId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string RequestName { get; set; } = string.Empty;
        public string RequestMethod { get; set; } = string.Empty;
        public string RequestUrl { get; set; } = string.Empty;
        public bool HasResponse { get; set; }
        public long? StatusCode { get; set; }
        public long DurationMs { get; set; }
        public long SizeBytes { get; set; }
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
        return await Task.Run(async () => await connection.QueryFirstOrDefaultAsync<RequestHistoryEntity>(
            new CommandDefinition(sql, new { ProjectId = projectId, Id = id }, cancellationToken: cancellationToken)));
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
        await Task.Run(async () => await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: cancellationToken)));
    }

    public async Task DeleteAsync(string projectId, string id, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await Task.Run(async () => await connection.ExecuteAsync(new CommandDefinition(
            "delete from request_history where project_id = @ProjectId and id = @Id",
            new { ProjectId = projectId, Id = id },
            cancellationToken: cancellationToken)));
    }

    public async Task ClearAsync(string projectId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await Task.Run(async () => await connection.ExecuteAsync(new CommandDefinition(
            "delete from request_history where project_id = @ProjectId",
            new { ProjectId = projectId },
            cancellationToken: cancellationToken)));
    }

    public async Task ClearAllAsync(CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await Task.Run(async () => await connection.ExecuteAsync(new CommandDefinition(
            "delete from request_history",
            cancellationToken: cancellationToken)));
    }

    public async Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        // timestamp 以 UTC ISO-8601 TEXT 存储，按字典序比较与时间序一致
        return await Task.Run(async () => await connection.ExecuteAsync(new CommandDefinition(
            "delete from request_history where timestamp < @Cutoff",
            new { Cutoff = cutoff },
            cancellationToken: cancellationToken)));
    }
}
