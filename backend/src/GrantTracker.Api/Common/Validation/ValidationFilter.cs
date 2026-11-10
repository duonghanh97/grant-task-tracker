using GrantTracker.Api.Common.Errors;
using MiniValidation;

namespace GrantTracker.Api.Common.Validation;

/// <summary>Validates the request body's DataAnnotations with MiniValidation before the endpoint runs.</summary>
internal sealed class ValidationFilter<T> : IEndpointFilter
    where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var argument = context.Arguments.OfType<T>().FirstOrDefault();
        if (argument is null)
        {
            return ApiProblem.Validation(
                new Dictionary<string, string[]> { ["body"] = ["A request body is required."] });
        }

        if (!MiniValidator.TryValidate(argument, out var errors))
        {
            return ApiProblem.Validation(errors);
        }

        return await next(context);
    }
}

internal static class ValidationFilterExtensions
{
    public static RouteHandlerBuilder WithValidation<T>(this RouteHandlerBuilder builder)
        where T : class =>
        builder.AddEndpointFilter<ValidationFilter<T>>().ProducesValidationProblem();
}
