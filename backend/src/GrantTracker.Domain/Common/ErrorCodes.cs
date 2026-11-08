namespace GrantTracker.Domain.Common;

/// <summary>
/// Stable, machine-readable error codes returned in the <c>code</c> field of
/// every error response. Clients may rely on these values; do not rename them.
/// </summary>
public static class ErrorCodes
{
    // 400
    public const string ValidationFailed = "VALIDATION_FAILED";

    // 404
    public const string NotFound = "NOT_FOUND";
    public const string GrantNotFound = "GRANT_NOT_FOUND";
    public const string TaskNotFound = "TASK_NOT_FOUND";

    // 409
    public const string GrantClosed = "GRANT_CLOSED";
    public const string GrantHasPendingHighPriorityTasks = "GRANT_HAS_PENDING_HIGH_PRIORITY_TASKS";
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";

    // 5xx
    public const string UnexpectedError = "UNEXPECTED_ERROR";
}
