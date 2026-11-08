using GrantTracker.Api.Common.Time;
using GrantTracker.Data;
using Microsoft.EntityFrameworkCore;

namespace GrantTracker.Api.Features.Tasks;

public sealed class TaskService(AppDbContext db, BusinessClock clock)
{
    /// <returns>The created task, or <c>null</c> if the grant does not exist.</returns>
    public async Task<TaskDto?> AddAsync(int grantId, CreateTaskRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var grant = await db.Grants.SingleOrDefaultAsync(g => g.Id == grantId, cancellationToken);
        if (grant is null)
        {
            return null;
        }

        var today = clock.Today;
        var task = grant.AddTask(request.Title, request.DueDate!.Value, request.Priority!.Value, today);

        // Inserts the task and updates the grant's Version in one transaction.
        await db.SaveChangesAsync(cancellationToken);
        return TaskDto.From(task, today);
    }

    /// <summary>
    /// Completes a task (BR-4). Repeating the request returns the task with its
    /// original completion date.
    /// </summary>
    /// <returns>The task, or <c>null</c> if it does not exist.</returns>
    public async Task<TaskDto?> CompleteAsync(int taskId, CancellationToken cancellationToken)
    {
        // Loading the grant puts the task in grant.Tasks, so the change goes through Grant.
        var task = await db.Tasks
            .Include(t => t.Grant)
            .SingleOrDefaultAsync(t => t.Id == taskId, cancellationToken);
        if (task is null)
        {
            return null;
        }

        var today = clock.Today;
        if (task.Grant.CompleteTask(task, today))
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return TaskDto.From(task, today);
    }
}
