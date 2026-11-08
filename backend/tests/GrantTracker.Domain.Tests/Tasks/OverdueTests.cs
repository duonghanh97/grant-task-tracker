using GrantTracker.Domain.Tasks;
using static GrantTracker.Domain.Tests.TestData;

namespace GrantTracker.Domain.Tests.Tasks;

// BR-6: a task is overdue when it is not completed and its due date is before today.
public class OverdueTests
{
    [TestCase(-1, true, TestName = "Pending task due yesterday is overdue")]
    [TestCase(-30, true, TestName = "Pending task due a month ago is overdue")]
    [TestCase(0, false, TestName = "Pending task due today is not overdue")]
    [TestCase(1, false, TestName = "Pending task due tomorrow is not overdue")]
    public void PendingTask(int dueInDays, bool expected)
    {
        var task = PendingTaskDue(dueInDays);

        AssertOverdue(task, expected);
    }

    [Test]
    public void CompletedTaskPastItsDueDateIsNotOverdue()
    {
        var created = Today.AddDays(-10);
        var grant = OpenGrant(applicationDaysAgo: 30);
        var task = grant.AddTask("Task", Today.AddDays(-5), TaskPriority.High, created);
        grant.CompleteTask(task, Today.AddDays(-1));

        AssertOverdue(task, expected: false);
    }

    [Test]
    public void TaskBecomesOverdueTheDayAfterItsDueDate()
    {
        var task = PendingTaskDue(0);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(task.IsOverdue(Today), Is.False);
            Assert.That(task.IsOverdue(Today.AddDays(1)), Is.True);
        }
    }

    // Tasks are created as of an earlier date so their due dates can be in the past.
    private static GrantTask PendingTaskDue(int dueInDays)
    {
        var created = Today.AddDays(-60);
        var grant = OpenGrant(applicationDaysAgo: 90);
        return grant.AddTask("Task", Today.AddDays(dueInDays), TaskPriority.Low, created);
    }

    // The method and the query expression must always agree.
    private static void AssertOverdue(GrantTask task, bool expected)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(task.IsOverdue(Today), Is.EqualTo(expected), "IsOverdue");
            Assert.That(GrantTask.OverdueOn(Today).Compile()(task), Is.EqualTo(expected), "OverdueOn");
        }
    }
}
