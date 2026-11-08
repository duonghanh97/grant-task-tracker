using GrantTracker.Api.IntegrationTests.Infrastructure;
using GrantTracker.Domain.Grants;
using GrantTracker.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace GrantTracker.Api.IntegrationTests.Data;

public class SchemaTests
{
    private static readonly DateOnly Today = new(2026, 11, 8);

    [Test]
    public async Task MigrationsApplyToEmptyDatabaseAndMatchTheModel()
    {
        await using var database = new TempSqliteDatabase();
        await using var db = await database.CreateMigratedContextAsync();

        var applied = await db.Database.GetAppliedMigrationsAsync();
        var journalMode = await db.Database
            .SqlQueryRaw<string>("SELECT journal_mode AS Value FROM pragma_journal_mode")
            .SingleAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(applied, Has.Some.EndsWith("_InitialCreate"));
            Assert.That(db.Database.HasPendingModelChanges(), Is.False,
                "The model has changed without a migration. Run: dotnet ef migrations add <Name>");
            Assert.That(journalMode, Is.EqualTo("wal"));
        }
    }

    [Test]
    public async Task DeletingGrantDeletesItsTasks()
    {
        await using var database = new TempSqliteDatabase();
        await using (var db = await database.CreateMigratedContextAsync())
        {
            var grant = new Grant("GR-1", "Org", Today);
            grant.AddTask("Task", Today, TaskPriority.Low, Today);
            db.Grants.Add(grant);
            await db.SaveChangesAsync();
        }

        await using (var db = database.CreateContext())
        {
            db.Grants.Remove(await db.Grants.SingleAsync());
            await db.SaveChangesAsync();
        }

        await using (var db = database.CreateContext())
        {
            Assert.That(await db.Tasks.CountAsync(), Is.Zero);
        }
    }

    [Test]
    public async Task ReferenceNumberIsUnique()
    {
        await using var database = new TempSqliteDatabase();
        await using var db = await database.CreateMigratedContextAsync();
        db.Grants.Add(new Grant("GR-1", "First", Today));
        db.Grants.Add(new Grant("GR-1", "Second", Today));

        Assert.That(() => db.SaveChangesAsync(), Throws.InstanceOf<DbUpdateException>());
    }

    [Test]
    public async Task GrantAndTasksRoundTrip()
    {
        await using var database = new TempSqliteDatabase();
        await using (var db = await database.CreateMigratedContextAsync())
        {
            var grant = new Grant("GR-1", "Org", Today.AddDays(-10));
            var done = grant.AddTask("Done", Today.AddDays(-5), TaskPriority.High, Today.AddDays(-10));
            grant.AddTask("Pending", Today.AddDays(5), TaskPriority.Medium, Today.AddDays(-10));
            grant.CompleteTask(done, Today.AddDays(-6));
            db.Grants.Add(grant);
            await db.SaveChangesAsync();
        }

        await using (var db = database.CreateContext())
        {
            var grant = await db.Grants.Include(g => g.Tasks).SingleAsync();
            var tasks = grant.Tasks.OrderBy(t => t.Title).ToList();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(grant.ApplicationDate, Is.EqualTo(Today.AddDays(-10)));
                Assert.That(grant.Status, Is.EqualTo(GrantStatus.Open));
                Assert.That(tasks.Select(t => t.Title), Is.EqualTo(new[] { "Done", "Pending" }));
                Assert.That(tasks[0].CompletedDate, Is.EqualTo(Today.AddDays(-6)));
                Assert.That(tasks[0].Priority, Is.EqualTo(TaskPriority.High));
                Assert.That(tasks[1].CompletedDate, Is.Null);
                Assert.That(grant.PendingTaskCount, Is.EqualTo(1));
            }
        }
    }
}
