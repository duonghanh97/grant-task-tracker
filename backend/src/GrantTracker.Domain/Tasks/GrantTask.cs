using System.Linq.Expressions;
using GrantTracker.Domain.Grants;

namespace GrantTracker.Domain.Tasks;

/// <summary>
/// Work an officer must do for a grant. Named <c>GrantTask</c> to avoid
/// clashing with <see cref="System.Threading.Tasks.Task"/>. Created and changed
/// only through <see cref="Grants.Grant"/>.
/// </summary>
public sealed class GrantTask
{
    public const int TitleMaxLength = 100;

    internal GrantTask(Grant grant, string title, DateOnly dueDate, TaskPriority priority)
    {
        Grant = grant;
        Title = title;
        DueDate = dueDate;
        Priority = priority;
    }

    // Used by EF Core when loading from the database.
    private GrantTask()
    {
        Grant = null!;
        Title = null!;
    }

    public int Id { get; private set; }

    public int GrantId { get; private set; }

    public Grant Grant { get; private set; }

    public string Title { get; private set; }

    public DateOnly DueDate { get; private set; }

    public TaskPriority Priority { get; private set; }

    /// <summary>The date the task was completed; <c>null</c> while it is pending.</summary>
    public DateOnly? CompletedDate { get; private set; }

    public bool IsCompleted => CompletedDate is not null;

    /// <summary>
    /// BR-6: a task is overdue when it is not completed and its due date is
    /// before <paramref name="today"/>. A task due today is not overdue.
    /// </summary>
    public bool IsOverdue(DateOnly today) => CompletedDate is null && DueDate < today;

    /// <summary>The BR-6 rule as a query expression, for database filtering.</summary>
    public static Expression<Func<GrantTask, bool>> OverdueOn(DateOnly today) =>
        t => t.CompletedDate == null && t.DueDate < today;

    /// <summary>Records the completion date unless the task is already completed.</summary>
    internal bool Complete(DateOnly today)
    {
        if (IsCompleted)
        {
            return false;
        }

        CompletedDate = today;
        return true;
    }
}
