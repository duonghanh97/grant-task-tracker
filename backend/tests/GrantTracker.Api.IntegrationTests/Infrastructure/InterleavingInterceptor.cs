using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace GrantTracker.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Runs a competing action just before the API's next SaveChanges, i.e. after
/// the request has read its data but before it writes. This reproduces a race
/// deterministically instead of relying on thread timing.
/// </summary>
internal sealed class InterleavingInterceptor : SaveChangesInterceptor
{
    private Func<Task>? _pending;

    /// <summary>
    /// Arms the interceptor. It fires once, so a competing request made from
    /// <paramref name="action"/> saves normally.
    /// </summary>
    public void BeforeNextSave(Func<Task> action) => _pending = action;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var action = Interlocked.Exchange(ref _pending, null);
        if (action is not null)
        {
            await action();
        }

        return result;
    }

    public static ApiFactory CreateFactory(out InterleavingInterceptor interceptor)
    {
        var created = new InterleavingInterceptor();
        interceptor = created;
        return new ApiFactory
        {
            ConfigureServices = services =>
                services.ConfigureDbContext<GrantTracker.Data.AppDbContext>(options => options.AddInterceptors(created)),
        };
    }
}
