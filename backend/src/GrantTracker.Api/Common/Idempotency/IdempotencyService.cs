using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GrantTracker.Data;
using GrantTracker.Data.Idempotency;
using GrantTracker.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace GrantTracker.Api.Common.Idempotency;

/// <summary>
/// Stores and looks up <c>Idempotency-Key</c> records for task creation (OP-3).
/// A record points at the task the request created; records expire after
/// <see cref="Retention"/>.
/// </summary>
public sealed class IdempotencyService(AppDbContext db, TimeProvider timeProvider)
{
    public const string HeaderName = "Idempotency-Key";
    public const string ReplayedHeaderName = "Idempotent-Replayed";

    public static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    private DateTime NowUtc => timeProvider.GetUtcNow().UtcDateTime;

    private DateTime CutoffUtc => NowUtc - Retention;

    /// <summary>SHA-256 (hex) of the grant id and the request body as received.</summary>
    public static string ComputeHash(int grantId, string? title, DateOnly? dueDate, TaskPriority? priority)
    {
        var canonical = JsonSerializer.Serialize(new object?[]
        {
            grantId,
            title,
            dueDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            priority?.ToString(),
        });
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    /// <summary>Finds an unexpired record and the task it created.</summary>
    public Task<IdempotencyRecord?> FindAsync(string key, CancellationToken cancellationToken)
    {
        var cutoff = CutoffUtc;
        return db.IdempotencyRecords.AsNoTracking()
            .Include(r => r.CreatedTask)
            .SingleOrDefaultAsync(r => r.Key == key && r.CreatedAtUtc >= cutoff, cancellationToken);
    }

    /// <summary>
    /// Tracks a new record; it is saved with the task in the caller's
    /// SaveChanges. Expired records are deleted first, which also frees an
    /// expired key for reuse.
    /// </summary>
    public async Task AddAsync(string key, string requestHash, GrantTask createdTask, CancellationToken cancellationToken)
    {
        await PurgeExpiredAsync(cancellationToken);
        db.IdempotencyRecords.Add(new IdempotencyRecord
        {
            Key = key,
            RequestHash = requestHash,
            CreatedTask = createdTask,
            CreatedAtUtc = NowUtc,
        });
    }

    public Task<int> PurgeExpiredAsync(CancellationToken cancellationToken)
    {
        var cutoff = CutoffUtc;
        return db.IdempotencyRecords.Where(r => r.CreatedAtUtc < cutoff).ExecuteDeleteAsync(cancellationToken);
    }
}
