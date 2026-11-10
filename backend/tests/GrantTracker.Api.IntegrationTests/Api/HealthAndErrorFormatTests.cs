using System.Net;
using GrantTracker.Api.IntegrationTests.Infrastructure;
using GrantTracker.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace GrantTracker.Api.IntegrationTests.Api;

public class HealthAndErrorFormatTests
{
    [Test]
    public async Task LiveAndReadyAreHealthy()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();

        var live = await client.GetAsync("/health/live");
        var ready = await client.GetAsync("/health/ready");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(live.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(ready.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }
    }

    [Test]
    public async Task ReadyReportsUnavailableWhenTheSchemaIsMissing()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        await using (var db = factory.CreateDbContext())
        {
            await db.Database.ExecuteSqlRawAsync("DROP TABLE IdempotencyRecords; DROP TABLE Tasks; DROP TABLE Grants;");
        }

        var live = await client.GetAsync("/health/live");
        var ready = await client.GetAsync("/health/ready");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(live.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Liveness must not depend on the database.");
            Assert.That(ready.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        }
    }

    [Test]
    public async Task UnknownRouteReturnsProblemDetails()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();

        var response = await client.GetAsync("/api/does-not-exist");

        await response.ReadProblemAsync(HttpStatusCode.NotFound, ErrorCodes.NotFound);
    }
}
