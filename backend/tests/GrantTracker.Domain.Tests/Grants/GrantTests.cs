using GrantTracker.Domain.Grants;

namespace GrantTracker.Domain.Tests.Grants;

public class GrantTests
{
    [Test]
    public void NewGrantIsOpenWithNoTasks()
    {
        var grant = new Grant(" GR-100001 ", " Riverside ", TestData.Today);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(grant.Status, Is.EqualTo(GrantStatus.Open));
            Assert.That(grant.Tasks, Is.Empty);
            Assert.That(grant.PendingTaskCount, Is.Zero);
            Assert.That(grant.ReferenceNumber, Is.EqualTo("GR-100001"));
            Assert.That(grant.OrganisationName, Is.EqualTo("Riverside"));
            Assert.That(grant.Version, Is.Not.EqualTo(Guid.Empty));
        }
    }

    [TestCase("", "Org")]
    [TestCase("   ", "Org")]
    [TestCase("GR-1", "")]
    [TestCase("GR-1", "  ")]
    public void BlankReferenceOrOrganisationIsRejected(string reference, string organisation)
    {
        Assert.That(
            () => new Grant(reference, organisation, TestData.Today),
            Throws.InstanceOf<ArgumentException>());
    }
}
