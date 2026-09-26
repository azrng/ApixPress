using Dapper;
using ApixPress.App.Data.Context;
using ApixPress.App.Models.Entities;
using ApixPress.App.Repositories.Interfaces;
using Azrng.Core.DependencyInjection;

namespace ApixPress.App.Repositories.Implementations;

public sealed class RequestCaseDeliveryRepository : IRequestCaseDeliveryRepository, ITransientDependency
{
    private readonly IDbConnectionFactory _connectionFactory;

    public RequestCaseDeliveryRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task ImportAsync(RequestCaseDeliveryImportPlan plan, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(plan.ProjectId))
        {
            throw new ArgumentException("交付包导入缺少目标项目。", nameof(plan));
        }

        await Task.Run(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (plan.EnvironmentIdsToClearVariables.Count > 0)
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        "delete from environment_variables where environment_id in @EnvironmentIds",
                        new { EnvironmentIds = plan.EnvironmentIdsToClearVariables },
                        transaction,
                        cancellationToken: cancellationToken));
                }

                if (plan.EndpointIdsToDelete.Count > 0)
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        "delete from request_parameters where endpoint_id in @EndpointIds",
                        new { EndpointIds = plan.EndpointIdsToDelete },
                        transaction,
                        cancellationToken: cancellationToken));
                    await connection.ExecuteAsync(new CommandDefinition(
                        "delete from api_endpoints where id in @EndpointIds",
                        new { EndpointIds = plan.EndpointIdsToDelete },
                        transaction,
                        cancellationToken: cancellationToken));
                }

                if (plan.DocumentIdsToDelete.Count > 0)
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        "delete from api_documents where id in @DocumentIds and not exists (select 1 from api_endpoints where document_id = api_documents.id)",
                        new { DocumentIds = plan.DocumentIdsToDelete },
                        transaction,
                        cancellationToken: cancellationToken));
                }

                foreach (var environment in plan.Environments)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (environment.IsActive)
                    {
                        await connection.ExecuteAsync(new CommandDefinition(
                            "update project_environments set is_active = 0 where project_id = @ProjectId and id <> @EnvironmentId",
                            new { ProjectId = plan.ProjectId, EnvironmentId = environment.Id },
                            transaction,
                            cancellationToken: cancellationToken));
                    }

                    await connection.ExecuteAsync(new CommandDefinition(
                        """
                        insert into project_environments (
                            id, project_id, name, base_url, is_active, sort_order, source_project_id, source_id, created_at, updated_at
                        ) values (
                            @Id, @ProjectId, @Name, @BaseUrl, @IsActive, @SortOrder, @SourceProjectId, @SourceId, @CreatedAt, @UpdatedAt
                        )
                        on conflict(id) do update set
                            name = excluded.name,
                            base_url = excluded.base_url,
                            is_active = excluded.is_active,
                            sort_order = excluded.sort_order,
                            source_project_id = excluded.source_project_id,
                            source_id = excluded.source_id,
                            updated_at = excluded.updated_at
                        """,
                        environment,
                        transaction,
                        cancellationToken: cancellationToken));
                }

                if (plan.Variables.Count > 0)
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        """
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
                        """,
                        plan.Variables,
                        transaction,
                        cancellationToken: cancellationToken));
                }

                if (plan.Document is not null)
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        """
                        insert into api_documents (
                            id, project_id, name, source_type, source_value, base_url, raw_json, imported_at
                        ) values (
                            @Id, @ProjectId, @Name, @SourceType, @SourceValue, @BaseUrl, @RawJson, @ImportedAt
                        )
                        """,
                        plan.Document,
                        transaction,
                        cancellationToken: cancellationToken));
                }

                if (plan.Endpoints.Count > 0)
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        """
                        insert into api_endpoints (
                            id, document_id, group_name, name, method, path, description, request_body_mode, request_body_template
                        ) values (
                            @Id, @DocumentId, @GroupName, @Name, @Method, @Path, @Description, @RequestBodyMode, @RequestBodyTemplate
                        )
                        """,
                        plan.Endpoints,
                        transaction,
                        cancellationToken: cancellationToken));
                }

                if (plan.Parameters.Count > 0)
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        """
                        insert into request_parameters (
                            id, endpoint_id, parameter_type, name, default_value, description, required
                        ) values (
                            @Id, @EndpointId, @ParameterType, @Name, @DefaultValue, @Description, @Required
                        )
                        """,
                        plan.Parameters,
                        transaction,
                        cancellationToken: cancellationToken));
                }

                if (plan.RequestCases.Count > 0)
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        """
                        insert into request_cases (
                            id, project_id, entry_type, name, group_name, folder_path, parent_id, tags_json,
                            description, request_snapshot_json, source_project_id, source_id, updated_at
                        ) values (
                            @Id, @ProjectId, @EntryType, @Name, @GroupName, @FolderPath, @ParentId, @TagsJson,
                            @Description, @RequestSnapshotJson, @SourceProjectId, @SourceId, @UpdatedAt
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
                            source_project_id = excluded.source_project_id,
                            source_id = excluded.source_id,
                            updated_at = excluded.updated_at
                        """,
                        plan.RequestCases,
                        transaction,
                        cancellationToken: cancellationToken));
                }

                cancellationToken.ThrowIfCancellationRequested();
                transaction.Commit();
            }
            catch
            {
                try
                {
                    transaction.Rollback();
                }
                catch
                {
                    // 原始异常更能定位导入失败原因，回滚失败不覆盖它。
                }

                throw;
            }
        }, cancellationToken);
    }
}
