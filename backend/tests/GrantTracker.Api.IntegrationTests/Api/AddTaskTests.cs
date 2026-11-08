using System.Net;
using System.Net.Http.Json;
using System.Text;
using GrantTracker.Api.Features.Grants;
using GrantTracker.Api.Features.Tasks;
using GrantTracker.Api.IntegrationTests.Infrastructure;
using GrantTracker.Domain.Common;
using GrantTracker.Domain.Grants;
using GrantTracker.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace GrantTracker.Api.IntegrationTests.Api;

public class AddTaskTests
{
    private static readonly DateOnly Today = ApiFactory.Today;

    private ApiFactory _factory = null!;
    private HttpClient _client = null!;
    private Grant _grant = null!;

    [SetUp]
    public async Task SetUp()
    {
        _factory = new ApiFactory();
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
    public async Task AddingTaskToOpenGrantReturnsCreatedTask()
    {
        var response = await PostTaskAsync(new { title = "  Verify bank details  ", dueDate = Today.AddDays(5), priority = "High" });

        var task = await response.ReadAsync<TaskDto>(HttpStatusCode.Created);
        var grant = await (await _client.GetAsync($"/api/grants/{_grant.Id}")).ReadAsync<GrantDetailDto>(HttpStatusCode.OK);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(task.Id, Is.Positive);
            Assert.That(task.GrantId, Is.EqualTo(_grant.Id));
            Assert.That(task.Title, Is.EqualTo("Verify bank details"));
            Assert.That(task.DueDate, Is.EqualTo(Today.AddDays(5)));
            Assert.That(task.Priority, Is.EqualTo(TaskPriority.High));
            Assert.That(task.CompletedDate, Is.Null);
            Assert.That(task.IsOverdue, Is.False);
            Assert.That(grant.Tasks.Select(t => t.Id), Is.EqualTo(new[] { task.Id }));
            Assert.That(grant.PendingTaskCount, Is.EqualTo(1));
        }
    }

    // BR-1
    [Test]
    public async Task AddingTaskToClosedGrantIsAConflict()
    {
        await CloseGrantDirectlyAsync();

        var response = await PostTaskAsync(new { title = "Late", dueDate = Today.AddDays(5), priority = "Low" });

        var problem = await response.ReadProblemAsync(HttpStatusCode.Conflict, ErrorCodes.GrantClosed);
        Assert.That((string?)problem["detail"], Is.EqualTo("Grant GR-1 is closed, so tasks cannot be added to it."));
        Assert.That(await CountTasksAsync(), Is.Zero);
    }

    // BR-2
    [Test]
    public async Task PastDueDateIsRejected()
    {
        var response = await PostTaskAsync(new { title = "Task", dueDate = Today.AddDays(-1), priority = "Low" });

        var problem = await response.ReadProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationFailed);
        Assert.That(problem.FieldErrors("dueDate"), Is.EqualTo(new[] { "Due date cannot be in the past." }));
        Assert.That(await CountTasksAsync(), Is.Zero);
    }

    // BR-2
    [Test]
    public async Task DueDateBeforeApplicationDateIsRejected()
    {
        var future = new Grant("GR-2", "Future", Today.AddDays(10));
        await _factory.ArrangeAsync(db => db.Grants.Add(future));

        var response = await _client.PostAsJsonAsync(
            $"/api/grants/{future.Id}/tasks", new { title = "Task", dueDate = Today.AddDays(5), priority = "Low" });

        var problem = await response.ReadProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationFailed);
        Assert.That(problem.FieldErrors("dueDate"),
            Is.EqualTo(new[] { "Due date cannot be before the grant's application date (2026-11-18)." }));
    }

    // BR-2
    [Test]
    public async Task DueDateTodayIsAccepted()
    {
        var response = await PostTaskAsync(new { title = "Task", dueDate = Today, priority = "Low" });

        var task = await response.ReadAsync<TaskDto>(HttpStatusCode.Created);
        Assert.That(task.DueDate, Is.EqualTo(Today));
    }

    // BR-3
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task MissingOrBlankTitleIsRejected(string? title)
    {
        var response = await PostTaskAsync(new { title, dueDate = Today.AddDays(1), priority = "Low" });

        var problem = await response.ReadProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationFailed);
        Assert.That(problem.FieldErrors("title"), Is.EqualTo(new[] { "Title is required." }));
    }

    // BR-3
    [Test]
    public async Task TitleOf100CharactersIsAccepted()
    {
        var response = await PostTaskAsync(new { title = new string('a', 100), dueDate = Today.AddDays(1), priority = "Low" });

        var task = await response.ReadAsync<TaskDto>(HttpStatusCode.Created);
        Assert.That(task.Title, Has.Length.EqualTo(100));
    }

    // BR-3
    [Test]
    public async Task TitleOf101CharactersIsRejected()
    {
        var response = await PostTaskAsync(new { title = new string('a', 101), dueDate = Today.AddDays(1), priority = "Low" });

        var problem = await response.ReadProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationFailed);
        Assert.That(problem.FieldErrors("title"), Is.EqualTo(new[] { "Title must be 100 characters or fewer." }));
    }

    [Test]
    public async Task MissingDueDateAndPriorityAreRejected()
    {
        var response = await PostTaskAsync(new { title = "Task" });

        var problem = await response.ReadProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationFailed);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(problem.FieldErrors("dueDate"), Is.EqualTo(new[] { "Due date is required." }));
            Assert.That(problem.FieldErrors("priority"), Is.EqualTo(new[] { "Priority is required." }));
        }
    }

    [TestCase("""{ "title": "Task", "dueDate": "2026-11-09", "priority": "Urgent" }""", TestName = "Unknown priority")]
    [TestCase("""{ "title": "Task", "dueDate": "2026-11-09", "priority": 2 }""", TestName = "Numeric priority")]
    [TestCase("""{ "title": "Task", "dueDate": "next week", "priority": "Low" }""", TestName = "Unparseable date")]
    [TestCase("""{ "title": """, TestName = "Truncated JSON")]
    public async Task MalformedBodyIsRejected(string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync($"/api/grants/{_grant.Id}/tasks", content);

        await response.ReadProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationFailed);
        Assert.That(await CountTasksAsync(), Is.Zero);
    }

    [Test]
    public async Task UnknownGrantReturnsNotFound()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/grants/999/tasks", new { title = "Task", dueDate = Today.AddDays(1), priority = "Low" });

        await response.ReadProblemAsync(HttpStatusCode.NotFound, ErrorCodes.GrantNotFound);
    }

    private Task<HttpResponseMessage> PostTaskAsync(object body) =>
        _client.PostAsJsonAsync($"/api/grants/{_grant.Id}/tasks", body);

    private async Task<int> CountTasksAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.Tasks.CountAsync();
    }

    private async Task CloseGrantDirectlyAsync()
    {
        await using var db = _factory.CreateDbContext();
        var grant = await db.Grants.Include(g => g.Tasks).SingleAsync(g => g.Id == _grant.Id);
        grant.Close();
        await db.SaveChangesAsync();
    }
}
