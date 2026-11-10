using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GrantTracker.Data;

public static class DataServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="AppDbContext"/> on SQLite. The connection string is
    /// resolved when the context is first created, so hosts and tests can
    /// override configuration after registration.
    /// </summary>
    public static IServiceCollection AddData(
        this IServiceCollection services,
        Func<IServiceProvider, string> connectionString)
    {
        services.AddDbContext<AppDbContext>((sp, options) => options.UseSqlite(connectionString(sp)));
        return services;
    }
}
