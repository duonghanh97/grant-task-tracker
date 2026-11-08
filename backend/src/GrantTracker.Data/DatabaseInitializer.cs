using Microsoft.EntityFrameworkCore;

namespace GrantTracker.Data;

public static class DatabaseInitializer
{
    /// <summary>
    /// Optionally deletes the database, applies migrations, enables WAL mode so
    /// reads do not block writes, and seeds an empty database.
    /// </summary>
    public static async Task InitializeAsync(
        AppDbContext db,
        bool resetOnStartup,
        Action<AppDbContext>? seed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        if (resetOnStartup)
        {
            await db.Database.EnsureDeletedAsync(cancellationToken);
        }

        await db.Database.MigrateAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);

        if (seed is not null && !await db.Grants.AnyAsync(cancellationToken))
        {
            seed(db);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
