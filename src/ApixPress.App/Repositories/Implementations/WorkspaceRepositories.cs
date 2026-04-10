using Dapper;
using Azrng.Core.DependencyInjection;
using ApixPress.App.Data.Context;
using ApixPress.App.Models.Entities;
using ApixPress.App.Repositories.Interfaces;

namespace ApixPress.App.Repositories.Implementations;

public sealed class ApiDocumentRepository : IApiDocumentRepository, ITransientDependency
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ApiDocumentRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<ApiDocumentEntity>> GetDocumentsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
                           select
                               id Id,
                               name Name,
                               source_type SourceType,
                               source_value SourceValue,
                               base_url BaseUrl,
                               raw_json RawJson,
                               imported_at ImportedAt
                           from api_documents
                           order by imported_at desc
                           """;

        using var connection = _connectionFactory.CreateConnection();
        var items = await connection.QueryAsync<ApiDocumentEntity>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
        return items.ToList();
    }

    public async Task<IReadOnlyList<ApiEndpointEntity>> GetEndpointsByDocumentIdAsync(string documentId, CancellationToken cancellationToken)
    {
        const string sql = """
                           select
                               id Id,
                               document_id DocumentId,
                               group_name GroupName,
                               name Name,
                               method Method,
                               path Path,
                               description Description,
                               request_body_template RequestBodyTemplate
                           from api_endpoints
                           where document_id = @DocumentId
                           order by group_name, method, path
                           """;

        using var connection = _connectionFactory.CreateConnection();
        var items = await connection.QueryAsync<ApiEndpointEntity>(
            new CommandDefinition(sql, new { DocumentId = documentId }, cancellationToken: cancellationToken));
        return items.ToList();
    }

    public async Task<IReadOnlyList<RequestParameterEntity>> GetParametersByEndpointIdsAsync(IEnumerable<string> endpointIds, CancellationToken cancellationToken)
    {
        var ids = endpointIds.ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        const string sql = """
                           select
                               id Id,
                               endpoint_id EndpointId,
                               parameter_type ParameterType,
                               name Name,
                               default_value DefaultValue,
                               description Description,
                               required Required
                           from request_parameters
                           where endpoint_id in @EndpointIds
                           order by name
                           """;

        using var connection = _connectionFactory.CreateConnection();
        var items = await connection.QueryAsync<RequestParameterEntity>(
            new CommandDefinition(sql, new { EndpointIds = ids }, cancellationToken: cancellationToken));
        return items.ToList();
    }

    public async Task SaveDocumentGraphAsync(
        ApiDocumentEntity document,
        IReadOnlyList<ApiEndpointEntity> endpoints,
        IReadOnlyList<RequestParameterEntity> parameters,
        CancellationToken cancellationToken)
    {
        const string insertDocumentSql = """
                                         insert into api_documents (
                                             id, name, source_type, source_value, base_url, raw_json, imported_at
                                         ) values (
                                             @Id, @Name, @SourceType, @SourceValue, @BaseUrl, @RawJson, @ImportedAt
                                         )
                                         """;

        const string insertEndpointSql = """
                                         insert into api_endpoints (
                                             id, document_id, group_name, name, method, path, description, request_body_template
                                         ) values (
                                             @Id, @DocumentId, @GroupName, @Name, @Method, @Path, @Description, @RequestBodyTemplate
                                         )
                                         """;

        const string insertParameterSql = """
                                          insert into request_parameters (
                                              id, endpoint_id, parameter_type, name, default_value, description, required
                                          ) values (
                                              @Id, @EndpointId, @ParameterType, @Name, @DefaultValue, @Description, @Required
                                          )
                                          """;

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition(insertDocumentSql, document, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(insertEndpointSql, endpoints, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(insertParameterSql, parameters, transaction, cancellationToken: cancellationToken));

        transaction.Commit();
    }
}

public sealed class RequestCaseRepository : IRequestCaseRepository, ITransientDependency
{
    private readonly IDbConnectionFactory _connectionFactory;

    public RequestCaseRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<RequestCaseEntity>> GetCasesAsync(CancellationToken cancellationToken)
    {
        const string sql = """
                           select
                               id Id,
                               name Name,
                               group_name GroupName,
                               tags_json TagsJson,
                               description Description,
                               request_snapshot_json RequestSnapshotJson,
                               updated_at UpdatedAt
                           from request_cases
                           order by updated_at desc
                           """;

        using var connection = _connectionFactory.CreateConnection();
        var items = await connection.QueryAsync<RequestCaseEntity>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
        return items.ToList();
    }

    public async Task<RequestCaseEntity?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        const string sql = """
                           select
                               id Id,
                               name Name,
                               group_name GroupName,
                               tags_json TagsJson,
                               description Description,
                               request_snapshot_json RequestSnapshotJson,
                               updated_at UpdatedAt
                           from request_cases
                           where id = @Id
                           limit 1
                           """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<RequestCaseEntity>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task UpsertAsync(RequestCaseEntity entity, CancellationToken cancellationToken)
    {
        const string sql = """
                           insert into request_cases (
                               id, name, group_name, tags_json, description, request_snapshot_json, updated_at
                           ) values (
                               @Id, @Name, @GroupName, @TagsJson, @Description, @RequestSnapshotJson, @UpdatedAt
                           )
                           on conflict(group_name, name) do update set
                               id = excluded.id,
                               tags_json = excluded.tags_json,
                               description = excluded.description,
                               request_snapshot_json = excluded.request_snapshot_json,
                               updated_at = excluded.updated_at
                           """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "delete from request_cases where id = @Id",
            new { Id = id },
            cancellationToken: cancellationToken));
    }
}

public sealed class EnvironmentVariableRepository : IEnvironmentVariableRepository, ITransientDependency
{
    private readonly IDbConnectionFactory _connectionFactory;

    public EnvironmentVariableRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<EnvironmentVariableEntity>> GetByEnvironmentAsync(string environmentName, CancellationToken cancellationToken)
    {
        const string sql = """
                           select
                               id Id,
                               environment_name EnvironmentName,
                               key Key,
                               value Value,
                               is_enabled IsEnabled
                           from environment_variables
                           where environment_name = @EnvironmentName
                           order by key
                           """;

        using var connection = _connectionFactory.CreateConnection();
        var items = await connection.QueryAsync<EnvironmentVariableEntity>(
            new CommandDefinition(sql, new { EnvironmentName = environmentName }, cancellationToken: cancellationToken));
        return items.ToList();
    }

    public async Task UpsertAsync(EnvironmentVariableEntity entity, CancellationToken cancellationToken)
    {
        const string sql = """
                           insert into environment_variables (
                               id, environment_name, key, value, is_enabled
                           ) values (
                               @Id, @EnvironmentName, @Key, @Value, @IsEnabled
                           )
                           on conflict(environment_name, key) do update set
                               id = excluded.id,
                               value = excluded.value,
                               is_enabled = excluded.is_enabled
                           """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "delete from environment_variables where id = @Id",
            new { Id = id },
            cancellationToken: cancellationToken));
    }
}

public sealed class RequestHistoryRepository : IRequestHistoryRepository, ITransientDependency
{
    private readonly IDbConnectionFactory _connectionFactory;

    public RequestHistoryRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<RequestHistoryEntity>> GetHistoryAsync(int limit, CancellationToken cancellationToken)
    {
        const string sql = """
                           select
                               id Id,
                               timestamp Timestamp,
                               request_snapshot_json RequestSnapshotJson,
                               response_snapshot_json ResponseSnapshotJson
                           from request_history
                           order by timestamp desc
                           limit @Limit
                           """;

        using var connection = _connectionFactory.CreateConnection();
        var items = await connection.QueryAsync<RequestHistoryEntity>(
            new CommandDefinition(sql, new { Limit = limit }, cancellationToken: cancellationToken));
        return items.ToList();
    }

    public async Task UpsertAsync(RequestHistoryEntity entity, CancellationToken cancellationToken)
    {
        const string sql = """
                           insert into request_history (
                               id, timestamp, request_snapshot_json, response_snapshot_json
                           ) values (
                               @Id, @Timestamp, @RequestSnapshotJson, @ResponseSnapshotJson
                           )
                           """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "delete from request_history where id = @Id",
            new { Id = id },
            cancellationToken: cancellationToken));
    }

    public async Task ClearAsync(CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "delete from request_history",
            cancellationToken: cancellationToken));
    }
}
