using System.Net;
using GrantTracker.Api.Features.Grants;
using GrantTracker.Api.IntegrationTests.Infrastructure;
using GrantTracker.Domain.Common;
using GrantTracker.Domain.Grants;
using GrantTracker.Domain.Tasks;

namespace GrantTracker.Api.IntegrationTests.Api;

public class GrantQueryTests
{
    private static readonly DateOnly Today = ApiFactory.Today;

    [Test]
    public async Task ListsAllGrantsWithPendingTaskCounts()
    {
        await using var factory = new ApiFactory { Seed = true };
        using var client = factory.CreateApiClient();

        var grants = await (await client.GetAsync("/api/grants"))
            .ReadAsync<GrantSummaryDto[]>(HttpStatusCode.OK);

        Assert.That(
            grants.Select(g => (g.ReferenceNumber, g.Status, g.PendingTaskCount)),
            Is.EqualTo(new[]
            {
                ("GR-100001", GrantStatus.Open, 2),
                ("GR-100002", GrantStatus.Open, 2),
                ("GR-100003", GrantStatus.Closed, 0),
                ("GR-100004", GrantStatus.Open, 0),
            }));
    }

    [TestCase("Open", new[] { "GR-100001", "GR-100002", "GR-100004" })]
    [TestCase("closed", new[] { "GR-100003" })]
    [TestCase("", new[] { "GR-100001", "GR-100002", "GR-100003", "GR-100004" })]
    public async Task FiltersByStatus(string status, string[] expected)
    {
        await using var factory = new ApiFactory { Seed = true };
        using var client = factory.CreateApiClient();

        var grants = await (await client.GetAsync($"/api/grants?status={status}"))
            .ReadAsync<GrantSummaryDto[]>(HttpStatusCode.OK);

        Assert.That(grants.Select(g => g.ReferenceNumber), Is.EqualTo(expected));
    }

    [TestCase("Pending")]
    [TestCase("1")]
    public async Task UnknownStatusIsRejected(string status)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();

        var problem = await (await client.GetAsync($"/api/grants?status={status}"))
            .ReadProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationFailed);

        Assert.That(problem.FieldErrors("status"), Is.EqualTo(new[] { "Status must be Open or Closed." }));
    }

    [Test]
    public async Task GetsAGrantWithItsTasksAndOverdueFlags()
    {
        await using var factory = new ApiFactory { Seed = true };
        using var client = factory.CreateApiClient();
        var summaries = await (await client.GetAsync("/api/grants")).ReadAsync<GrantSummaryDto[]>(HttpStatusCode.OK);
        var riverside = summaries.Single(g => g.ReferenceNumber == "GR-100001");

        var grant = await (await client.GetAsync($"/api/grants/{riverside.Id}"))
            .ReadAsync<GrantDetailDto>(HttpStatusCode.OK);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(grant.OrganisationName, Is.EqualTo("Riverside Community Garden Inc."));
            Assert.That(grant.ApplicationDate, Is.EqualTo(Today.AddDays(-60)));
            Assert.That(grant.PendingTaskCount, Is.EqualTo(2));
            Assert.That(
                grant.Tasks.Select(t => (t.Title, t.DueDate, t.Priority, t.CompletedDate, t.IsOverdue)),
                Is.EqualTo(new[]
                {
                    ("Check eligibility documents", Today.AddDays(-50), TaskPriority.High, (DateOnly?)Today.AddDays(-50), false),
                    ("Verify bank details", Today.AddDays(-3), TaskPriority.High, (DateOnly?)null, true),
                    ("Request quote for tools", Today.AddDays(5), TaskPriority.Medium, (DateOnly?)null, false),
                }),
                "Tasks are ordered by due date; only the pending past-due task is overdue.");
        }
    }

    [Test]
    public async Task UnknownGrantReturnsNotFound()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();

        var problem = await (await client.GetAsync("/api/grants/999"))
            .ReadProblemAsync(HttpStatusCode.NotFound, ErrorCodes.GrantNotFound);

        Assert.That((string?)problem["detail"], Is.EqualTo("Grant 999 does not exist."));
    }

    // D6: at 2026-11-08 20:00 UTC it is already 9 November at +7 but still 8 November at -8.
    [TestCase("+7", true)]
    [TestCase("-8", false)]
    public async Task OverdueFlagUsesTheConfiguredOffset(string utcOffset, bool expectedOverdue)
    {
        await using var factory = new ApiFactory { UtcOffset = utcOffset };
        factory.Time.SetUtcNow(new DateTimeOffset(2026, 11, 8, 20, 0, 0, TimeSpan.Zero));
        var grant = new Grant("GR-1", "Org", Today.AddDays(-30));
        grant.AddTask("Due 8 November", new DateOnly(2026, 11, 8), TaskPriority.Low, Today.AddDays(-30));
        await factory.ArrangeAsync(db => db.Grants.Add(grant));
        using var client = factory.CreateApiClient();

        var detail = await (await client.GetAsync($"/api/grants/{grant.Id}"))
            .ReadAsync<GrantDetailDto>(HttpStatusCode.OK);

        Assert.That(detail.Tasks.Single().IsOverdue, Is.EqualTo(expectedOverdue));
    }
}
