using Theseus.Core.Domain;
using Theseus.Core.Running;

namespace Theseus.Tests.Running;

[TestFixture]
public class RunModeChoiceTests
{
    private static readonly Dictionary<string, RunMode> NoDefaults = [];

    private static Workstream Card(RunMode? own = null, string? project = null, WorkstreamRole role = WorkstreamRole.None) =>
        new("card", "Card", "#208DD7", false, @"C:\work", [], "Carry on.", 0, "New", false, false, false, null, [], null,
            DateTimeOffset.UnixEpoch)
        {
            RunMode = own,
            Project = project,
            Role = role,
        };

    [Test]
    public void AnOrdinaryCard_RunsInATab()
    {
        Assert.That(RunModeChoice.For(Card(), NoDefaults), Is.EqualTo(RunMode.Tab));
    }

    [TestCase(WorkstreamRole.Verifier)]
    [TestCase(WorkstreamRole.ScenarioWriter)]
    public void VerifiersAndScenarioWriters_RunHeadless(WorkstreamRole role)
    {
        Assert.That(RunModeChoice.For(Card(role: role), NoDefaults), Is.EqualTo(RunMode.Headless));
    }

    [Test]
    public void TheProjectsDefault_ComesBeforeTheRole()
    {
        var modes = new Dictionary<string, RunMode> { ["proj"] = RunMode.Tab };

        Assert.That(RunModeChoice.For(Card(project: "proj", role: WorkstreamRole.Verifier), modes), Is.EqualTo(RunMode.Tab));
    }

    [Test]
    public void TheCardsOwnMode_ComesFirst()
    {
        var modes = new Dictionary<string, RunMode> { ["proj"] = RunMode.Headless };

        Assert.That(RunModeChoice.For(Card(RunMode.Tab, "proj"), modes), Is.EqualTo(RunMode.Tab));
    }
}
