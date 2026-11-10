using GrantTracker.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GrantTracker.Api.Common.Errors;

/// <summary>Maps malformed requests (e.g. invalid JSON or an unknown enum value) to 400.</summary>
internal sealed class BadRequestExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException badRequest)
        {
            return false;
        }

        var problem = new ProblemDetails
        {
            Status = badRequest.StatusCode,
            Title = "The request is malformed",
            Detail = badRequest.Message,
            Type = ApiProblem.TypeFor(ErrorCodes.ValidationFailed),
        };
        if (badRequest.StatusCode == StatusCodes.Status400BadRequest)
        {
            problem.Extensions["code"] = ErrorCodes.ValidationFailed;
        }

        httpContext.Response.StatusCode = badRequest.StatusCode;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
