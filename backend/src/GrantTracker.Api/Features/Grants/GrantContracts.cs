using GrantTracker.Api.Features.Tasks;
using GrantTracker.Domain.Grants;

namespace GrantTracker.Api.Features.Grants;

public sealed record GrantSummaryDto(
    int Id,
    string ReferenceNumber,
    string OrganisationName,
    DateOnly ApplicationDate,
    GrantStatus Status,
    int PendingTaskCount);

/// <param name="Tasks">Ordered by due date, then id.</param>
public sealed record GrantDetailDto(
    int Id,
    string ReferenceNumber,
    string OrganisationName,
    DateOnly ApplicationDate,
    GrantStatus Status,
    int PendingTaskCount,
    IReadOnlyList<TaskDto> Tasks)
{
    public static GrantDetailDto From(Grant grant, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(grant);
        return new(
            grant.Id,
            grant.ReferenceNumber,
            grant.OrganisationName,
            grant.ApplicationDate,
            grant.Status,
            grant.PendingTaskCount,
            [.. grant.Tasks.OrderBy(t => t.DueDate).ThenBy(t => t.Id).Select(t => TaskDto.From(t, today))]);
    }
}
