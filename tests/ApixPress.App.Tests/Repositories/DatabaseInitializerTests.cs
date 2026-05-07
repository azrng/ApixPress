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

        Assert.Equal(1, schemaVersion);
        Assert.Equal(0, projectCount);
        Assert.Equal(0, environmentCount);
    }
}
