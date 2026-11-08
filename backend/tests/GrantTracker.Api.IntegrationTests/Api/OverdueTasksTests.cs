using System.Net;
using GrantTracker.Api.Features.Tasks;
using GrantTracker.Api.IntegrationTests.Infrastructure;
using GrantTracker.Domain.Grants;
using GrantTracker.Domain.Tasks;

namespace GrantTracker.Api.IntegrationTests.Api;

// BR-6: overdue = not completed and due before today; listed most overdue first.
public class OverdueTasksTests
{
    private static readonly DateOnly Today = ApiFactory.Today;

    [Test]
    public async Task ListsTheSeededOverdueTasksMostOverdueFirst()
    {
        await using var factory = new ApiFactory { Seed = true };
        using var client = factory.CreateClient();

        var overdue = await GetOverdueAsync(client);

        Assert.That(
            overdue.Select(t => (t.GrantReferenceNumber, t.Title, t.Priority, t.DaysOverdue)),
            Is.EqualTo(new[]
            {
                ("GR-100001", "Verify bank details", TaskPriority.High, 3),
                ("GR-100002", "Review progress report", TaskPriority.Low, 1),
            }),
            "Completed past-due tasks and future tasks are excluded.");
    }

    [Test]
    public async Task ExcludesTasksDueTodayAndCompletedTasks()
    {
        await using var factory = new ApiFactory();
        var created = Today.AddDays(-30);
        var grant = new Grant("GR-1", "Org", created);
        grant.AddTask("Due today", Today, TaskPriority.High, created);
        var completed = grant.AddTask("Completed late", Today.AddDays(-5), TaskPriority.High, created);
        grant.CompleteTask(completed, Today.AddDays(-1));
        grant.AddTask("Due yesterday", Today.AddDays(-1), TaskPriority.Low, created);
        await factory.ArrangeAsync(db => db.Grants.Add(grant));
        using var client = factory.CreateClient();

        var overdue = await GetOverdueAsync(client);

        Assert.That(overdue.Select(t => t.Title), Is.EqualTo(new[] { "Due yesterday" }));
    }

    [Test]
    public async Task OrdersByDueDateThenIdAcrossGrants()
    {
        await using var factory = new ApiFactory();
        var created = Today.AddDays(-30);
        var first = new Grant("GR-1", "First", created);
        var second = new Grant("GR-2", "Second", created);
        var b = second.AddTask("B (due -5, added first)", Today.AddDays(-5), TaskPriority.Low, created);
        var a = first.AddTask("A (due -10)", Today.AddDays(-10), TaskPriority.Low, created);
        await factory.ArrangeAsync(db => db.Grants.AddRange(second, first));
        var c = first.AddTask("C (due -5, added second)", Today.AddDays(-5), TaskPriority.Low, created);
        await factory.ArrangeAsync(db => db.Grants.Attach(first));
        using var client = factory.CreateClient();

        var overdue = await GetOverdueAsync(client);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(b.Id, Is.LessThan(c.Id), "Arrange: B must have the lower id.");
            Assert.That(overdue.Select(t => t.Id), Is.EqualTo(new[] { a.Id, b.Id, c.Id }));
            Assert.That(overdue.Select(t => t.DaysOverdue), Is.EqualTo(new[] { 10, 5, 5 }));
        }
    }

    // G4: BR-5 allows closing a grant with pending Low/Medium tasks, and the
    // spec asks for overdue tasks across all grants.
    [Test]
    public async Task IncludesOverdueTasksOnClosedGrants()
    {
        await using var factory = new ApiFactory();
        var created = Today.AddDays(-30);
        var grant = new Grant("GR-1", "Org", created);
        grant.AddTask("Left pending", Today.AddDays(-2), TaskPriority.Medium, created);
        grant.Close();
        await factory.ArrangeAsync(db => db.Grants.Add(grant));
        using var client = factory.CreateClient();

        var overdue = await GetOverdueAsync(client);

        Assert.That(overdue.Select(t => t.Title), Is.EqualTo(new[] { "Left pending" }));
    }

    [Test]
    public async Task ResultFollowsTheClock()
    {
        await using var factory = new ApiFactory { Seed = true };
        using var client = factory.CreateClient();

        factory.Time.Advance(TimeSpan.FromDays(6));
        var overdue = await GetOverdueAsync(client);

        Assert.That(
            overdue.Select(t => (t.Title, t.DaysOverdue)),
            Is.EqualTo(new[]
            {
                ("Verify bank details", 9),
                ("Review progress report", 7),
                ("Request quote for tools", 1),
            }));
    }

    private static async Task<OverdueTaskDto[]> GetOverdueAsync(HttpClient client) =>
        await (await client.GetAsync("/api/tasks/overdue")).ReadAsync<OverdueTaskDto[]>(HttpStatusCode.OK);
}
