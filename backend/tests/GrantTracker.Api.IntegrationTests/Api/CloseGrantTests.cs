using System.Net;
using GrantTracker.Api.Features.Grants;
using GrantTracker.Api.IntegrationTests.Infrastructure;
using GrantTracker.Domain.Common;
using GrantTracker.Domain.Grants;
using GrantTracker.Domain.Tasks;

namespace GrantTracker.Api.IntegrationTests.Api;

// BR-5: a grant cannot be closed while it has any pending High-priority task.
public class CloseGrantTests
{
    private static readonly DateOnly Today = ApiFactory.Today;

    [Test]
    public async Task GrantWithoutPendingHighPriorityTasksCloses()
    {
        await using var factory = new ApiFactory();
        var grant = NewGrant();
        grant.AddTask("Low task", Today.AddDays(1), TaskPriority.Low, Today);
        grant.AddTask("Medium task", Today.AddDays(1), TaskPriority.Medium, Today);
        await factory.ArrangeAsync(db => db.Grants.Add(grant));
        using var client = factory.CreateClient();

        var closed = await (await CloseAsync(client, grant.Id)).ReadAsync<GrantDetailDto>(HttpStatusCode.OK);
        var closedList = await (await client.GetAsync("/api/grants?status=Closed")).ReadAsync<GrantSummaryDto[]>(HttpStatusCode.OK);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(closed.Status, Is.EqualTo(GrantStatus.Closed));
            Assert.That(closed.PendingTaskCount, Is.EqualTo(2), "Pending Low and Medium tasks are allowed.");
            Assert.That(closedList.Select(g => g.Id), Is.EqualTo(new[] { grant.Id }));
        }
    }

    [Test]
    public async Task PendingHighPriorityTaskBlocksClosure()
    {
        await using var factory = new ApiFactory();
        var grant = NewGrant();
        grant.AddTask("Verify bank details", Today.AddDays(1), TaskPriority.High, Today);
        await factory.ArrangeAsync(db => db.Grants.Add(grant));
        using var client = factory.CreateClient();

        var problem = await (await CloseAsync(client, grant.Id))
            .ReadProblemAsync(HttpStatusCode.Conflict, ErrorCodes.GrantHasPendingHighPriorityTasks);
        var after = await (await client.GetAsync($"/api/grants/{grant.Id}")).ReadAsync<GrantDetailDto>(HttpStatusCode.OK);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((string?)problem["title"], Is.EqualTo("Grant cannot be closed"));
            Assert.That((string?)problem["detail"], Is.EqualTo("Grant GR-1 has 1 pending High-priority task."));
            Assert.That(after.Status, Is.EqualTo(GrantStatus.Open));
        }
    }

    [Test]
    public async Task CompletedHighPriorityTaskDoesNotBlockClosure()
    {
        await using var factory = new ApiFactory();
        var grant = NewGrant();
        var task = grant.AddTask("Check eligibility", Today.AddDays(1), TaskPriority.High, Today);
        await factory.ArrangeAsync(db => db.Grants.Add(grant));
        using var client = factory.CreateClient();
        (await client.PutAsync($"/api/tasks/{task.Id}/complete", null)).EnsureSuccessStatusCode();

        var closed = await (await CloseAsync(client, grant.Id)).ReadAsync<GrantDetailDto>(HttpStatusCode.OK);

        Assert.That(closed.Status, Is.EqualTo(GrantStatus.Closed));
    }

    [Test]
    public async Task ClosingAClosedGrantReturnsItUnchanged()
    {
        await using var factory = new ApiFactory();
        var grant = NewGrant();
        await factory.ArrangeAsync(db => db.Grants.Add(grant));
        using var client = factory.CreateClient();
        await (await CloseAsync(client, grant.Id)).ReadAsync<GrantDetailDto>(HttpStatusCode.OK);

        var repeated = await (await CloseAsync(client, grant.Id)).ReadAsync<GrantDetailDto>(HttpStatusCode.OK);

        Assert.That(repeated.Status, Is.EqualTo(GrantStatus.Closed));
    }

    [Test]
    public async Task UnknownGrantReturnsNotFound()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        await (await CloseAsync(client, 999)).ReadProblemAsync(HttpStatusCode.NotFound, ErrorCodes.GrantNotFound);
    }

    private static Grant NewGrant() => new("GR-1", "Org", Today.AddDays(-30));

    private static Task<HttpResponseMessage> CloseAsync(HttpClient client, int grantId) =>
        client.PutAsync($"/api/grants/{grantId}/close", content: null);
}
