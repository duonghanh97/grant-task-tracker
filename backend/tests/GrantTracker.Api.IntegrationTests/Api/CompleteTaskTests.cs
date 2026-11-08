using System.Net;
using GrantTracker.Api.Features.Grants;
using GrantTracker.Api.Features.Tasks;
using GrantTracker.Api.IntegrationTests.Infrastructure;
using GrantTracker.Domain.Common;
using GrantTracker.Domain.Grants;
using GrantTracker.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace GrantTracker.Api.IntegrationTests.Api;

// BR-4 and OP-3: completion records a date once; repeating it is safe.
public class CompleteTaskTests
{
    private static readonly DateOnly Today = ApiFactory.Today;

    private ApiFactory _factory = null!;
    private HttpClient _client = null!;
    private Grant _grant = null!;
    private GrantTask _overdueTask = null!;

    [SetUp]
    public async Task SetUp()
    {
        _factory = new ApiFactory();
        _grant = new Grant("GR-1", "Org", Today.AddDays(-30));
        _overdueTask = _grant.AddTask("Verify bank details", Today.AddDays(-3), TaskPriority.High, Today.AddDays(-30));
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
    public async Task CompletingAPendingTaskRecordsToday()
    {
        var task = await (await CompleteAsync(_overdueTask.Id)).ReadAsync<TaskDto>(HttpStatusCode.OK);
        var grant = await (await _client.GetAsync($"/api/grants/{_grant.Id}")).ReadAsync<GrantDetailDto>(HttpStatusCode.OK);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(task.CompletedDate, Is.EqualTo(Today));
            Assert.That(task.IsOverdue, Is.False, "A completed task is never overdue.");
            Assert.That(grant.PendingTaskCount, Is.Zero);
            Assert.That(grant.Tasks.Single().CompletedDate, Is.EqualTo(Today));
        }
    }

    [Test]
    public async Task RepeatingCompletionOnALaterDayKeepsTheOriginalDate()
    {
        await (await CompleteAsync(_overdueTask.Id)).ReadAsync<TaskDto>(HttpStatusCode.OK);
        var versionAfterFirst = await GrantVersionAsync();
        _factory.Time.Advance(TimeSpan.FromDays(2));

        var repeated = await (await CompleteAsync(_overdueTask.Id)).ReadAsync<TaskDto>(HttpStatusCode.OK);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(repeated.CompletedDate, Is.EqualTo(Today));
            Assert.That(await GrantVersionAsync(), Is.EqualTo(versionAfterFirst), "A repeated completion must not write.");
        }
    }

    [Test]
    public async Task UnknownTaskReturnsNotFound()
    {
        var problem = await (await CompleteAsync(999)).ReadProblemAsync(HttpStatusCode.NotFound, ErrorCodes.TaskNotFound);

        Assert.That((string?)problem["detail"], Is.EqualTo("Task 999 does not exist."));
    }

    private Task<HttpResponseMessage> CompleteAsync(int taskId) =>
        _client.PutAsync($"/api/tasks/{taskId}/complete", content: null);

    private async Task<Guid> GrantVersionAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.Grants.Where(g => g.Id == _grant.Id).Select(g => g.Version).SingleAsync();
    }
}
