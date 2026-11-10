using GrantTracker.Api.IntegrationTests.Infrastructure;
using GrantTracker.Domain.Grants;
using GrantTracker.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace GrantTracker.Api.IntegrationTests.Data;

// OP-2 at the persistence level: two officers load the same grant; whoever
// saves second fails, so BR-5 cannot be broken by interleaving.
public class ConcurrencyTokenTests
{
    private static readonly DateOnly Today = new(2026, 11, 8);

    [Test]
    public async Task AddingHighTaskAfterGrantWasClosedFailsAndRollsBack()
    {
        await using var database = await ArrangeOpenGrantAsync();
        await using var officerA = database.CreateContext();
        await using var officerB = database.CreateContext();
        var grantA = await officerA.Grants.Include(g => g.Tasks).SingleAsync();
        var grantB = await officerB.Grants.Include(g => g.Tasks).SingleAsync();

        grantA.Close();
        await officerA.SaveChangesAsync();
        grantB.AddTask("Verify bank details", Today.AddDays(1), TaskPriority.High, Today);

        Assert.That(() => officerB.SaveChangesAsync(), Throws.InstanceOf<DbUpdateConcurrencyException>());
        await AssertBr5HoldsAsync(database, expectedStatus: GrantStatus.Closed, expectedTasks: 0);
    }

    [Test]
    public async Task ClosingAfterHighTaskWasAddedFails()
    {
        await using var database = await ArrangeOpenGrantAsync();
        await using var officerA = database.CreateContext();
        await using var officerB = database.CreateContext();
        var grantA = await officerA.Grants.Include(g => g.Tasks).SingleAsync();
        var grantB = await officerB.Grants.Include(g => g.Tasks).SingleAsync();

        grantB.AddTask("Verify bank details", Today.AddDays(1), TaskPriority.High, Today);
        await officerB.SaveChangesAsync();
        grantA.Close(); // passes in memory: officer A's snapshot has no High task

        Assert.That(() => officerA.SaveChangesAsync(), Throws.InstanceOf<DbUpdateConcurrencyException>());
        await AssertBr5HoldsAsync(database, expectedStatus: GrantStatus.Open, expectedTasks: 1);
    }

    private static async Task<TempSqliteDatabase> ArrangeOpenGrantAsync()
    {
        var database = new TempSqliteDatabase();
        await using var db = await database.CreateMigratedContextAsync();
        db.Grants.Add(new Grant("GR-1", "Org", Today.AddDays(-10)));
        await db.SaveChangesAsync();
        return database;
    }

    private static async Task AssertBr5HoldsAsync(TempSqliteDatabase database, GrantStatus expectedStatus, int expectedTasks)
    {
        await using var db = database.CreateContext();
        var grant = await db.Grants.Include(g => g.Tasks).SingleAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(grant.Status, Is.EqualTo(expectedStatus));
            Assert.That(grant.Tasks, Has.Count.EqualTo(expectedTasks));
            Assert.That(
                grant.Status == GrantStatus.Closed && grant.Tasks.Any(t => t is { Priority: TaskPriority.High, IsCompleted: false }),
                Is.False,
                "BR-5 violated: a closed grant has a pending High-priority task.");
        }
    }
}
