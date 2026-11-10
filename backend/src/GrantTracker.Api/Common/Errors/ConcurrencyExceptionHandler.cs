using GrantTracker.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GrantTracker.Api.Common.Errors;

/// <summary>
/// Maps a stale write (the grant's Version changed since it was read) to
/// 409 CONCURRENCY_CONFLICT. EF Core has already rolled back the transaction.
/// </summary>
internal sealed class ConcurrencyExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DbUpdateConcurrencyException)
        {
            return false;
        }

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = ApiProblem.TitleFor(ErrorCodes.ConcurrencyConflict),
            Detail = "Someone else changed this grant at the same time. Reload it and try again.",
            Type = ApiProblem.TypeFor(ErrorCodes.ConcurrencyConflict),
            Extensions = { ["code"] = ErrorCodes.ConcurrencyConflict },
        };

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
