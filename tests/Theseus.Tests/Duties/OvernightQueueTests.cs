using Theseus.Core.Domain;
using Theseus.Core.Duties;
using Theseus.Core.Settings;

namespace Theseus.Tests.Duties;

/// <summary>
/// The overnight queue wakes an open, idle card of a project that has it on, with unfinished work and nothing waiting on the
/// owner, past the idle threshold; and never one it already woke whose piece has not moved since.
/// </summary>
[TestFixture]
public class OvernightQueueTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 21, 23, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 21);
    private static readonly DutySettings On = new() { QueueProjects = ["Theseus"] };

    private static QueueCandidate Ready() =>
        new(new DutyCard("card", "Card", SessionLifecycle.Idle, true), "Theseus")
        {
            ProjectRunning = true,
            HasUnfinishedPiece = true,
            StatusSinceUtc = Now.AddMinutes(-56),
            PieceTitle = "Projects in use",
            Status = "Build: PR 5",
        };

    private static string? Refusal(QueueCandidate candidate, QueueRecord? record = null, DutySettings? settings = null) =>
        OvernightQueue.Refusal(candidate, record, settings ?? On, Now, Today);

    [Test]
    public void AnIdleCardWithUnfinishedWork_IsResumed_WithAReason()
    {
        var chosen = OvernightQueue.Choose([Ready()], _ => null, On, Now, TimeZoneInfo.Utc).Single();

        Assert.Multiple(() =>
        {
            Assert.That(chosen.Card, Is.EqualTo("card"));
            Assert.That(chosen.Instruction, Does.Contain("your piece \"Projects in use\" is at Build: PR 5"));
            Assert.That(chosen.Instruction, Does.Contain("idle since 22:04"));
        });
    }

    [Test]
    public void TheQueueIsOff_UnlessTheOwnerSwitchedItOnForTheProject()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Refusal(Ready(), settings: new DutySettings()), Is.EqualTo("the queue is off for its project"));
            Assert.That(Refusal(Ready() with { Project = null }), Is.EqualTo("the queue is off for its project"));
        });
    }

    [Test]
    public void EachConditionHoldsACardBack()
    {
        var card = Ready().Card;

        Assert.Multiple(() =>
        {
            Assert.That(Refusal(Ready() with { ProjectRunning = false }), Is.Not.Null);
            Assert.That(Refusal(Ready() with { HasUnfinishedPiece = false }), Is.Not.Null);
            Assert.That(Refusal(Ready() with { WaitsForOwner = true }), Is.EqualTo("its piece waits for you"));
            Assert.That(Refusal(Ready() with { IsVerifier = true }), Is.Not.Null);
            Assert.That(Refusal(Ready() with { Card = card with { PausedByOwner = true } }), Is.Not.Null);
            Assert.That(Refusal(Ready() with { Card = card with { PausedByCard = "lead" } }), Is.Not.Null);
            Assert.That(Refusal(Ready() with { Card = card with { IsBlocked = true } }), Is.Not.Null);
            Assert.That(Refusal(Ready() with { Card = card with { IsArchived = true } }), Is.Not.Null);
            Assert.That(Refusal(Ready() with { Card = card with { StoppedByOwner = true } }), Is.EqualTo("you stopped it"));
            Assert.That(Refusal(Ready() with { Card = card with { IsStopping = true } }), Is.Not.Null);
            Assert.That(Refusal(Ready() with { Card = card with { IsAsking = true } }), Is.Not.Null);
            Assert.That(Refusal(Ready() with { Card = card with { Lifecycle = SessionLifecycle.Running } }), Is.Not.Null);
            Assert.That(Refusal(Ready() with { Card = card with { IsLive = false, Lifecycle = SessionLifecycle.Ended } }),
                Is.EqualTo("it is not open and idle"), "a stopped card is never started by the queue");
            Assert.That(Refusal(Ready() with { StatusSinceUtc = Now.AddMinutes(-10) }), Is.EqualTo("it has not been idle long enough"));
            Assert.That(Refusal(Ready() with { StatusSinceUtc = null }), Is.Not.Null);
        });
    }

    [Test]
    public void ACardTheQueueWoke_IsNotWokenAgain_UntilItsPieceMoves()
    {
        var woken = new QueueRecord("card", Now.AddHours(-1), "Build: PR 5", Today, 1);

        Assert.Multiple(() =>
        {
            Assert.That(Refusal(Ready(), woken), Is.EqualTo("its piece has not moved since the queue last resumed it"));
            Assert.That(Refusal(Ready() with { Status = "Build: PR 6" }, woken), Is.Null);
        });
    }

    [Test]
    public void TheDailyCapPerCard_Holds()
    {
        var often = new QueueRecord("card", Now.AddHours(-1), "Build: PR 4", Today, 6);

        Assert.Multiple(() =>
        {
            Assert.That(Refusal(Ready(), often), Is.EqualTo("the queue's daily cap for it is reached"));
            Assert.That(Refusal(Ready(), often with { CountedDay = Today.AddDays(-1) }), Is.Null);
        });
    }
}
