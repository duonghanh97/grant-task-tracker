using GrantTracker.Domain.Tasks;

namespace GrantTracker.Data.Idempotency;

/// <summary>
/// Remembers a successful task-creation request by its <c>Idempotency-Key</c>,
/// so a retry returns the task that was created instead of creating another (OP-3).
/// </summary>
public sealed class IdempotencyRecord
{
    public const int KeyMaxLength = 100;

    public required string Key { get; init; }

    /// <summary>SHA-256 of the grant id and the request body, as hex.</summary>
    public required string RequestHash { get; init; }

    public required GrantTask CreatedTask { get; init; }

    public int CreatedTaskId { get; private set; }

    public required DateTime CreatedAtUtc { get; init; }
}
