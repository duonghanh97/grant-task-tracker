using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace GrantTracker.Api.IntegrationTests.Infrastructure;

internal static class HttpAssertions
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response, HttpStatusCode expected)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.That(response.StatusCode, Is.EqualTo(expected), body);
        return JsonSerializer.Deserialize<T>(body, Json)
            ?? throw new AssertionException("Response body was empty.");
    }

    /// <summary>Asserts a ProblemDetails response and returns it for further checks.</summary>
    public static async Task<JsonObject> ReadProblemAsync(
        this HttpResponseMessage response, HttpStatusCode expected, string expectedCode)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.That(response.StatusCode, Is.EqualTo(expected), body);
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"), body);

        var problem = JsonNode.Parse(body)!.AsObject();
        using (Assert.EnterMultipleScope())
        {
            Assert.That((int?)problem["status"], Is.EqualTo((int)expected), body);
            Assert.That((string?)problem["code"], Is.EqualTo(expectedCode), body);
            Assert.That((string?)problem["traceId"], Is.Not.Null.And.Not.Empty, body);
        }

        return problem;
    }

    public static string[] FieldErrors(this JsonObject problem, string field) =>
        problem["errors"]?[field]?.AsArray().Select(e => (string)e!).ToArray() ?? [];
}
