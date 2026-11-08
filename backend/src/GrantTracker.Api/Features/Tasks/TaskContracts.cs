using GrantTracker.Domain.Tasks;

namespace GrantTracker.Api.Features.Tasks;

/// <param name="IsOverdue">Computed by the server with its business date (BR-6).</param>
public sealed record TaskDto(
    int Id,
    int GrantId,
    string Title,
    DateOnly DueDate,
    TaskPriority Priority,
    DateOnly? CompletedDate,
    bool IsOverdue)
{
    public static TaskDto From(GrantTask task, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(task);
        return new(task.Id, task.GrantId, task.Title, task.DueDate, task.Priority, task.CompletedDate,
            task.IsOverdue(today));
    }
}
