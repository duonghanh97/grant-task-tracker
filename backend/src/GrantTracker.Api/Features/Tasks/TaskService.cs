using GrantTracker.Api.Common.Idempotency;
using GrantTracker.Api.Common.Time;
using GrantTracker.Data;
using GrantTracker.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace GrantTracker.Api.Features.Tasks;

public sealed class TaskService(AppDbContext db, BusinessClock clock, IdempotencyService idempotency)
{
    /// <summary>
    /// Adds a task (BR-1 to BR-3). The idempotency key makes retries safe
    /// (OP-3): a repeat of a successful request returns the task it created.
    /// </summary>
    public async Task<AddTaskResult> AddAsync(
        int grantId, CreateTaskRequest request, string idempotencyKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var requestHash = IdempotencyService.ComputeHash(grantId, request.Title, request.DueDate, request.Priority);

        if (await TryReplayAsync(idempotencyKey, requestHash, cancellationToken) is { } replay)
        {
            return replay;
        }

        var grant = await db.Grants.SingleOrDefaultAsync(g => g.Id == grantId, cancellationToken);
        if (grant is null)
        {
            return AddTaskResult.GrantNotFound;
        }

        var today = clock.Today;
        var task = grant.AddTask(request.Title, request.DueDate!.Value, request.Priority!.Value, today);
        await idempotency.AddAsync(idempotencyKey, requestHash, task, cancellationToken);

        try
        {
            // Inserts the task and its idempotency record and updates the
            // grant's Version, all in one transaction.
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // An identical request may have saved first. Its record makes this
            // save fail on the key, or its task insert on the grant's Version.
            db.ChangeTracker.Clear();
            if (await TryReplayAsync(idempotencyKey, requestHash, cancellationToken) is { } winner)
            {
                return winner;
            }

            throw;
        }

        return AddTaskResult.Created(TaskDto.From(task, today));
    }

    private async Task<AddTaskResult?> TryReplayAsync(string key, string requestHash, CancellationToken cancellationToken)
    {
        var record = await idempotency.FindAsync(key, cancellationToken);
        if (record is null)
        {
            return null;
        }

        return record.RequestHash == requestHash
            ? AddTaskResult.Replayed(TaskDto.From(record.CreatedTask, clock.Today))
            : AddTaskResult.KeyReused;
    }

    /// <summary>
    /// Completes a task (BR-4). Repeating the request returns the task with its
    /// original completion date.
    /// </summary>
    /// <remarks>
    /// If the grant changed between this request's read and write (for example,
    /// the same completion was sent twice at once), the save fails on the
    /// grant's Version. Completion is idempotent, so the service reloads and
    /// tries once more: if the task is now completed, that attempt changes
    /// nothing and returns 200 (G1). A second conflict returns 409.
    /// </remarks>
    /// <returns>The task, or <c>null</c> if it does not exist.</returns>
    public async Task<TaskDto?> CompleteAsync(int taskId, CancellationToken cancellationToken)
    {
        try
        {
            return await TryCompleteAsync(taskId, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return await TryCompleteAsync(taskId, cancellationToken);
        }
    }

    private async Task<TaskDto?> TryCompleteAsync(int taskId, CancellationToken cancellationToken)
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

    /// <summary>
    /// Lists overdue tasks across all grants (BR-6), most overdue first. Tasks
    /// on closed grants are included: BR-5 lets a grant close with pending Low
    /// or Medium tasks.
    /// </summary>
    public async Task<IReadOnlyList<OverdueTaskDto>> ListOverdueAsync(CancellationToken cancellationToken)
    {
        var today = clock.Today;
        var rows = await db.Tasks.AsNoTracking()
            .Where(GrantTask.OverdueOn(today))
            .OrderBy(t => t.DueDate)
            .ThenBy(t => t.Id)
            .Select(t => new { t.Id, t.GrantId, t.Grant.ReferenceNumber, t.Title, t.DueDate, t.Priority })
            .ToListAsync(cancellationToken);

        return
        [
            .. rows.Select(r => new OverdueTaskDto(
                r.Id, r.GrantId, r.ReferenceNumber, r.Title, r.DueDate, r.Priority,
                today.DayNumber - r.DueDate.DayNumber)),
        ];
    }
}

public enum AddTaskOutcome
{
    Created,
    Replayed,
    GrantNotFound,
    KeyReused,
}

public sealed record AddTaskResult(AddTaskOutcome Outcome, TaskDto? Task)
{
    public static readonly AddTaskResult GrantNotFound = new(AddTaskOutcome.GrantNotFound, null);
    public static readonly AddTaskResult KeyReused = new(AddTaskOutcome.KeyReused, null);

    public static AddTaskResult Created(TaskDto task) => new(AddTaskOutcome.Created, task);

    public static AddTaskResult Replayed(TaskDto task) => new(AddTaskOutcome.Replayed, task);
}
