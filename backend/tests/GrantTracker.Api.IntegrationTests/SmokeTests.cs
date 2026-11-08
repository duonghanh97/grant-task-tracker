using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GrantTracker.Api.IntegrationTests;

public class SmokeTests
{
    [Test]
    public async Task LiveHealthCheckResponds()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}
