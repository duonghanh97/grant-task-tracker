using GrantTracker.Api.Common.Errors;
using GrantTracker.Api.Common.Validation;
using Microsoft.AspNetCore.Http.HttpResults;

namespace GrantTracker.Api.Features.Tasks;

public static class TaskEndpoints
{
    public static RouteGroupBuilder MapTaskEndpoints(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);

        api.MapPost("/grants/{grantId:int}/tasks", AddTask)
            .WithTags("Tasks")
            .WithSummary("Add a task to an open grant.")
            .WithValidation<CreateTaskRequest>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

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
        int grantId, CreateTaskRequest request, TaskService service, CancellationToken cancellationToken)
    {
        var task = await service.AddAsync(grantId, request, cancellationToken);
        return task is null
            ? ApiProblem.GrantNotFound(grantId)
            : TypedResults.Created((string?)null, task);
    }

    private static async Task<Results<Ok<TaskDto>, ProblemHttpResult>> CompleteTask(
        int id, TaskService service, CancellationToken cancellationToken)
    {
        var task = await service.CompleteAsync(id, cancellationToken);
        return task is null ? ApiProblem.TaskNotFound(id) : TypedResults.Ok(task);
    }
}
