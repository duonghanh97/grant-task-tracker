using GrantTracker.Api.Common.Errors;
using GrantTracker.Domain.Grants;
using Microsoft.AspNetCore.Http.HttpResults;

namespace GrantTracker.Api.Features.Grants;

public static class GrantEndpoints
{
    public static RouteGroupBuilder MapGrantEndpoints(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);
        var grants = api.MapGroup("/grants").WithTags("Grants");

        grants.MapGet("/", ListGrants)
            .WithSummary("List grants with their pending-task counts, optionally filtered by status.");

        grants.MapGet("/{id:int}", GetGrant)
            .WithSummary("Get a grant with its tasks.");

        return api;
    }

    private static async Task<Results<Ok<IReadOnlyList<GrantSummaryDto>>, ValidationProblem>> ListGrants(
        string? status, GrantService service, CancellationToken cancellationToken)
    {
        GrantStatus? filter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!TryParseStatus(status, out var parsed))
            {
                return ApiProblem.Validation(new Dictionary<string, string[]>
                {
                    ["status"] = ["Status must be Open or Closed."],
                });
            }

            filter = parsed;
        }

        return TypedResults.Ok(await service.ListAsync(filter, cancellationToken));
    }

    private static async Task<Results<Ok<GrantDetailDto>, ProblemHttpResult>> GetGrant(
        int id, GrantService service, CancellationToken cancellationToken)
    {
        var grant = await service.GetAsync(id, cancellationToken);
        return grant is null ? ApiProblem.GrantNotFound(id) : TypedResults.Ok(grant);
    }

    // Accepts names only (case-insensitive); Enum.TryParse would also accept "0" or "1".
    private static bool TryParseStatus(string value, out GrantStatus status)
    {
        foreach (var candidate in Enum.GetValues<GrantStatus>())
        {
            if (string.Equals(candidate.ToString(), value.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                status = candidate;
                return true;
            }
        }

        status = default;
        return false;
    }
}
