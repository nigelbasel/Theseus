using Theseus.Core.Domain;

namespace Theseus.Tests.Domain;

[TestFixture]
public class WorkstreamKeyTests
{
    [Test]
    public void FromTitle_MakesAKebabCaseKey()
    {
        Assert.That(WorkstreamKey.FromTitle("Contact form validation"), Is.EqualTo("contact-form-validation"));
    }

    [Test]
    public void FromTitle_DropsPunctuationRatherThanPuttingItInAFilename()
    {
        Assert.That(WorkstreamKey.FromTitle("Search → Index (phase 2)"), Is.EqualTo("search-index-phase-2"));
    }

    [Test]
    public void FromTitle_CollapsesRunsOfSeparators()
    {
        Assert.That(WorkstreamKey.FromTitle("legacy   job / migration"), Is.EqualTo("legacy-job-migration"));
    }

    [Test]
    public void FromTitle_DoesNotLeaveALeadingOrTrailingSeparator()
    {
        Assert.That(WorkstreamKey.FromTitle("  -- Admin console --  "), Is.EqualTo("admin-console"));
    }

    [Test]
    public void FromTitle_KeepsDigits()
    {
        Assert.That(WorkstreamKey.FromTitle("PROJ-101 forms"), Is.EqualTo("proj-101-forms"));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    [TestCase("→→→")]
    public void FromTitle_FallsBackToAUsableNameRatherThanAnEmptyFilename(string? title)
    {
        Assert.That(WorkstreamKey.FromTitle(title), Is.EqualTo("workstream"));
    }

    [Test]
    public void Unique_KeepsTheKeyWhenNothingHasTakenIt()
    {
        Assert.That(WorkstreamKey.Unique("Tailhawk", []), Is.EqualTo("tailhawk"));
    }

    [Test]
    public void Unique_SuffixesRatherThanOverwritingAnExistingPlan()
    {
        Assert.That(WorkstreamKey.Unique("Tailhawk", ["tailhawk"]), Is.EqualTo("tailhawk-2"));
    }

    [Test]
    public void Unique_KeepsCountingPastTheFirstCollision()
    {
        Assert.That(WorkstreamKey.Unique("Tailhawk", ["tailhawk", "tailhawk-2", "tailhawk-3"]),
            Is.EqualTo("tailhawk-4"));
    }

    [Test]
    public void Unique_MatchesExistingKeysCaseInsensitivelyLikeTheFileSystemDoes()
    {
        Assert.That(WorkstreamKey.Unique("Tailhawk", ["TAILHAWK"]), Is.EqualTo("tailhawk-2"));
    }
}
