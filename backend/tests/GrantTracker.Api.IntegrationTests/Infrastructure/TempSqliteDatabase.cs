using GrantTracker.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GrantTracker.Api.IntegrationTests.Infrastructure;

/// <summary>
/// A SQLite database in a temporary file, deleted on dispose. A file (not
/// in-memory) lets several connections share it, which concurrency tests need.
/// </summary>
internal sealed class TempSqliteDatabase : IAsyncDisposable
{
    public TempSqliteDatabase()
    {
        FilePath = Path.Combine(Path.GetTempPath(), $"granttracker-test-{Guid.NewGuid():N}.db");
    }

    public string FilePath { get; }

    public string ConnectionString => $"Data Source={FilePath}";

    public AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(ConnectionString).Options);

    public async Task<AppDbContext> CreateMigratedContextAsync()
    {
        var db = CreateContext();
        await DatabaseInitializer.InitializeAsync(db, resetOnStartup: false, seed: null);
        return db;
    }

    public ValueTask DisposeAsync()
    {
        using (var connection = new SqliteConnection(ConnectionString))
        {
            SqliteConnection.ClearPool(connection);
        }

        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            File.Delete(FilePath + suffix);
        }

        return ValueTask.CompletedTask;
    }
}
