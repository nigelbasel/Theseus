using Theseus.Core.Launching;
using Theseus.Tests.Projects;

namespace Theseus.Tests.Launching;

/// <summary>
/// A start with an instruction (1.1e AC 15): the instruction ends the opening prompt, after the standing and every-start
/// prompts, fresh or resumed, and is not kept for later starts.
/// </summary>
[TestFixture]
public class StartInstructionTests
{
    private const string Handoff = @"C:\notes\HANDOFF-a.md";

    [Test]
    public void AFreshOpening_EndsWithTheInstruction_AfterTheEveryStartPrompt()
    {
        var workstream = ProjectCards.Card("a") with { EveryStartPrompt = "Every start.", StartInstruction = "More work: add the chips." };

        var opening = SessionOpening.For("Standing.", null, workstream, hasHistory: true, Handoff, handoffExists: true);

        Assert.That(opening, Does.EndWith("Every start." + Environment.NewLine + Environment.NewLine + "More work: add the chips."));
        Assert.That(opening.IndexOf("Standing.", StringComparison.Ordinal), Is.LessThan(opening.IndexOf("Every start.", StringComparison.Ordinal)));
    }

    [Test]
    public void AResumedOpening_EndsWithTheInstructionToo()
    {
        var workstream = ProjectCards.Card("a") with { StartInstruction = "More work: add the chips." };

        Assert.That(SessionOpening.ForResume(workstream, DateTimeOffset.UtcNow, Handoff), Does.EndWith("More work: add the chips."));
    }

    [Test]
    public void WithoutAnInstruction_TheOpeningIsAsBefore()
    {
        var plain = ProjectCards.Card("a") with { EveryStartPrompt = "Every start." };

        Assert.That(SessionOpening.For(null, null, plain, true, Handoff, true), Does.EndWith("Every start."));
        Assert.That(plain.StartInstruction, Is.Null, "nothing stored carries one: a later start opens without it");
    }
}
