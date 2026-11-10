using GrantTracker.Domain.Common;
using GrantTracker.Domain.Grants;
using GrantTracker.Domain.Tasks;
using static GrantTracker.Domain.Tests.TestData;

namespace GrantTracker.Domain.Tests.Grants;

// BR-5: a grant cannot be closed while it has any pending High-priority task.
public class CloseGrantTests
{
    [Test]
    public void GrantWithNoTasksCloses()
    {
        var grant = OpenGrant();
        var versionBefore = grant.Version;

        var changed = grant.Close();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changed, Is.True);
            Assert.That(grant.Status, Is.EqualTo(GrantStatus.Closed));
            Assert.That(grant.Version, Is.Not.EqualTo(versionBefore));
        }
    }

    [Test]
    public void PendingHighPriorityTaskBlocksClosure()
    {
        var grant = OpenGrant();
        grant.AddTask("Verify bank details", Today.AddDays(1), TaskPriority.High, Today);
        var versionBefore = grant.Version;

        var ex = Assert.Throws<DomainException>(() => grant.Close());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex.Kind, Is.EqualTo(DomainErrorKind.Conflict));
            Assert.That(ex.Code, Is.EqualTo(ErrorCodes.GrantHasPendingHighPriorityTasks));
            Assert.That(ex.Message, Is.EqualTo("Grant GR-100001 has 1 pending High-priority task."));
            Assert.That(grant.Status, Is.EqualTo(GrantStatus.Open));
            Assert.That(grant.Version, Is.EqualTo(versionBefore));
        }
    }

    [Test]
    public void MessageCountsEveryPendingHighPriorityTask()
    {
        var grant = OpenGrant();
        grant.AddTask("A", Today.AddDays(1), TaskPriority.High, Today);
        grant.AddTask("B", Today.AddDays(2), TaskPriority.High, Today);

        var ex = Assert.Throws<DomainException>(() => grant.Close());

        Assert.That(ex.Message, Is.EqualTo("Grant GR-100001 has 2 pending High-priority tasks."));
    }

    [Test]
    public void CompletedHighPriorityTaskDoesNotBlockClosure()
    {
        var grant = OpenGrant();
        var task = grant.AddTask("Check eligibility", Today.AddDays(1), TaskPriority.High, Today);
        grant.CompleteTask(task, Today);

        Assert.That(grant.Close(), Is.True);
    }

    [TestCase(TaskPriority.Low)]
    [TestCase(TaskPriority.Medium)]
    public void PendingLowerPriorityTaskDoesNotBlockClosure(TaskPriority priority)
    {
        var grant = OpenGrant();
        grant.AddTask("Task", Today.AddDays(1), priority, Today);

        Assert.That(grant.Close(), Is.True);
    }

    [Test]
    public void ClosingClosedGrantChangesNothing()
    {
        var grant = OpenGrant();
        grant.Close();
        var versionAfterFirst = grant.Version;

        var changed = grant.Close();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changed, Is.False);
            Assert.That(grant.Status, Is.EqualTo(GrantStatus.Closed));
            Assert.That(grant.Version, Is.EqualTo(versionAfterFirst));
        }
    }

    // BR-1, tested here because it needs a closed grant.
    [Test]
    public void AddingTaskToClosedGrantFails()
    {
        var grant = OpenGrant();
        grant.Close();

        var ex = Assert.Throws<DomainException>(
            () => grant.AddTask("Late task", Today.AddDays(5), TaskPriority.Low, Today));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex.Kind, Is.EqualTo(DomainErrorKind.Conflict));
            Assert.That(ex.Code, Is.EqualTo(ErrorCodes.GrantClosed));
            Assert.That(grant.Tasks, Is.Empty);
        }
    }
}
