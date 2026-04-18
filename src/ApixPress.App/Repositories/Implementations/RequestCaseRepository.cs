using Dapper;
using ApixPress.App.Data.Context;
using ApixPress.App.Models.Entities;
using ApixPress.App.Repositories.Interfaces;
using Azrng.Core.DependencyInjection;

namespace ApixPress.App.Repositories.Implementations;

public sealed class RequestCaseRepository : IRequestCaseRepository, ITransientDependency
{
    private readonly IDbConnectionFactory _connectionFactory;

    public RequestCaseRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<RequestCaseEntity>> GetCasesAsync(string projectId, CancellationToken cancellationToken)
    {
        const string sql = """
                           select
                               id Id,
                               project_id ProjectId,
                               entry_type EntryType,
                               name Name,
                               group_name GroupName,
                               folder_path FolderPath,
                               parent_id ParentId,
                               tags_json TagsJson,
                               description Description,
                               request_snapshot_json RequestSnapshotJson,
                               updated_at UpdatedAt
                           from request_cases
                           where project_id = @ProjectId
                           order by entry_type, folder_path, updated_at desc
                           """;

        using var connection = _connectionFactory.CreateConnection();
        var items = await connection.QueryAsync<RequestCaseEntity>(
            new CommandDefinition(sql, new { ProjectId = projectId }, cancellationToken: cancellationToken));
        return items.ToList();
    }

    public async Task<RequestCaseEntity?> GetByIdAsync(string projectId, string id, CancellationToken cancellationToken)
    {
        const string sql = """
                           select
                               id Id,
                               project_id ProjectId,
                               entry_type EntryType,
                               name Name,
                               group_name GroupName,
                               folder_path FolderPath,
                               parent_id ParentId,
                               tags_json TagsJson,
                               description Description,
                               request_snapshot_json RequestSnapshotJson,
                               updated_at UpdatedAt
                           from request_cases
                           where project_id = @ProjectId and id = @Id
                           limit 1
                           """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<RequestCaseEntity>(
            new CommandDefinition(sql, new { ProjectId = projectId, Id = id }, cancellationToken: cancellationToken));
    }

    public async Task UpsertAsync(RequestCaseEntity entity, CancellationToken cancellationToken)
    {
        const string sql = """
                           insert into request_cases (
                               id, project_id, entry_type, name, group_name, folder_path, parent_id, tags_json, description, request_snapshot_json, updated_at
                           ) values (
                               @Id, @ProjectId, @EntryType, @Name, @GroupName, @FolderPath, @ParentId, @TagsJson, @Description, @RequestSnapshotJson, @UpdatedAt
                           )
                           on conflict(id) do update set
                               project_id = excluded.project_id,
                               entry_type = excluded.entry_type,
                               name = excluded.name,
                               group_name = excluded.group_name,
                               folder_path = excluded.folder_path,
                               parent_id = excluded.parent_id,
                               tags_json = excluded.tags_json,
                               description = excluded.description,
                               request_snapshot_json = excluded.request_snapshot_json,
                               updated_at = excluded.updated_at
                           """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: cancellationToken));
    }

    public async Task UpsertRangeAsync(IReadOnlyList<RequestCaseEntity> entities, CancellationToken cancellationToken)
    {
        if (entities.Count == 0)
        {
            return;
        }

        const string sql = """
                           insert into request_cases (
                               id, project_id, entry_type, name, group_name, folder_path, parent_id, tags_json, description, request_snapshot_json, updated_at
                           ) values (
                               @Id, @ProjectId, @EntryType, @Name, @GroupName, @FolderPath, @ParentId, @TagsJson, @Description, @RequestSnapshotJson, @UpdatedAt
                           )
                           on conflict(id) do update set
                               project_id = excluded.project_id,
                               entry_type = excluded.entry_type,
                               name = excluded.name,
                               group_name = excluded.group_name,
                               folder_path = excluded.folder_path,
                               parent_id = excluded.parent_id,
                               tags_json = excluded.tags_json,
                               description = excluded.description,
                               request_snapshot_json = excluded.request_snapshot_json,
                               updated_at = excluded.updated_at
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

    public async Task DeleteAsync(string projectId, string id, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "delete from request_cases where project_id = @ProjectId and id = @Id",
            new { ProjectId = projectId, Id = id },
            cancellationToken: cancellationToken));
    }

    public async Task DeleteRangeAsync(string projectId, IEnumerable<string> ids, CancellationToken cancellationToken)
    {
        var targetIds = ids
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (targetIds.Length == 0)
        {
            return;
        }

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "delete from request_cases where project_id = @ProjectId and id in @Ids",
            new { ProjectId = projectId, Ids = targetIds },
            cancellationToken: cancellationToken));
    }
}
