using GrantTracker.Api.Common.Time;
using GrantTracker.Data;
using GrantTracker.Domain.Grants;
using Microsoft.EntityFrameworkCore;

namespace GrantTracker.Api.Features.Grants;

public sealed class GrantService(AppDbContext db, BusinessClock clock)
{
    public async Task<IReadOnlyList<GrantSummaryDto>> ListAsync(GrantStatus? status, CancellationToken cancellationToken)
    {
        var query = db.Grants.AsNoTracking();
        if (status is { } filter)
        {
            query = query.Where(g => g.Status == filter);
        }

        return await query
            .OrderBy(g => g.ReferenceNumber)
            .Select(g => new GrantSummaryDto(
                g.Id,
                g.ReferenceNumber,
                g.OrganisationName,
                g.ApplicationDate,
                g.Status,
                g.Tasks.Count(t => t.CompletedDate == null)))
            .ToListAsync(cancellationToken);
    }

    public async Task<GrantDetailDto?> GetAsync(int id, CancellationToken cancellationToken)
    {
        var grant = await db.Grants.AsNoTracking()
            .Include(g => g.Tasks)
            .SingleOrDefaultAsync(g => g.Id == id, cancellationToken);

        return grant is null ? null : GrantDetailDto.From(grant, clock.Today);
    }

    /// <summary>
    /// Closes a grant (BR-5). Closing an already-closed grant returns it unchanged.
    /// </summary>
    /// <returns>The grant, or <c>null</c> if it does not exist.</returns>
    public async Task<GrantDetailDto?> CloseAsync(int id, CancellationToken cancellationToken)
    {
        // BR-5 is checked against every task, so all of them must be loaded.
        var grant = await db.Grants
            .Include(g => g.Tasks)
            .SingleOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (grant is null)
        {
            return null;
        }

        if (grant.Close())
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return GrantDetailDto.From(grant, clock.Today);
    }
}
