using Theseus.Core.Duties;

namespace Theseus.Tests.Duties;

/// <summary>What a run tells the card: which duty and run, the instruction, what Theseus recorded since, and the brief way out.</summary>
[TestFixture]
public class DutyPromptTests
{
    private static readonly DateTimeOffset SetAt = new(2026, 11, 24, 21, 30, 0, TimeSpan.Zero);

    private static Duty Duty(DutyRunState state) =>
        new("d-00000001", "card", "Re-triage", "re-run your brief", DutyCadence.Every(TimeSpan.FromHours(2)), DutyEnd.Never, null, SetAt, state);

    [Test]
    public void TheFirstRun_SaysSo_AndThatNothingIsRecorded()
    {
        var text = DutyPrompt.For(Duty(new DutyRunState(SetAt)), [], 0, TimeZoneInfo.Utc);

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.StartWith("Duty \"Re-triage\" (every 2 h; run 1; first run). re-run your brief"));
            Assert.That(text, Does.Contain("as far as Theseus knows: nothing recorded."));
            Assert.That(text, Does.EndWith(DutyPrompt.NothingNewLine));
        });
    }

    [Test]
    public void ALaterRun_NamesTheLastRun_TheChanges_AndAnyCatchUp()
    {
        var state = new DutyRunState(SetAt) { RunCount = 13, LastRunUtc = SetAt, LastOutcome = DutyOutcome.Finished };

        var text = DutyPrompt.For(Duty(state), ["21:40 Spec accepted", "22:10 Report confirmed"], 2, TimeZoneInfo.Utc);

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.StartWith("Duty \"Re-triage\" (every 2 h; run 14; last run 21:30, finished; catching up: 2 runs missed)."));
            Assert.That(text, Does.Contain("- 21:40 Spec accepted"));
            Assert.That(text, Does.Contain("- 22:10 Report confirmed"));
        });
    }
}
