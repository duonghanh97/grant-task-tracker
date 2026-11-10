using GrantTracker.Domain.Common;
using GrantTracker.Domain.Grants;
using GrantTracker.Domain.Tasks;
using static GrantTracker.Domain.Tests.TestData;

namespace GrantTracker.Domain.Tests.Grants;

public class AddTaskTests
{
    // ---- BR-1: a task cannot be added to a closed grant ----

    [Test]
    public void AddingTaskToOpenGrantSucceeds()
    {
        var grant = OpenGrant();
        var versionBefore = grant.Version;

        var task = grant.AddTask("Verify bank details", Today.AddDays(5), TaskPriority.High, Today);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(grant.Tasks, Is.EquivalentTo(new[] { task }));
            Assert.That(task.Title, Is.EqualTo("Verify bank details"));
            Assert.That(task.DueDate, Is.EqualTo(Today.AddDays(5)));
            Assert.That(task.Priority, Is.EqualTo(TaskPriority.High));
            Assert.That(task.CompletedDate, Is.Null);
            Assert.That(task.Grant, Is.SameAs(grant));
            Assert.That(grant.Version, Is.Not.EqualTo(versionBefore), "Adding a task must change Version.");
        }
    }

    // ---- BR-2: due date not in the past and not before the application date ----

    [Test]
    public void DueDateInThePastFails()
    {
        var grant = OpenGrant();

        var ex = Assert.Throws<DomainException>(
            () => grant.AddTask("Task", Today.AddDays(-1), TaskPriority.Low, Today));

        AssertValidationError(ex, nameof(GrantTask.DueDate), "Due date cannot be in the past.");
        Assert.That(grant.Tasks, Is.Empty);
    }

    [Test]
    public void DueDateBeforeApplicationDateFails()
    {
        // The application date is in the future, so only the second half of BR-2 applies.
        var grant = new Grant("GR-1", "Org", Today.AddDays(10));

        var ex = Assert.Throws<DomainException>(
            () => grant.AddTask("Task", Today.AddDays(5), TaskPriority.Low, Today));

        AssertValidationError(ex, nameof(GrantTask.DueDate),
            "Due date cannot be before the grant's application date (2026-11-18).");
    }

    [Test]
    public void DueDateTodaySucceeds()
    {
        var grant = OpenGrant();

        var task = grant.AddTask("Task", Today, TaskPriority.Low, Today);

        Assert.That(task.DueDate, Is.EqualTo(Today));
    }

    [Test]
    public void DueDateOnApplicationDateSucceeds()
    {
        var grant = new Grant("GR-1", "Org", Today);

        var task = grant.AddTask("Task", Today, TaskPriority.Low, Today);

        Assert.That(task.DueDate, Is.EqualTo(grant.ApplicationDate));
    }

    [Test]
    public void FutureDueDateSucceeds()
    {
        var grant = OpenGrant();

        var task = grant.AddTask("Task", Today.AddDays(30), TaskPriority.Low, Today);

        Assert.That(task.DueDate, Is.EqualTo(Today.AddDays(30)));
    }

    // ---- BR-3: title required, at most 100 characters ----

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("\t\n")]
    public void MissingOrBlankTitleFails(string? title)
    {
        var grant = OpenGrant();

        var ex = Assert.Throws<DomainException>(
            () => grant.AddTask(title, Today.AddDays(1), TaskPriority.Low, Today));

        AssertValidationError(ex, nameof(GrantTask.Title), "Title is required.");
    }

    [Test]
    public void TitleOf100CharactersSucceeds()
    {
        var grant = OpenGrant();
        var title = new string('a', 100);

        var task = grant.AddTask(title, Today.AddDays(1), TaskPriority.Low, Today);

        Assert.That(task.Title, Has.Length.EqualTo(100));
    }

    [Test]
    public void TitleOf101CharactersFails()
    {
        var grant = OpenGrant();

        var ex = Assert.Throws<DomainException>(
            () => grant.AddTask(new string('a', 101), Today.AddDays(1), TaskPriority.Low, Today));

        AssertValidationError(ex, nameof(GrantTask.Title), "Title must be 100 characters or fewer.");
    }

    [Test]
    public void TitleIsTrimmedBeforeLengthCheck()
    {
        var grant = OpenGrant();
        var title = "  " + new string('a', 100) + "  ";

        var task = grant.AddTask(title, Today.AddDays(1), TaskPriority.Low, Today);

        Assert.That(task.Title, Is.EqualTo(new string('a', 100)));
    }

    [Test]
    public void AllInvalidFieldsAreReportedTogether()
    {
        var grant = OpenGrant();

        var ex = Assert.Throws<DomainException>(
            () => grant.AddTask("", Today.AddDays(-1), (TaskPriority)42, Today));

        Assert.That(ex.FieldErrors.Keys, Is.EquivalentTo(new[] { "Title", "DueDate", "Priority" }));
    }

    private static void AssertValidationError(DomainException ex, string field, string message)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex.Kind, Is.EqualTo(DomainErrorKind.Validation));
            Assert.That(ex.Code, Is.EqualTo(ErrorCodes.ValidationFailed));
            Assert.That(ex.FieldErrors.Keys, Is.EquivalentTo(new[] { field }));
            Assert.That(ex.FieldErrors[field], Does.Contain(message));
        }
    }
}
