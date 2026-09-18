using Dapper;
using Azrng.Core.DependencyInjection;
using ApixPress.App.Data.Context;
using ApixPress.App.Models.Entities;
using ApixPress.App.Repositories.Interfaces;
using System.Data;

namespace ApixPress.App.Repositories.Implementations;

public sealed class ApiDocumentRepository : IApiDocumentRepository, ITransientDependency
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ApiDocumentRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<ApiDocumentEntity>> GetDocumentsAsync(string projectId, CancellationToken cancellationToken)
    {
        const string sql = """
                           -- 列表场景不取 raw_json（导入原文可达 MB 级，DTO 不使用），详情走 GetByIdAsync
                           select
                               id Id,
                               project_id ProjectId,
                               name Name,
                               source_type SourceType,
                               source_value SourceValue,
                               base_url BaseUrl,
                               imported_at ImportedAt
                           from api_documents
                           where project_id = @ProjectId
                           order by imported_at desc
                           """;

        using var connection = _connectionFactory.CreateConnection();
        var items = await Task.Run(async () => await connection.QueryAsync<ApiDocumentEntity>(
            new CommandDefinition(sql, new { ProjectId = projectId }, cancellationToken: cancellationToken)));
        return items.ToList();
    }

    public async Task<ApiDocumentEntity?> GetByIdAsync(string projectId, string documentId, CancellationToken cancellationToken)
    {
        const string sql = """
                           select
                               id Id,
                               project_id ProjectId,
                               name Name,
                               source_type SourceType,
                               source_value SourceValue,
                               base_url BaseUrl,
                               raw_json RawJson,
                               imported_at ImportedAt
                           from api_documents
                           where project_id = @ProjectId and id = @DocumentId
                           limit 1
                           """;

        using var connection = _connectionFactory.CreateConnection();
        return await Task.Run(async () => await connection.QuerySingleOrDefaultAsync<ApiDocumentEntity>(
            new CommandDefinition(sql, new { ProjectId = projectId, DocumentId = documentId }, cancellationToken: cancellationToken)));
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
                               request_body_mode RequestBodyMode,
                               request_body_template RequestBodyTemplate
                           from api_endpoints
                           where document_id = @DocumentId
                           order by group_name, method, path
                           """;

        using var connection = _connectionFactory.CreateConnection();
        var items = await Task.Run(async () => await connection.QueryAsync<ApiEndpointEntity>(
            new CommandDefinition(sql, new { DocumentId = documentId }, cancellationToken: cancellationToken)));
        return items.ToList();
    }

    public async Task<IReadOnlyList<ApiProjectEndpointEntity>> GetEndpointsByProjectIdAsync(string projectId, CancellationToken cancellationToken)
    {
        const string sql = """
                           select
                               ep.id Id,
                               ep.document_id DocumentId,
                               ad.name DocumentName,
                               ep.group_name GroupName,
                               ep.name Name,
                               ep.method Method,
                               ep.path Path,
                               ep.description Description
                           from api_endpoints ep
                           inner join api_documents ad on ad.id = ep.document_id
                           where ad.project_id = @ProjectId
                           order by ad.imported_at desc, ep.group_name, ep.name
                           """;

        using var connection = _connectionFactory.CreateConnection();
        var items = await Task.Run(async () => await connection.QueryAsync<ApiProjectEndpointEntity>(
            new CommandDefinition(sql, new { ProjectId = projectId }, cancellationToken: cancellationToken)));
        return items.ToList();
    }

    public async Task<IReadOnlyList<ApiEndpointEntity>> GetEndpointDetailsByProjectIdAsync(string projectId, CancellationToken cancellationToken)
    {
        const string sql = """
                           select
                               ep.id Id,
                               ep.document_id DocumentId,
                               ep.group_name GroupName,
                               ep.name Name,
                               ep.method Method,
                               ep.path Path,
                               ep.description Description,
                               ep.request_body_mode RequestBodyMode,
                               ep.request_body_template RequestBodyTemplate
                           from api_endpoints ep
                           inner join api_documents ad on ad.id = ep.document_id
                           where ad.project_id = @ProjectId
                           order by ad.imported_at desc, ep.group_name, ep.method, ep.path
                           """;

        using var connection = _connectionFactory.CreateConnection();
        var items = await Task.Run(async () => await connection.QueryAsync<ApiEndpointEntity>(
            new CommandDefinition(sql, new { ProjectId = projectId }, cancellationToken: cancellationToken)));
        return items.ToList();
    }

    public async Task<IReadOnlyList<RequestParameterEntity>> GetParametersByEndpointIdsAsync(IEnumerable<string> endpointIds, CancellationToken cancellationToken)
    {
        var ids = endpointIds.ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        using var connection = _connectionFactory.CreateConnection();
        const int batchSize = 250;
        var items = await Task.Run(async () =>
        {
            var result = new List<RequestParameterEntity>();
            foreach (var batch in ids.Chunk(batchSize))
            {
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
                var batchItems = await connection.QueryAsync<RequestParameterEntity>(
                    new CommandDefinition(sql, new { EndpointIds = batch }, cancellationToken: cancellationToken));
                result.AddRange(batchItems);
            }

            return result;
        });
        return items
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task DeleteEndpointsByIdsAsync(IEnumerable<string> endpointIds, CancellationToken cancellationToken)
    {
        var ids = endpointIds
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (ids.Length == 0)
        {
            return;
        }

        const string deleteParametersSql = """
                                           delete from request_parameters
                                           where endpoint_id in @EndpointIds
                                           """;

        const string deleteEndpointsSql = """
                                         delete from api_endpoints
                                         where id in @EndpointIds
                                         """;

        // 连接打开、事务与提交整体放入线程池：Commit（WAL 下含 fsync）不再阻塞 UI 线程
        await Task.Run(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            foreach (var batch in ids.Chunk(250))
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    deleteParametersSql,
                    new { EndpointIds = batch },
                    transaction,
                    cancellationToken: cancellationToken));
                await connection.ExecuteAsync(new CommandDefinition(
                    deleteEndpointsSql,
                    new { EndpointIds = batch },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            transaction.Commit();
        });
    }

    public async Task SaveDocumentGraphAsync(
        ApiDocumentEntity document,
        IReadOnlyList<ApiEndpointEntity> endpoints,
        IReadOnlyList<RequestParameterEntity> parameters,
        CancellationToken cancellationToken)
    {
        const string deleteEndpointParametersSql = """
                                                   delete from request_parameters
                                                   where endpoint_id in @EndpointIds
                                                   """;

        const string deleteEndpointsSql = """
                                          delete from api_endpoints
                                          where id in @EndpointIds
                                          """;

        const string deleteEmptyDocumentsSql = """
                                               delete from api_documents
                                               where project_id = @ProjectId
                                                 and id not in (
                                                     select distinct document_id
                                                     from api_endpoints
                                                 )
                                               """;

        const string insertDocumentSql = """
                                         insert into api_documents (
                                             id, project_id, name, source_type, source_value, base_url, raw_json, imported_at
                                         ) values (
                                             @Id, @ProjectId, @Name, @SourceType, @SourceValue, @BaseUrl, @RawJson, @ImportedAt
                                         )
                                         """;

        const string insertEndpointSql = """
                                         insert into api_endpoints (
                                             id, document_id, group_name, name, method, path, description, request_body_mode, request_body_template
                                         ) values (
                                             @Id, @DocumentId, @GroupName, @Name, @Method, @Path, @Description, @RequestBodyMode, @RequestBodyTemplate
                                         )
                                         """;

        const string insertParameterSql = """
                                          insert into request_parameters (
                                              id, endpoint_id, parameter_type, name, default_value, description, required
                                          ) values (
                                              @Id, @EndpointId, @ParameterType, @Name, @DefaultValue, @Description, @Required
                                          )
                                          """;

        // 连接打开、冲突清理、写入与提交整体放入线程池：大批量导入时 Commit 不阻塞 UI 线程
        await Task.Run(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            var conflictingEndpointIds = await LoadConflictingEndpointIdsAsync(connection, transaction, document.ProjectId, endpoints, cancellationToken);
            if (conflictingEndpointIds.Count > 0)
            {
                foreach (var batch in conflictingEndpointIds.Chunk(250))
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        deleteEndpointParametersSql,
                        new { EndpointIds = batch },
                        transaction,
                        cancellationToken: cancellationToken));
                    await connection.ExecuteAsync(new CommandDefinition(
                        deleteEndpointsSql,
                        new { EndpointIds = batch },
                        transaction,
                        cancellationToken: cancellationToken));
                }
                await connection.ExecuteAsync(new CommandDefinition(
                    deleteEmptyDocumentsSql,
                    new { document.ProjectId },
                    transaction,
                    cancellationToken: cancellationToken));
            }
            await connection.ExecuteAsync(new CommandDefinition(insertDocumentSql, document, transaction, cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new CommandDefinition(insertEndpointSql, endpoints, transaction, cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new CommandDefinition(insertParameterSql, parameters, transaction, cancellationToken: cancellationToken));

            transaction.Commit();
        });
    }

    private static async Task<List<string>> LoadConflictingEndpointIdsAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        string projectId,
        IReadOnlyList<ApiEndpointEntity> endpoints,
        CancellationToken cancellationToken)
    {
        if (endpoints.Count == 0)
        {
            return [];
        }

        var endpointKeys = endpoints
            .Select(endpoint => (Method: endpoint.Method.ToUpperInvariant(), endpoint.Path))
            .Distinct()
            .ToList();
        var conflictingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        const int keyBatchSize = 250;

        // SQLite 的参数和 SQL 文本都有实际上限；分批 VALUES JOIN 保持单次查询稳定，
        // 同时避免为大文档生成数千个 OR 分支和重复查询计划。
        for (var offset = 0; offset < endpointKeys.Count; offset += keyBatchSize)
        {
            var batch = endpointKeys.Skip(offset).Take(keyBatchSize).ToList();
            var values = string.Join(", ", batch.Select((_, index) => $"(@Method{index}, @Path{index})"));
            var parameters = new DynamicParameters();
            parameters.Add("ProjectId", projectId);
            for (var index = 0; index < batch.Count; index++)
            {
                parameters.Add($"Method{index}", batch[index].Method);
                parameters.Add($"Path{index}", batch[index].Path);
            }

            var sql = $"""
                       with incoming(method, path) as (values {values})
                       select ep.id
                       from api_endpoints ep
                       inner join api_documents ad on ad.id = ep.document_id
                       inner join incoming on incoming.method = ep.method and incoming.path = ep.path
                       where ad.project_id = @ProjectId
                       """;

            var ids = await connection.QueryAsync<string>(new CommandDefinition(
                sql,
                parameters,
                transaction,
                cancellationToken: cancellationToken));
            foreach (var id in ids)
            {
                conflictingIds.Add(id);
            }
        }

        return conflictingIds.ToList();
    }
}
