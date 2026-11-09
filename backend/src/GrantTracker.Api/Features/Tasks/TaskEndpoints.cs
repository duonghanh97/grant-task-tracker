using GrantTracker.Api.Common.Errors;
using GrantTracker.Api.Common.Idempotency;
using GrantTracker.Api.Common.Validation;
using GrantTracker.Data.Idempotency;
using GrantTracker.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace GrantTracker.Api.Features.Tasks;

public static class TaskEndpoints
{
    public static RouteGroupBuilder MapTaskEndpoints(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);

        api.MapPost("/grants/{grantId:int}/tasks", AddTask)
            .WithTags("Tasks")
            .WithSummary("Add a task to an open grant. Requires an Idempotency-Key header; retries with the same key return the original task.")
            .WithValidation<CreateTaskRequest>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        api.MapPut("/tasks/{id:int}/complete", CompleteTask)
            .WithTags("Tasks")
            .WithSummary("Mark a task as completed. Safe to repeat: the original completion date is kept.")
            .ProducesProblem(StatusCodes.Status409Conflict);

        api.MapGet("/tasks/overdue", ListOverdue)
            .WithTags("Tasks")
            .WithSummary("List overdue tasks across all grants, most overdue first.");

        return api;
    }

    private static async Task<Ok<IReadOnlyList<OverdueTaskDto>>> ListOverdue(
        TaskService service, CancellationToken cancellationToken) =>
        TypedResults.Ok(await service.ListOverdueAsync(cancellationToken));

    private static async Task<Results<Created<TaskDto>, ProblemHttpResult>> AddTask(
        int grantId,
        CreateTaskRequest request,
        [FromHeader(Name = IdempotencyService.HeaderName)] string? idempotencyKey,
        TaskService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, ErrorCodes.IdempotencyKeyMissing,
                "Send a unique Idempotency-Key header (for example a UUID) and reuse it when retrying.");
        }

        if (idempotencyKey.Length > IdempotencyRecord.KeyMaxLength)
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, ErrorCodes.IdempotencyKeyInvalid,
                $"The Idempotency-Key header must be at most {IdempotencyRecord.KeyMaxLength} characters.");
        }

        var result = await service.AddAsync(grantId, request, idempotencyKey, cancellationToken);
        switch (result.Outcome)
        {
            case AddTaskOutcome.GrantNotFound:
                return ApiProblem.GrantNotFound(grantId);
            case AddTaskOutcome.KeyReused:
                return ApiProblem.Create(StatusCodes.Status422UnprocessableEntity, ErrorCodes.IdempotencyKeyReused,
                    "This Idempotency-Key was used for a different request. Generate a new key for new data.");
            case AddTaskOutcome.Replayed:
                httpContext.Response.Headers[IdempotencyService.ReplayedHeaderName] = "true";
                return TypedResults.Created((string?)null, result.Task);
            default:
                return TypedResults.Created((string?)null, result.Task);
        }
    }

    private static async Task<Results<Ok<TaskDto>, ProblemHttpResult>> CompleteTask(
        int id, TaskService service, CancellationToken cancellationToken)
    {
        var task = await service.CompleteAsync(id, cancellationToken);
        return task is null ? ApiProblem.TaskNotFound(id) : TypedResults.Ok(task);
    }
}
