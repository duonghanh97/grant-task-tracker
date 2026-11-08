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
}
