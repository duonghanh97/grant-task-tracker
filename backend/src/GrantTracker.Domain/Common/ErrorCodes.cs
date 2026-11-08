namespace GrantTracker.Domain.Common;

/// <summary>
/// Stable, machine-readable error codes returned in the <c>code</c> field of
/// every error response. Clients may rely on these values; do not rename them.
/// </summary>
public static class ErrorCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string GrantClosed = "GRANT_CLOSED";
    public const string GrantHasPendingHighPriorityTasks = "GRANT_HAS_PENDING_HIGH_PRIORITY_TASKS";
}
