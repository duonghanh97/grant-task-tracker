using System.Net;
using System.Net.Http.Json;
using GrantTracker.Api.Features.Tasks;
using GrantTracker.Api.IntegrationTests.Infrastructure;
using GrantTracker.Domain.Common;
using GrantTracker.Domain.Grants;
using Microsoft.EntityFrameworkCore;

namespace GrantTracker.Api.IntegrationTests.Api;

// OP-3: retrying task creation with the same Idempotency-Key never creates a duplicate.
public class IdempotencyTests
{
    private static readonly DateOnly Today = ApiFactory.Today;

    private ApiFactory _factory = null!;
    private InterleavingInterceptor _interceptor = null!;
    private HttpClient _client = null!;
    private Grant _grant = null!;

    [SetUp]
    public async Task SetUp()
    {
        _factory = InterleavingInterceptor.CreateFactory(out _interceptor);
        _grant = new Grant("GR-1", "Org", Today.AddDays(-30));
        await _factory.ArrangeAsync(db => db.Grants.Add(_grant));
        _client = _factory.CreateClient();
    }

    [TearDown]
    public async Task TearDown()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Test]
    public async Task RetryWithSameKeyAndBodyReplaysTheCreatedTask()
    {
        var body = Body("Verify bank details");

        var first = await PostAsync("key-1", body);
        var retry = await PostAsync("key-1", body);

        var created = await first.ReadAsync<TaskDto>(HttpStatusCode.Created);
        var replayed = await retry.ReadAsync<TaskDto>(HttpStatusCode.Created);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(replayed, Is.EqualTo(created));
            Assert.That(first.Headers.Contains("Idempotent-Replayed"), Is.False);
            Assert.That(retry.Headers.GetValues("Idempotent-Replayed"), Is.EqualTo(new[] { "true" }));
            Assert.That(await CountTasksAsync(), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task ReplayReturnsTheTasksCurrentState()
    {
        var created = await (await PostAsync("key-1", Body("Task"))).ReadAsync<TaskDto>(HttpStatusCode.Created);
        (await _client.PutAsync($"/api/tasks/{created.Id}/complete", null)).EnsureSuccessStatusCode();

        var replayed = await (await PostAsync("key-1", Body("Task"))).ReadAsync<TaskDto>(HttpStatusCode.Created);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(replayed.Id, Is.EqualTo(created.Id));
            Assert.That(replayed.CompletedDate, Is.EqualTo(Today));
        }
    }

    [Test]
    public async Task DifferentKeysCreateSeparateTasks()
    {
        await (await PostAsync("key-1", Body("Task"))).ReadAsync<TaskDto>(HttpStatusCode.Created);
        await (await PostAsync("key-2", Body("Task"))).ReadAsync<TaskDto>(HttpStatusCode.Created);

        Assert.That(await CountTasksAsync(), Is.EqualTo(2));
    }

    [Test]
    public async Task SameKeyWithDifferentBodyIsRejected()
    {
        await (await PostAsync("key-1", Body("Original"))).ReadAsync<TaskDto>(HttpStatusCode.Created);

        var response = await PostAsync("key-1", Body("Changed"));

        await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.IdempotencyKeyReused);
        Assert.That(await CountTasksAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task SameKeyAndBodyForAnotherGrantIsRejected()
    {
        var other = new Grant("GR-2", "Other", Today.AddDays(-30));
        await _factory.ArrangeAsync(db => db.Grants.Add(other));
        await (await PostAsync("key-1", Body("Task"))).ReadAsync<TaskDto>(HttpStatusCode.Created);

        var response = await PostAsync("key-1", Body("Task"), other.Id);

        await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.IdempotencyKeyReused);
    }

    [Test]
    public async Task FailedRequestIsNotRememberedSoTheKeyCanBeRetried()
    {
        var past = new { title = "Task", dueDate = Today.AddDays(-1), priority = "Low" };
        await (await PostAsync("key-1", past)).ReadProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationFailed);

        var retry = await PostAsync("key-1", Body("Task"));

        await retry.ReadAsync<TaskDto>(HttpStatusCode.Created);
    }

    [Test]
    public async Task KeyIsRememberedFor24Hours()
    {
        await (await PostAsync("key-1", Body("Task"))).ReadAsync<TaskDto>(HttpStatusCode.Created);

        _factory.Time.Advance(TimeSpan.FromHours(23));
        var withinRetention = await PostAsync("key-1", Body("Task"));

        Assert.That(withinRetention.Headers.Contains("Idempotent-Replayed"), Is.True);
        Assert.That(await CountTasksAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task ExpiredKeyIsTreatedAsNew()
    {
        var first = await (await PostAsync("key-1", Body("Task"))).ReadAsync<TaskDto>(HttpStatusCode.Created);

        _factory.Time.Advance(TimeSpan.FromHours(25));
        var response = await PostAsync("key-1", Body("Task"));
        var second = await response.ReadAsync<TaskDto>(HttpStatusCode.Created);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.Headers.Contains("Idempotent-Replayed"), Is.False);
            Assert.That(second.Id, Is.Not.EqualTo(first.Id));
            Assert.That(await CountTasksAsync(), Is.EqualTo(2));
        }
    }

    // The original request is still in flight (read done, write pending) when the
    // retry arrives and completes. The original must replay the retry's task.
    [Test]
    public async Task RacingIdenticalRequestsCreateOneTask()
    {
        HttpResponseMessage? retry = null;
        _interceptor.BeforeNextSave(async () => retry = await PostAsync("key-1", Body("Task")));

        var original = await PostAsync("key-1", Body("Task"));

        var originalTask = await original.ReadAsync<TaskDto>(HttpStatusCode.Created);
        var retryTask = await retry!.ReadAsync<TaskDto>(HttpStatusCode.Created);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(originalTask, Is.EqualTo(retryTask));
            Assert.That(original.Headers.Contains("Idempotent-Replayed"), Is.True);
            Assert.That(await CountTasksAsync(), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task MissingKeyIsRejected()
    {
        var response = await _client.PostAsJsonAsync($"/api/grants/{_grant.Id}/tasks", Body("Task"));

        await response.ReadProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.IdempotencyKeyMissing);
        Assert.That(await CountTasksAsync(), Is.Zero);
    }

    [Test]
    public async Task OverlongKeyIsRejected()
    {
        var response = await PostAsync(new string('k', 101), Body("Task"));

        await response.ReadProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.IdempotencyKeyInvalid);
    }

    private static object Body(string title) => new { title, dueDate = Today.AddDays(5), priority = "Medium" };

    private Task<HttpResponseMessage> PostAsync(string key, object body, int? grantId = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/grants/{grantId ?? _grant.Id}/tasks")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("Idempotency-Key", key);
        return _client.SendAsync(request);
    }

    private async Task<int> CountTasksAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.Tasks.CountAsync();
    }
}
