using System.Net;
using System.Net.Http.Json;
using GrantTracker.Api.Features.Grants;
using GrantTracker.Api.Features.Tasks;
using GrantTracker.Api.IntegrationTests.Infrastructure;
using GrantTracker.Domain.Common;
using GrantTracker.Domain.Grants;
using GrantTracker.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace GrantTracker.Api.IntegrationTests.Api;

// OP-2: business rules, especially BR-5, hold when officers act on the same grant at once.
public class ConcurrencyTests
{
    private static readonly DateOnly Today = ApiFactory.Today;

    [Test]
    public async Task HighTaskAddedWhileClosingMakesTheCloseFail()
    {
        await using var factory = InterleavingInterceptor.CreateFactory(out var interceptor);
        var grant = await ArrangeGrantAsync(factory);
        using var client = factory.CreateClient();
        HttpResponseMessage? competing = null;
        interceptor.BeforeNextSave(async () => competing = await client.PostAsJsonAsync(
            $"/api/grants/{grant.Id}/tasks", new { title = "Urgent", dueDate = Today.AddDays(1), priority = "High" }));

        var close = await client.PutAsync($"/api/grants/{grant.Id}/close", null);

        await close.ReadProblemAsync(HttpStatusCode.Conflict, ErrorCodes.ConcurrencyConflict);
        Assert.That(competing!.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        await AssertBr5HoldsAsync(factory, grant.Id, GrantStatus.Open);
    }

    [Test]
    public async Task ClosingWhileAddingAHighTaskMakesTheAddFail()
    {
        await using var factory = InterleavingInterceptor.CreateFactory(out var interceptor);
        var grant = await ArrangeGrantAsync(factory);
        using var client = factory.CreateClient();
        HttpResponseMessage? competing = null;
        interceptor.BeforeNextSave(async () => competing = await client.PutAsync($"/api/grants/{grant.Id}/close", null));

        var add = await client.PostAsJsonAsync(
            $"/api/grants/{grant.Id}/tasks", new { title = "Urgent", dueDate = Today.AddDays(1), priority = "High" });

        await add.ReadProblemAsync(HttpStatusCode.Conflict, ErrorCodes.ConcurrencyConflict);
        Assert.That(competing!.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        await AssertBr5HoldsAsync(factory, grant.Id, GrantStatus.Closed);
        await using var db = factory.CreateDbContext();
        Assert.That(await db.Tasks.CountAsync(), Is.Zero, "The losing insert must be rolled back.");
    }

    // G1: a completion that loses a race with an identical completion still succeeds.
    [Test]
    public async Task SimultaneousCompletionsOfTheSameTaskBothSucceed()
    {
        await using var factory = InterleavingInterceptor.CreateFactory(out var interceptor);
        var grant = new Grant("GR-1", "Org", Today.AddDays(-10));
        var task = grant.AddTask("Verify bank details", Today.AddDays(1), TaskPriority.High, Today.AddDays(-10));
        await factory.ArrangeAsync(db => db.Grants.Add(grant));
        using var client = factory.CreateClient();
        HttpResponseMessage? competing = null;
        interceptor.BeforeNextSave(async () => competing = await client.PutAsync($"/api/tasks/{task.Id}/complete", null));

        var first = await (await client.PutAsync($"/api/tasks/{task.Id}/complete", null)).ReadAsync<TaskDto>(HttpStatusCode.OK);
        var second = await competing!.ReadAsync<TaskDto>(HttpStatusCode.OK);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.CompletedDate, Is.EqualTo(Today));
            Assert.That(second.CompletedDate, Is.EqualTo(first.CompletedDate));
        }
    }

    // Each write must change the grant's Version, or the races above would go undetected.
    [Test]
    public async Task EveryWritePathChangesTheGrantVersionAndNoOpsDoNot()
    {
        await using var factory = new ApiFactory();
        var grant = await ArrangeGrantAsync(factory);
        using var client = factory.CreateClient();
        var versions = new List<Guid> { await VersionAsync(factory, grant.Id) };

        var task = await (await client.PostAsJsonAsync($"/api/grants/{grant.Id}/tasks",
            new { title = "Task", dueDate = Today.AddDays(1), priority = "High" })).ReadAsync<TaskDto>(HttpStatusCode.Created);
        versions.Add(await VersionAsync(factory, grant.Id));

        (await client.PutAsync($"/api/tasks/{task.Id}/complete", null)).EnsureSuccessStatusCode();
        versions.Add(await VersionAsync(factory, grant.Id));

        (await client.PutAsync($"/api/grants/{grant.Id}/close", null)).EnsureSuccessStatusCode();
        versions.Add(await VersionAsync(factory, grant.Id));

        (await client.PutAsync($"/api/tasks/{task.Id}/complete", null)).EnsureSuccessStatusCode();
        (await client.PutAsync($"/api/grants/{grant.Id}/close", null)).EnsureSuccessStatusCode();
        var afterNoOps = await VersionAsync(factory, grant.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(versions, Is.Unique, "add task, complete task and close grant must each change Version");
            Assert.That(afterNoOps, Is.EqualTo(versions[^1]), "repeated complete/close must not write");
        }
    }

    // Not deterministic like the tests above, but exercises real parallel requests.
    [Test]
    public async Task ParallelCloseAndAddHighNeverBreakBr5()
    {
        await using var factory = new ApiFactory();
        var grants = Enumerable.Range(1, 10).Select(i => new Grant($"GR-{i}", "Org", Today.AddDays(-10))).ToList();
        await factory.ArrangeAsync(db => db.Grants.AddRange(grants));
        using var client = factory.CreateClient();

        foreach (var grant in grants)
        {
            var close = client.PutAsync($"/api/grants/{grant.Id}/close", null);
            var add = client.PostAsJsonAsync($"/api/grants/{grant.Id}/tasks",
                new { title = "Urgent", dueDate = Today.AddDays(1), priority = "High" });
            var results = await Task.WhenAll(close, add);

            Assert.That(results.Count(r => r.IsSuccessStatusCode), Is.EqualTo(1),
                $"Exactly one of close/add should win for {grant.ReferenceNumber}: "
                + string.Join(", ", results.Select(r => (int)r.StatusCode)));
        }

        await using var db = factory.CreateDbContext();
        var violations = await db.Grants
            .Where(g => g.Status == GrantStatus.Closed)
            .CountAsync(g => g.Tasks.Any(t => t.Priority == TaskPriority.High && t.CompletedDate == null));
        Assert.That(violations, Is.Zero);
    }

    private static async Task<Grant> ArrangeGrantAsync(ApiFactory factory)
    {
        var grant = new Grant("GR-1", "Org", Today.AddDays(-10));
        await factory.ArrangeAsync(db => db.Grants.Add(grant));
        return grant;
    }

    private static async Task<Guid> VersionAsync(ApiFactory factory, int grantId)
    {
        await using var db = factory.CreateDbContext();
        return await db.Grants.Where(g => g.Id == grantId).Select(g => g.Version).SingleAsync();
    }

    private static async Task AssertBr5HoldsAsync(ApiFactory factory, int grantId, GrantStatus expectedStatus)
    {
        await using var db = factory.CreateDbContext();
        var grant = await db.Grants.Include(g => g.Tasks).SingleAsync(g => g.Id == grantId);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(grant.Status, Is.EqualTo(expectedStatus));
            Assert.That(
                grant.Status == GrantStatus.Closed && grant.Tasks.Any(t => t is { Priority: TaskPriority.High, IsCompleted: false }),
                Is.False,
                "BR-5 violated: a closed grant has a pending High-priority task.");
        }
    }
}
