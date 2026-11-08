using GrantTracker.Api.Common.Time;
using GrantTracker.Data;
using GrantTracker.Data.Seeding;
using Microsoft.Extensions.Options;

namespace GrantTracker.Api.Common.Database;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Delete and recreate the database on every start (Development default).</summary>
    public bool ResetOnStartup { get; set; }

    /// <summary>Seed demonstration data when the database is empty.</summary>
    public bool Seed { get; set; } = true;
}

internal static class DatabaseStartup
{
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var options = services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var clock = services.GetRequiredService<BusinessClock>();
        var db = services.GetRequiredService<AppDbContext>();

        await DatabaseInitializer.InitializeAsync(
            db,
            options.ResetOnStartup,
            options.Seed ? ctx => DatabaseSeeder.Seed(ctx, clock.Today) : null,
            app.Lifetime.ApplicationStopping);
    }
}
