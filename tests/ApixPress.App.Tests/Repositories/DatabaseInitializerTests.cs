using ApixPress.App.Data.Context;
using Dapper;

namespace ApixPress.App.Tests.Repositories;

public sealed class DatabaseInitializerTests
{
    [Fact]
    public void Initialize_ShouldApplyInitialMigrationWithoutCreatingProjectForEmptyDatabase()
    {
        using var factory = new TestSqliteConnectionFactory();
        var initializer = new DatabaseInitializer(factory);

        initializer.Initialize();

        using var connection = factory.CreateConnection();
        connection.Open();

        var schemaVersion = connection.ExecuteScalar<long>("select max(version) from schema_migrations");
        var projectCount = connection.ExecuteScalar<long>("select count(1) from projects");
        var environmentCount = connection.ExecuteScalar<long>("select count(1) from project_environments");
        var httpSettingsTableCount = connection.ExecuteScalar<long>(
            "select count(1) from sqlite_master where type = 'table' and name = 'project_http_settings'");

        Assert.Equal(2, schemaVersion);
        Assert.Equal(0, projectCount);
        Assert.Equal(0, environmentCount);
        Assert.Equal(1, httpSettingsTableCount);
    }

    [Fact]
    public void Initialize_ShouldUpgradeVersionOneDatabaseWithProjectHttpSettingsTable()
    {
        using var factory = new TestSqliteConnectionFactory();
        using (var connection = factory.CreateConnection())
        {
            connection.Open();
            connection.Execute(
                """
                create table schema_migrations (
                    version integer primary key,
                    applied_at text not null
                );

                insert into schema_migrations (version, applied_at)
                values (1, '2026-05-13T00:00:00Z');

                create table projects (
                    id text primary key,
                    name text not null,
                    description text not null default '',
                    is_default integer not null default 0,
                    created_at text not null,
                    updated_at text not null
                );

                create table project_environments (
                    id text primary key,
                    project_id text not null,
                    name text not null,
                    base_url text not null default '',
                    is_active integer not null default 0,
                    sort_order integer not null default 0,
                    created_at text not null,
                    updated_at text not null
                );

                create table api_documents (
                    id text primary key,
                    project_id text not null,
                    name text not null,
                    source_type text not null,
                    source_value text not null,
                    base_url text not null default '',
                    raw_json text not null,
                    imported_at text not null
                );

                create table api_endpoints (
                    id text primary key,
                    document_id text not null,
                    group_name text not null,
                    name text not null,
                    method text not null,
                    path text not null,
                    description text not null default '',
                    request_body_mode text not null default 'None',
                    request_body_template text not null default ''
                );

                create table request_parameters (
                    id text primary key,
                    endpoint_id text not null,
                    parameter_type text not null,
                    name text not null,
                    default_value text not null default '',
                    description text not null default '',
                    required integer not null default 0
                );

                create table request_cases (
                    id text primary key,
                    project_id text not null,
                    entry_type text not null default 'quick-request',
                    name text not null,
                    group_name text not null,
                    folder_path text not null default '',
                    parent_id text not null default '',
                    tags_json text not null default '[]',
                    description text not null default '',
                    request_snapshot_json text not null,
                    updated_at text not null
                );

                create table environment_variables (
                    id text primary key,
                    environment_id text not null,
                    environment_name text not null default '',
                    key text not null,
                    value text not null default '',
                    is_enabled integer not null default 1
                );

                create table request_history (
                    id text primary key,
                    project_id text not null,
                    timestamp text not null,
                    request_snapshot_json text not null,
                    response_snapshot_json text not null default '{}'
                );
                """);
        }

        var initializer = new DatabaseInitializer(factory);

        initializer.Initialize();

        using var verificationConnection = factory.CreateConnection();
        verificationConnection.Open();

        var schemaVersion = verificationConnection.ExecuteScalar<long>("select max(version) from schema_migrations");
        var httpSettingsTableCount = verificationConnection.ExecuteScalar<long>(
            "select count(1) from sqlite_master where type = 'table' and name = 'project_http_settings'");

        Assert.Equal(2, schemaVersion);
        Assert.Equal(1, httpSettingsTableCount);
    }
}
