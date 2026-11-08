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

        return api;
    }

    private static async Task<Results<Created<TaskDto>, ProblemHttpResult>> AddTask(
        int grantId, CreateTaskRequest request, TaskService service, CancellationToken cancellationToken)
    {
        var task = await service.AddAsync(grantId, request, cancellationToken);
        return task is null
            ? ApiProblem.GrantNotFound(grantId)
            : TypedResults.Created((string?)null, task);
    }
}
