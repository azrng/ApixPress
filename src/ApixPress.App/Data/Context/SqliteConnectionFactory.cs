using System.Data;
using ApixPress.App.Helpers;
using ApixPress.App.Services.Implementations;
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

        var shellSettings = AppShellSettingsService.LoadFromFileOrDefault(AppShellSettingsService.ResolveDefaultSettingsFilePath());
        if (!string.IsNullOrWhiteSpace(shellSettings.StorageDirectoryPath))
        {
            var databasePath = AppStoragePaths.ResolveDatabasePath(shellSettings.StorageDirectoryPath);
            EnsureDirectory(databasePath);
            _connectionString = $"Data Source={databasePath};Foreign Keys=True";
            return;
        }

        const string prefix = "Data Source=";
        if (rawConnectionString.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            var relativePath = rawConnectionString[prefix.Length..].Trim();
            var isDefaultEmbeddedDatabasePath = IsDefaultEmbeddedDatabasePath(relativePath);
            var fullPath = isDefaultEmbeddedDatabasePath
                ? AppStoragePaths.ResolveDatabasePath(null)
                : WorkspacePaths.ResolveFromBaseDirectory(relativePath);
            EnsureDirectory(fullPath);
            if (isDefaultEmbeddedDatabasePath)
            {
                CopyLegacyDefaultDatabaseIfNeeded(fullPath);
            }

            _connectionString = $"{prefix}{fullPath};Foreign Keys=True";
        }
        else
        {
            _connectionString = rawConnectionString.Contains("Foreign Keys=", StringComparison.OrdinalIgnoreCase)
                ? rawConnectionString
                : $"{rawConnectionString};Foreign Keys=True";
        }
    }

    public IDbConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }

    private static void EnsureDirectory(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static bool IsDefaultEmbeddedDatabasePath(string path)
    {
        var normalizedPath = path.Replace('\\', '/').TrimStart('/');
        return string.Equals(normalizedPath, "data/ApixPress.db", StringComparison.OrdinalIgnoreCase);
    }

    private static void CopyLegacyDefaultDatabaseIfNeeded(string databasePath)
    {
        if (File.Exists(databasePath))
        {
            return;
        }

        var legacyDatabasePath = WorkspacePaths.ResolveFromBaseDirectory(Path.Combine("data", AppStoragePaths.DatabaseFileName));
        if (string.Equals(legacyDatabasePath, databasePath, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(legacyDatabasePath))
        {
            return;
        }

        File.Copy(legacyDatabasePath, databasePath);
    }
}
