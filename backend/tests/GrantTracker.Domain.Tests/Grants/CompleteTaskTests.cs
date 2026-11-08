using GrantTracker.Domain.Tasks;
using static GrantTracker.Domain.Tests.TestData;

namespace GrantTracker.Domain.Tests.Grants;

// BR-4: completing a task records its completion date; repeating it keeps that date.
public class CompleteTaskTests
{
    [Test]
    public void FirstCompletionRecordsTodayAndChangesVersion()
    {
        var grant = OpenGrant();
        var task = grant.AddTask("Task", Today.AddDays(3), TaskPriority.Medium, Today);
        var versionBefore = grant.Version;

        var changed = grant.CompleteTask(task, Today);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changed, Is.True);
            Assert.That(task.CompletedDate, Is.EqualTo(Today));
            Assert.That(task.IsCompleted, Is.True);
            Assert.That(grant.PendingTaskCount, Is.Zero);
            Assert.That(grant.Version, Is.Not.EqualTo(versionBefore));
        }
    }

    [Test]
    public void RepeatedCompletionKeepsOriginalDateAndVersion()
    {
        var grant = OpenGrant();
        var task = grant.AddTask("Task", Today.AddDays(3), TaskPriority.Medium, Today);
        grant.CompleteTask(task, Today);
        var versionAfterFirst = grant.Version;

        var changed = grant.CompleteTask(task, Today.AddDays(2));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changed, Is.False);
            Assert.That(task.CompletedDate, Is.EqualTo(Today));
            Assert.That(grant.Version, Is.EqualTo(versionAfterFirst), "A no-op must not change Version.");
        }
    }

    [Test]
    public void TaskFromAnotherGrantIsRejected()
    {
        var grant = OpenGrant();
        var otherTask = OpenGrant().AddTask("Task", Today, TaskPriority.Low, Today);

        Assert.That(() => grant.CompleteTask(otherTask, Today), Throws.ArgumentException);
    }
}
