using GrantTracker.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace GrantTracker.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the real API in memory against its own temporary SQLite file, with a
/// fake clock. Create one per test so tests never share state or depend on order.
/// </summary>
internal sealed class ApiFactory : WebApplicationFactory<Program>
{
    /// <summary>The business date at the default clock time and offset.</summary>
    public static readonly DateOnly Today = new(2026, 11, 8);

    private readonly TempSqliteDatabase _database = new();

    /// <summary>2026-11-08 09:00 UTC unless a test changes it.</summary>
    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 11, 8, 9, 0, 0, TimeSpan.Zero));

    public string UtcOffset { get; init; } = "+00:00";

    /// <summary>Seed the SPECS §9 data at startup. Off by default so each test arranges its own data.</summary>
    public bool Seed { get; init; }

    public Action<IServiceCollection>? ConfigureServices { get; init; }

    public AppDbContext CreateDbContext() => _database.CreateContext();

    /// <summary>Saves entities directly to the database, bypassing the API.</summary>
    public async Task ArrangeAsync(Action<AppDbContext> arrange)
    {
        EnsureStarted();
        await using var db = CreateDbContext();
        arrange(db);
        await db.SaveChangesAsync();
    }

    /// <summary>Starts the host (migrating the database) without sending a request.</summary>
    public void EnsureStarted() => _ = Services;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:GrantTracker", _database.ConnectionString);
        builder.UseSetting("Database:ResetOnStartup", "false");
        builder.UseSetting("Database:Seed", Seed ? "true" : "false");
        builder.UseSetting("Business:UtcOffset", UtcOffset);
        builder.UseSetting("Serilog:MinimumLevel:Default", "Warning");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
            ConfigureServices?.Invoke(services);
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }
}
