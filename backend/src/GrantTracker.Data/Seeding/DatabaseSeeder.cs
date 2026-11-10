using GrantTracker.Domain.Grants;
using GrantTracker.Domain.Tasks;

namespace GrantTracker.Data.Seeding;

/// <summary>
/// Seeds the demonstration data from SPECS §9. All dates are relative to the
/// business date the application starts on, so overdue tasks stay overdue.
/// </summary>
/// <remarks>
/// The data is built through the domain methods, so it obeys the same
/// business rules as data entered through the API. Each task is added "as of"
/// its grant's application date, which is how a due date can now be in the past.
/// </remarks>
public static class DatabaseSeeder
{
    public static void Seed(AppDbContext db, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(db);

        db.Grants.AddRange(
            Riverside(today),
            Northside(today),
            Harbour(today),
            Hillview(today));
    }

    private static Grant Riverside(DateOnly today)
    {
        var grant = new Grant("GR-100001", "Riverside Community Garden Inc.", today.AddDays(-60));
        var asOf = grant.ApplicationDate;

        grant.AddTask("Verify bank details", today.AddDays(-3), TaskPriority.High, asOf);
        grant.AddTask("Request quote for tools", today.AddDays(5), TaskPriority.Medium, asOf);
        var eligibility = grant.AddTask(
            "Check eligibility documents", today.AddDays(-50), TaskPriority.High, asOf);
        grant.CompleteTask(eligibility, eligibility.DueDate);

        return grant;
    }

    private static Grant Northside(DateOnly today)
    {
        var grant = new Grant("GR-100002", "Northside Youth Sports Club", today.AddDays(-20));
        var asOf = grant.ApplicationDate;

        grant.AddTask("Review progress report", today.AddDays(-1), TaskPriority.Low, asOf);
        grant.AddTask("Schedule site visit", today.AddDays(10), TaskPriority.Medium, asOf);

        return grant;
    }

    private static Grant Harbour(DateOnly today)
    {
        var grant = new Grant("GR-100003", "Harbour Arts Collective", today.AddDays(-200));
        var asOf = grant.ApplicationDate;

        var acquittal = grant.AddTask(
            "Final acquittal review", today.AddDays(-150), TaskPriority.High, asOf);
        grant.CompleteTask(acquittal, acquittal.DueDate);
        grant.Close();

        return grant;
    }

    private static Grant Hillview(DateOnly today) =>
        new("GR-100004", "Hillview Library Friends Group", today.AddDays(-5));
}
