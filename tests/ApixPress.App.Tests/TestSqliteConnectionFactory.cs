using System.Data;
using Microsoft.Data.Sqlite;
using ApixPress.App.Data.Context;

namespace ApixPress.App.Tests;

public sealed class TestSqliteConnectionFactory : IDbConnectionFactory, IDisposable
{
    private readonly string _databasePath;
    private bool _disposed;

    public TestSqliteConnectionFactory()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"ApixPress-tests-{Guid.NewGuid():N}.db");
    }

    public IDbConnection CreateConnection()
    {
        return new SqliteConnection($"Data Source={_databasePath};Pooling=False;Foreign Keys=True");
    }

    public async Task InitializeAsync()
    {
        var migrationPath = Path.Combine(AppContext.BaseDirectory, "001_Initial.sql");
        var sql = await File.ReadAllTextAsync(migrationPath);

        await using var connection = new SqliteConnection($"Data Source={_databasePath};Pooling=False;Foreign Keys=True");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();

        // 测试库必须走与生产相同的迁移链，确保来源字段和索引与当前 schema 一致。
        new DatabaseInitializer(this).Initialize();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }
}
