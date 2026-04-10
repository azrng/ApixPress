using Dapper;
using ApixPress.App.Helpers;
using Azrng.Core.DependencyInjection;

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
        if (!File.Exists(migrationPath))
        {
            throw new FileNotFoundException("未找到数据库初始化脚本。", migrationPath);
        }

        var sql = File.ReadAllText(migrationPath);

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        connection.Execute(sql);
    }
}
