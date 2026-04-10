using System.Data;
using ApixPress.App.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Azrng.Core.DependencyInjection;

namespace ApixPress.App.Data.Context;

public sealed class SqliteConnectionFactory : IDbConnectionFactory, ISingletonDependency
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(IConfiguration configuration)
    {
        var rawConnectionString = configuration.GetConnectionString("Default")
                                  ?? throw new InvalidOperationException("缺少默认数据库连接字符串配置。");

        const string prefix = "Data Source=";
        if (rawConnectionString.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            var relativePath = rawConnectionString[prefix.Length..].Trim();
            var fullPath = WorkspacePaths.ResolveFromBaseDirectory(relativePath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _connectionString = $"{prefix}{fullPath}";
        }
        else
        {
            _connectionString = rawConnectionString;
        }
    }

    public IDbConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }
}
