using Dapper;
using ApixPress.App.Helpers;
using Azrng.Core.DependencyInjection;
using System.Data;
using System.Reflection;

namespace ApixPress.App.Data.Context;

public sealed class DatabaseInitializer : ISingletonDependency
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DatabaseInitializer(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public void Initialize()
    {
        var migrationPath = WorkspacePaths.ResolveFromBaseDirectory(Path.Combine("Data", "Migrations", "001_Initial.sql"));
        var sql = File.Exists(migrationPath)
            ? File.ReadAllText(migrationPath)
            : EmbeddedResourceReader.ReadRequiredText(Assembly.GetExecutingAssembly(), "Data.Migrations.001_Initial.sql");

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        connection.Execute("PRAGMA foreign_keys = ON;");
        if (HasExistingWorkspace(connection))
        {
            UpgradeLegacyWorkspace(connection);
        }

        connection.Execute(sql);
        UpgradeLegacyWorkspace(connection);
    }

    private static void UpgradeLegacyWorkspace(IDbConnection connection)
    {
        EnsureProjectWorkspaceTables(connection);
        EnsureColumn(connection, "api_documents", "project_id", "TEXT");
        EnsureColumn(connection, "request_cases", "project_id", "TEXT");
        EnsureColumn(connection, "request_cases", "entry_type", "TEXT NOT NULL DEFAULT 'quick-request'");
        EnsureColumn(connection, "request_cases", "folder_path", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, "request_cases", "parent_id", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, "request_history", "project_id", "TEXT");
        EnsureColumn(connection, "environment_variables", "environment_id", "TEXT");
        EnsureColumn(connection, "environment_variables", "environment_name", "TEXT NOT NULL DEFAULT ''");

        connection.Execute("DROP INDEX IF EXISTS ux_request_cases_group_name;");
        connection.Execute("DROP INDEX IF EXISTS ux_request_cases_project_group_name;");
        connection.Execute("DROP INDEX IF EXISTS ux_environment_variables_name_key;");
        connection.Execute("CREATE UNIQUE INDEX IF NOT EXISTS ux_request_cases_project_entry_scope_name ON request_cases(project_id, entry_type, group_name, folder_path, parent_id, name);");
        connection.Execute("CREATE UNIQUE INDEX IF NOT EXISTS ux_environment_variables_environment_key ON environment_variables(environment_id, key);");
        connection.Execute("CREATE UNIQUE INDEX IF NOT EXISTS ux_projects_name ON projects(name);");
        connection.Execute("CREATE UNIQUE INDEX IF NOT EXISTS ux_projects_default ON projects(is_default) WHERE is_default = 1;");
        connection.Execute("CREATE UNIQUE INDEX IF NOT EXISTS ux_project_environments_project_name ON project_environments(project_id, name);");
        connection.Execute("CREATE UNIQUE INDEX IF NOT EXISTS ux_project_environments_project_active ON project_environments(project_id, is_active) WHERE is_active = 1;");
        connection.Execute("CREATE INDEX IF NOT EXISTS ix_api_documents_project_imported_at ON api_documents(project_id, imported_at DESC);");
        connection.Execute("CREATE INDEX IF NOT EXISTS ix_api_endpoints_document_id ON api_endpoints(document_id);");
        connection.Execute("CREATE INDEX IF NOT EXISTS ix_api_endpoints_method_path ON api_endpoints(method, path);");
        connection.Execute("CREATE INDEX IF NOT EXISTS ix_request_parameters_endpoint_id ON request_parameters(endpoint_id);");
        connection.Execute("CREATE INDEX IF NOT EXISTS ix_request_cases_project_parent_id ON request_cases(project_id, parent_id);");

        connection.Execute("update request_cases set entry_type = 'quick-request' where ifnull(entry_type, '') = ''");
        connection.Execute("update request_cases set folder_path = '' where folder_path is null");
        connection.Execute("update request_cases set parent_id = '' where parent_id is null");

        var hasLegacyData = CountRows(connection, "api_documents") > 0
                            || CountRows(connection, "request_cases") > 0
                            || CountRows(connection, "request_history") > 0
                            || CountRows(connection, "environment_variables") > 0;
        var projectCount = CountRows(connection, "projects");

        if (projectCount == 0 && hasLegacyData)
        {
            var projectId = Guid.NewGuid().ToString("N");
            var environmentId = Guid.NewGuid().ToString("N");
            var baseUrl = ResolveLegacyBaseUrl(connection);
            var now = DateTime.UtcNow;

            using var transaction = connection.BeginTransaction();
            connection.Execute(
                """
                insert into projects (id, name, description, is_default, created_at, updated_at)
                values (@Id, @Name, @Description, 1, @CreatedAt, @UpdatedAt)
                """,
                new
                {
                    Id = projectId,
                    Name = "默认项目",
                    Description = "从旧版单项目工作区自动迁移",
                    CreatedAt = now,
                    UpdatedAt = now
                },
                transaction);
            connection.Execute(
                """
                insert into project_environments (id, project_id, name, base_url, is_active, sort_order, created_at, updated_at)
                values (@Id, @ProjectId, @Name, @BaseUrl, 1, 1, @CreatedAt, @UpdatedAt)
                """,
                new
                {
                    Id = environmentId,
                    ProjectId = projectId,
                    Name = "默认环境",
                    BaseUrl = baseUrl,
                    CreatedAt = now,
                    UpdatedAt = now
                },
                transaction);
            connection.Execute("update api_documents set project_id = @ProjectId where ifnull(project_id, '') = ''", new { ProjectId = projectId }, transaction);
            connection.Execute("update request_cases set project_id = @ProjectId where ifnull(project_id, '') = ''", new { ProjectId = projectId }, transaction);
            connection.Execute("update request_history set project_id = @ProjectId where ifnull(project_id, '') = ''", new { ProjectId = projectId }, transaction);
            connection.Execute(
                "update environment_variables set environment_id = @EnvironmentId, environment_name = @EnvironmentName where ifnull(environment_id, '') = ''",
                new { EnvironmentId = environmentId, EnvironmentName = "默认环境" },
                transaction);
            connection.Execute("delete from environment_variables where lower(key) = 'baseurl'", transaction: transaction);
            transaction.Commit();
        }

        if (CountRows(connection, "projects") > 0 && connection.ExecuteScalar<long>("select count(1) from projects where is_default = 1") == 0)
        {
            var firstProjectId = connection.ExecuteScalar<string>("select id from projects order by updated_at desc, name limit 1");
            if (!string.IsNullOrWhiteSpace(firstProjectId))
            {
                connection.Execute("update projects set is_default = case when id = @Id then 1 else 0 end", new { Id = firstProjectId });
            }
        }

        var defaultProjectId = connection.ExecuteScalar<string?>(
            "select id from projects where is_default = 1 order by updated_at desc limit 1")
            ?? connection.ExecuteScalar<string?>(
                "select id from projects order by updated_at desc, name limit 1");
        if (string.IsNullOrWhiteSpace(defaultProjectId))
        {
            return;
        }

        connection.Execute("update api_documents set project_id = @ProjectId where ifnull(project_id, '') = ''", new { ProjectId = defaultProjectId });
        connection.Execute("update request_cases set project_id = @ProjectId where ifnull(project_id, '') = ''", new { ProjectId = defaultProjectId });
        connection.Execute("update request_history set project_id = @ProjectId where ifnull(project_id, '') = ''", new { ProjectId = defaultProjectId });

        var activeEnvironmentId = connection.ExecuteScalar<string?>(
            "select id from project_environments where project_id = @ProjectId and is_active = 1 order by sort_order, name limit 1",
            new { ProjectId = defaultProjectId });

        if (string.IsNullOrWhiteSpace(activeEnvironmentId) && CountRows(connection, "projects") > 0)
        {
            activeEnvironmentId = Guid.NewGuid().ToString("N");
            connection.Execute(
                """
                insert into project_environments (id, project_id, name, base_url, is_active, sort_order, created_at, updated_at)
                values (@Id, @ProjectId, @Name, @BaseUrl, 1, 1, @CreatedAt, @UpdatedAt)
                """,
                new
                {
                    Id = activeEnvironmentId,
                    ProjectId = defaultProjectId,
                    Name = "默认环境",
                    BaseUrl = ResolveLegacyBaseUrl(connection),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
        }

        if (!string.IsNullOrWhiteSpace(activeEnvironmentId))
        {
            connection.Execute(
                "update environment_variables set environment_id = @EnvironmentId, environment_name = @EnvironmentName where ifnull(environment_id, '') = ''",
                new { EnvironmentId = activeEnvironmentId, EnvironmentName = "默认环境" });

            var legacyBaseUrl = ResolveLegacyBaseUrl(connection);
            if (!string.IsNullOrWhiteSpace(legacyBaseUrl))
            {
                connection.Execute(
                    "update project_environments set base_url = @BaseUrl where id = @EnvironmentId and ifnull(base_url, '') = ''",
                    new { BaseUrl = legacyBaseUrl, EnvironmentId = activeEnvironmentId });
            }

            connection.Execute("delete from environment_variables where lower(key) = 'baseurl'");
        }
    }

    private static void EnsureColumn(IDbConnection connection, string tableName, string columnName, string columnDefinition)
    {
        if (!TableExists(connection, tableName))
        {
            return;
        }

        var columns = connection.Query<TableColumnInfo>($"pragma table_info('{tableName}')").ToList();
        if (columns.Any(item => string.Equals(item.Name, columnName, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        connection.Execute($"alter table {tableName} add column {columnName} {columnDefinition}");
    }

    private static bool HasExistingWorkspace(IDbConnection connection)
    {
        return TableExists(connection, "api_documents")
               || TableExists(connection, "request_cases")
               || TableExists(connection, "request_history")
               || TableExists(connection, "environment_variables");
    }

    private static bool TableExists(IDbConnection connection, string tableName)
    {
        return connection.ExecuteScalar<long>(
            "select count(1) from sqlite_master where type = 'table' and name = @Name",
            new { Name = tableName }) > 0;
    }

    private static void EnsureProjectWorkspaceTables(IDbConnection connection)
    {
        connection.Execute(
            """
            create table if not exists projects (
                id text primary key,
                name text not null,
                description text not null default '',
                is_default integer not null default 0,
                created_at text not null,
                updated_at text not null
            )
            """);

        connection.Execute(
            """
            create table if not exists project_environments (
                id text primary key,
                project_id text not null,
                name text not null,
                base_url text not null default '',
                is_active integer not null default 0,
                sort_order integer not null default 0,
                created_at text not null,
                updated_at text not null
            )
            """);
    }

    private static long CountRows(IDbConnection connection, string tableName)
    {
        return connection.ExecuteScalar<long>($"select count(1) from {tableName}");
    }

    private static string ResolveLegacyBaseUrl(IDbConnection connection)
    {
        var environmentBaseUrl = connection.ExecuteScalar<string?>(
            "select value from environment_variables where lower(key) = 'baseurl' order by rowid desc limit 1");
        if (!string.IsNullOrWhiteSpace(environmentBaseUrl))
        {
            return environmentBaseUrl;
        }

        return connection.ExecuteScalar<string?>(
                   "select base_url from api_documents where ifnull(base_url, '') <> '' order by imported_at desc limit 1")
               ?? string.Empty;
    }

    private sealed class TableColumnInfo
    {
        public string Name { get; set; } = string.Empty;
    }
}
