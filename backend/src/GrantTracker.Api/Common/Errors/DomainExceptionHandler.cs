using GrantTracker.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GrantTracker.Api.Common.Errors;

/// <summary>Maps business-rule violations to 400 (validation) or 409 (conflict).</summary>
internal sealed class DomainExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DomainException domainException)
        {
            return false;
        }

        ProblemDetails problem = domainException.Kind == DomainErrorKind.Validation
            ? new HttpValidationProblemDetails(ApiProblem.CamelCaseKeys(domainException.FieldErrors))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = ApiProblem.ValidationTitle,
            }
            : new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = ApiProblem.TitleFor(domainException.Code),
                Detail = domainException.Message,
            };
        problem.Type = ApiProblem.TypeFor(domainException.Code);
        problem.Extensions["code"] = domainException.Code;

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
