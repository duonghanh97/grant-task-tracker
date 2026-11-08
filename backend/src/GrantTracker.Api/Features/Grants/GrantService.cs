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
}
