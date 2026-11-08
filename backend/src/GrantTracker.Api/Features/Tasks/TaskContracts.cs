using System.ComponentModel.DataAnnotations;
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

/// <summary>
/// Request shape is checked here (MiniValidation); the business rules for
/// title and due date (BR-2, BR-3) are enforced by the domain.
/// </summary>
public sealed record CreateTaskRequest
{
    [Required(ErrorMessage = "Title is required.")]
    public string? Title { get; init; }

    [Required(ErrorMessage = "Due date is required.")]
    public DateOnly? DueDate { get; init; }

    [Required(ErrorMessage = "Priority is required.")]
    public TaskPriority? Priority { get; init; }
}

/// <param name="DaysOverdue">Whole days between the due date and today; always at least 1.</param>
public sealed record OverdueTaskDto(
    int Id,
    int GrantId,
    string GrantReferenceNumber,
    string Title,
    DateOnly DueDate,
    TaskPriority Priority,
    int DaysOverdue);
