using System.Globalization;
using GrantTracker.Domain.Common;
using GrantTracker.Domain.Tasks;

namespace GrantTracker.Domain.Grants;

/// <summary>
/// A grant application and its tasks. <see cref="Grant"/> is the only way to
/// change a grant or its tasks, so every change also updates <see cref="Version"/>.
/// </summary>
public sealed class Grant
{
    private readonly List<GrantTask> _tasks = [];

    public Grant(string referenceNumber, string organisationName, DateOnly applicationDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(organisationName);

        ReferenceNumber = referenceNumber.Trim();
        OrganisationName = organisationName.Trim();
        ApplicationDate = applicationDate;
        Status = GrantStatus.Open;
        Version = Guid.NewGuid();
    }

    // Used by EF Core when loading from the database.
    private Grant()
    {
        ReferenceNumber = null!;
        OrganisationName = null!;
    }

    public int Id { get; private set; }

    public string ReferenceNumber { get; private set; }

    public string OrganisationName { get; private set; }

    public DateOnly ApplicationDate { get; private set; }

    public GrantStatus Status { get; private set; }

    /// <summary>
    /// Optimistic-concurrency token for the grant and all of its tasks. It
    /// changes on every write, so two writes based on the same snapshot cannot
    /// both be saved.
    /// </summary>
    public Guid Version { get; private set; }

    public IReadOnlyCollection<GrantTask> Tasks => _tasks.AsReadOnly();

    public int PendingTaskCount => _tasks.Count(t => !t.IsCompleted);

    /// <summary>
    /// Adds a task. Enforces BR-1 (grant must be open), BR-2 (due date not in
    /// the past and not before the application date) and BR-3 (trimmed title,
    /// 1–100 characters).
    /// </summary>
    /// <exception cref="DomainException">
    /// <see cref="ErrorCodes.GrantClosed"/> conflict, or a validation error per field.
    /// </exception>
    public GrantTask AddTask(string? title, DateOnly dueDate, TaskPriority priority, DateOnly today)
    {
        if (Status == GrantStatus.Closed)
        {
            throw DomainException.Conflict(
                ErrorCodes.GrantClosed,
                $"Grant {ReferenceNumber} is closed, so tasks cannot be added to it.");
        }

        var errors = new Dictionary<string, string[]>();

        var trimmedTitle = title?.Trim() ?? string.Empty;
        if (trimmedTitle.Length == 0)
        {
            errors[nameof(GrantTask.Title)] = ["Title is required."];
        }
        else if (trimmedTitle.Length > GrantTask.TitleMaxLength)
        {
            errors[nameof(GrantTask.Title)] =
                [$"Title must be {GrantTask.TitleMaxLength} characters or fewer."];
        }

        var dueDateErrors = new List<string>();
        if (dueDate < today)
        {
            dueDateErrors.Add("Due date cannot be in the past.");
        }

        if (dueDate < ApplicationDate)
        {
            dueDateErrors.Add(
                "Due date cannot be before the grant's application date ("
                + ApplicationDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ").");
        }

        if (dueDateErrors.Count > 0)
        {
            errors[nameof(GrantTask.DueDate)] = [.. dueDateErrors];
        }

        if (!Enum.IsDefined(priority))
        {
            errors[nameof(GrantTask.Priority)] = ["Priority must be Low, Medium or High."];
        }

        if (errors.Count > 0)
        {
            throw DomainException.Validation(errors);
        }

        var task = new GrantTask(this, trimmedTitle, dueDate, priority);
        _tasks.Add(task);
        Touch();
        return task;
    }

    /// <summary>
    /// Marks one of this grant's tasks as completed on <paramref name="today"/>
    /// (BR-4). Repeating the call keeps the original completion date.
    /// </summary>
    /// <returns><c>true</c> if the task changed; <c>false</c> if it was already completed.</returns>
    public bool CompleteTask(GrantTask task, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (!_tasks.Contains(task))
        {
            throw new ArgumentException("The task does not belong to this grant.", nameof(task));
        }

        if (!task.Complete(today))
        {
            return false;
        }

        Touch();
        return true;
    }

    /// <summary>
    /// Closes the grant (BR-5). Closing an already-closed grant changes nothing.
    /// The caller must load all of the grant's tasks first.
    /// </summary>
    /// <returns><c>true</c> if the grant changed; <c>false</c> if it was already closed.</returns>
    /// <exception cref="DomainException">
    /// <see cref="ErrorCodes.GrantHasPendingHighPriorityTasks"/> if a High-priority task is pending.
    /// </exception>
    public bool Close()
    {
        if (Status == GrantStatus.Closed)
        {
            return false;
        }

        var pendingHigh = _tasks.Count(t => t.Priority == TaskPriority.High && !t.IsCompleted);
        if (pendingHigh > 0)
        {
            throw DomainException.Conflict(
                ErrorCodes.GrantHasPendingHighPriorityTasks,
                $"Grant {ReferenceNumber} has {pendingHigh} pending High-priority "
                + (pendingHigh == 1 ? "task." : "tasks."));
        }

        Status = GrantStatus.Closed;
        Touch();
        return true;
    }

    private void Touch() => Version = Guid.NewGuid();
}
