using Theseus.Core.Domain;
using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>When a start request no longer applies (sessions-run-the-project item 15), and when it still does.</summary>
[TestFixture]
public class StartRequestLapseTests
{
    private static readonly DateTimeOffset Nine = new(2026, 11, 23, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void ACardStillPausedByTheOwner_InTheRequestersProject_StillApplies() =>
        Assert.That(StartRequestLapse.Why(Paused(), false, Card("lead", "theseus")), Is.Null);

    [Test]
    public void EachLapse_SaysWhy()
    {
        Assert.Multiple(() =>
        {
            Assert.That(StartRequestLapse.Why(null, false, Card("lead", "theseus")), Is.EqualTo("it is no longer on the board"));
            Assert.That(StartRequestLapse.Why(Paused() with { IsArchived = true }, false, Card("lead", "theseus")), Is.EqualTo("it was archived"));
            Assert.That(StartRequestLapse.Why(Paused() with { IsPaused = false }, false, Card("lead", "theseus")),
                Is.EqualTo("the owner started or resumed it"));
            Assert.That(StartRequestLapse.Why(Paused(), true, Card("lead", "theseus")), Is.EqualTo("the owner started or resumed it"));
            Assert.That(StartRequestLapse.Why(Paused() with { PausedBy = "lead" }, false, Card("lead", "theseus")),
                Is.EqualTo("the owner started or resumed it"), "his pause was replaced");
            Assert.That(StartRequestLapse.Why(Paused() with { Project = null }, false, Card("lead", "theseus")),
                Is.EqualTo("it is not in your project any more"));
            Assert.That(StartRequestLapse.Why(Paused(), false, Card("lead", "another")), Is.EqualTo("it is not in your project any more"));
            Assert.That(StartRequestLapse.Why(Paused(), false, null), Is.EqualTo("it is not in your project any more"));
        });
    }

    private static Workstream Paused() => Card("paused-card", "theseus") with { IsPaused = true };

    private static Workstream Card(string key, string? project) =>
        new(key, key, "#208DD7", false, Path.GetTempPath(), [], "Carry on.", 0, "New", false, false, false, null, [], null, Nine)
        {
            Project = project,
        };
}
