using System.Text.Json;
using GrantTracker.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace GrantTracker.Api.Common.Errors;

/// <summary>Builds the API's ProblemDetails responses. Every error carries a stable <c>code</c>.</summary>
internal static class ApiProblem
{
    public const string ValidationTitle = "One or more validation errors occurred.";

    public static string TypeFor(string code) =>
        "https://granttracker/errors/" + code.ToLowerInvariant().Replace('_', '-');

    public static string TitleFor(string code) => code switch
    {
        ErrorCodes.GrantClosed => "Grant is closed",
        ErrorCodes.GrantHasPendingHighPriorityTasks => "Grant cannot be closed",
        ErrorCodes.ConcurrencyConflict => "The grant was changed by someone else",
        ErrorCodes.GrantNotFound => "Grant not found",
        ErrorCodes.TaskNotFound => "Task not found",
        _ => "The request could not be completed",
    };

    public static ProblemHttpResult Create(int status, string code, string? detail = null, string? title = null) =>
        TypedResults.Problem(
            detail: detail,
            statusCode: status,
            title: title ?? TitleFor(code),
            type: TypeFor(code),
            extensions: new Dictionary<string, object?> { ["code"] = code });

    public static ValidationProblem Validation(IEnumerable<KeyValuePair<string, string[]>> errors) =>
        TypedResults.ValidationProblem(
            CamelCaseKeys(errors),
            title: ValidationTitle,
            type: TypeFor(ErrorCodes.ValidationFailed),
            extensions: new Dictionary<string, object?> { ["code"] = ErrorCodes.ValidationFailed });

    public static ProblemHttpResult GrantNotFound(int id) =>
        Create(StatusCodes.Status404NotFound, ErrorCodes.GrantNotFound, $"Grant {id} does not exist.");

    public static ProblemHttpResult TaskNotFound(int id) =>
        Create(StatusCodes.Status404NotFound, ErrorCodes.TaskNotFound, $"Task {id} does not exist.");

    /// <summary>Field names match the JSON property names, e.g. <c>Title</c> → <c>title</c>.</summary>
    public static Dictionary<string, string[]> CamelCaseKeys(IEnumerable<KeyValuePair<string, string[]>> errors) =>
        errors.ToDictionary(e => JsonNamingPolicy.CamelCase.ConvertName(e.Key), e => e.Value);

    /// <summary>Fills in <c>code</c> for errors produced by the framework (routing, model binding, 500s).</summary>
    public static void AddDefaultCode(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        if (problem.Extensions.ContainsKey("code"))
        {
            return;
        }

        problem.Extensions["code"] = problem.Status switch
        {
            StatusCodes.Status400BadRequest => ErrorCodes.ValidationFailed,
            StatusCodes.Status404NotFound => ErrorCodes.NotFound,
            >= 500 => ErrorCodes.UnexpectedError,
            var status => $"HTTP_{status}",
        };
    }
}
