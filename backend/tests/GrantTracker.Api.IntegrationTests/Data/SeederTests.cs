using GrantTracker.Api.IntegrationTests.Infrastructure;
using GrantTracker.Data;
using GrantTracker.Data.Seeding;
using GrantTracker.Domain.Grants;
using GrantTracker.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace GrantTracker.Api.IntegrationTests.Data;

public class SeederTests
{
    private static readonly DateOnly Today = new(2026, 11, 8);

    [Test]
    public async Task SeedsTheSpecifiedGrantsWithDatesRelativeToToday()
    {
        await using var database = new TempSqliteDatabase();
        await SeedAsync(database);
        await using var db = database.CreateContext();

        var grants = await db.Grants.OrderBy(g => g.ReferenceNumber)
            .Select(g => new { g.ReferenceNumber, g.OrganisationName, g.ApplicationDate, g.Status })
            .ToListAsync();

        Assert.That(grants, Is.EqualTo(new[]
        {
            new { ReferenceNumber = "GR-100001", OrganisationName = "Riverside Community Garden Inc.", ApplicationDate = Today.AddDays(-60), Status = GrantStatus.Open },
            new { ReferenceNumber = "GR-100002", OrganisationName = "Northside Youth Sports Club", ApplicationDate = Today.AddDays(-20), Status = GrantStatus.Open },
            new { ReferenceNumber = "GR-100003", OrganisationName = "Harbour Arts Collective", ApplicationDate = Today.AddDays(-200), Status = GrantStatus.Closed },
            new { ReferenceNumber = "GR-100004", OrganisationName = "Hillview Library Friends Group", ApplicationDate = Today.AddDays(-5), Status = GrantStatus.Open },
        }));
    }

    [Test]
    public async Task SeedsTheSpecifiedTasks()
    {
        await using var database = new TempSqliteDatabase();
        await SeedAsync(database);
        await using var db = database.CreateContext();

        var tasks = await db.Tasks.OrderBy(t => t.Grant.ReferenceNumber).ThenBy(t => t.Title)
            .Select(t => new { t.Grant.ReferenceNumber, t.Title, t.Priority, t.DueDate, Completed = t.CompletedDate != null })
            .ToListAsync();

        Assert.That(tasks, Is.EqualTo(new[]
        {
            new { ReferenceNumber = "GR-100001", Title = "Check eligibility documents", Priority = TaskPriority.High, DueDate = Today.AddDays(-50), Completed = true },
            new { ReferenceNumber = "GR-100001", Title = "Request quote for tools", Priority = TaskPriority.Medium, DueDate = Today.AddDays(5), Completed = false },
            new { ReferenceNumber = "GR-100001", Title = "Verify bank details", Priority = TaskPriority.High, DueDate = Today.AddDays(-3), Completed = false },
            new { ReferenceNumber = "GR-100002", Title = "Review progress report", Priority = TaskPriority.Low, DueDate = Today.AddDays(-1), Completed = false },
            new { ReferenceNumber = "GR-100002", Title = "Schedule site visit", Priority = TaskPriority.Medium, DueDate = Today.AddDays(10), Completed = false },
            new { ReferenceNumber = "GR-100003", Title = "Final acquittal review", Priority = TaskPriority.High, DueDate = Today.AddDays(-150), Completed = true },
        }));
    }

    // SPECS §9: the seed data must demonstrate each of these cases.
    [Test]
    public async Task DemonstratesEveryRequiredCase()
    {
        await using var database = new TempSqliteDatabase();
        await SeedAsync(database);
        await using var db = database.CreateContext();

        var grants = await db.Grants.Include(g => g.Tasks).ToListAsync();
        var tasks = grants.SelectMany(g => g.Tasks).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tasks.Any(t => t.IsOverdue(Today)), Is.True, "an overdue pending task");
            Assert.That(tasks.Any(t => t.IsCompleted), Is.True, "a completed task");
            Assert.That(tasks.Any(t => t is { Priority: TaskPriority.High, IsCompleted: false }), Is.True, "a pending High-priority task");
            Assert.That(grants.Any(g => g.Status == GrantStatus.Closed), Is.True, "a closed grant");
            Assert.That(grants.Any(g => g.Status == GrantStatus.Open && g.Tasks.Count == 0), Is.True, "an open grant with no tasks");
        }
    }

    [Test]
    public async Task DoesNotSeedADatabaseThatAlreadyHasData()
    {
        await using var database = new TempSqliteDatabase();
        await SeedAsync(database);
        await SeedAsync(database);
        await using var db = database.CreateContext();

        Assert.That(await db.Grants.CountAsync(), Is.EqualTo(4));
    }

    private static async Task SeedAsync(TempSqliteDatabase database)
    {
        await using var db = database.CreateContext();
        await DatabaseInitializer.InitializeAsync(db, resetOnStartup: false, seed: ctx => DatabaseSeeder.Seed(ctx, Today));
    }
}
