using Theseus.Core.Projects;
using Theseus.Core.Questions;
using Theseus.Core.Stopping;

namespace Theseus.Tests.Questions;

/// <summary>
/// A question asked with ask_owner is answered on the board (2.4e, Nigel 2026-11-26: "there is nothing to answer in those
/// sessions"): the answer reaches a tab, a headless and a stopped session, and the wait clears only once it is delivered.
/// </summary>
[TestFixture]
public class OwnerAnsweringTests
{
    private const string Session = "0b6f4d3c-2a1e-4c55-9d7a-6f1e2b3c4d5e";
    private static readonly DateTimeOffset Nine = new(2026, 11, 26, 21, 0, 0, TimeSpan.Zero);

    private string _folder = string.Empty;
    private OwnerQuestionStore _questions = null!;
    private StopRequestStore _tells = null!;
    private StopRequestStore _stops = null!;
    private OwnerEventLog _log = null!;
    private OwnerAnswering _answering = null!;

    [SetUp]
    public void SetUp()
    {
        _folder = Path.Combine(Path.GetTempPath(), $"theseus-answering-{Guid.NewGuid():N}");
        _questions = new OwnerQuestionStore(Path.Combine(_folder, "questions"));
        _tells = new StopRequestStore(Path.Combine(_folder, "tells"));
        _stops = new StopRequestStore(Path.Combine(_folder, "stops"));
        _log = new OwnerEventLog(Path.Combine(_folder, "owner-events.jsonl"));
        _answering = new OwnerAnswering(_questions, new SessionTells(_tells, _log));
        _questions.Ask(new OwnerQuestion(Session, "Which route?", IsGuess: false, Nine.AddMinutes(-5))
        {
            Options = ["A: stub harness", "B: I sign in"],
            Why = "Neither runs without one",
        });
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Test]
    public void AnswerText_QuotesTheQuestionAndTheAnswer()
    {
        var text = OwnerAnswerText.For(_questions.Read(Session)!, "B: I sign in");

        Assert.That(text, Does.Contain("Which route?").And.Contain("B: I sign in").And.Contain("on the Theseus board"));
    }

    [Test]
    public void ATabSession_IsGivenTheAnswer_AndWokenToTakeIt()
    {
        var woken = 0;

        var outcome = _answering.Answer(Target(AnswerReach.Tab), "B: I sign in", Nine, wake: () => { woken++; return true; }, resume: Never);

        Assert.That((outcome.Route, outcome.IsError, woken), Is.EqualTo((AnswerRoute.TypedIntoTab, false, 1)));
        Assert.That(_tells.Waiting(Session)!.Text, Does.Contain("B: I sign in"));
    }

    [Test]
    public void ATabThatCannotBeWoken_StillHasTheAnswerWaitingForItsNextTurn()
    {
        var outcome = _answering.Answer(Target(AnswerReach.Tab), "B", Nine, wake: () => false, resume: Never);

        Assert.That(outcome.Route, Is.EqualTo(AnswerRoute.NextTurn));
        Assert.That(_tells.Waiting(Session), Is.Not.Null);
    }

    [Test]
    public void AHeadlessSession_TakesTheAnswerWithItsNextTurn_AndIsNotTypedInto()
    {
        var outcome = _answering.Answer(Target(AnswerReach.Headless), "A: stub harness", Nine, wake: Fail, resume: Never);

        Assert.That(outcome.Route, Is.EqualTo(AnswerRoute.NextTurn));
        Assert.That(_tells.Waiting(Session)!.Text, Does.Contain("A: stub harness"));
    }

    [Test]
    public void AnEndedSession_IsResumed_AndItsStartTakesTheAnswer()
    {
        string? started = null;
        var tells = new SessionTells(_tells, _log);

        var outcome = _answering.Answer(Target(AnswerReach.Ended), "B", Nine, wake: Fail,
            resume: () => { started = tells.TakeForStart(Session, Nine); return null; });

        Assert.That(outcome.Route, Is.EqualTo(AnswerRoute.Resumed));
        Assert.That(started, Does.Contain("Which route?"));
    }

    [Test]
    public void AnEndedSessionThatCannotStart_KeepsTheAnswerForItsNextRun()
    {
        var outcome = _answering.Answer(Target(AnswerReach.Ended), "B", Nine, wake: Fail, resume: () => "the headless queue is full");

        Assert.That((outcome.Route, outcome.IsError), Is.EqualTo((AnswerRoute.Kept, false)));
        Assert.That(outcome.Said, Does.Contain("the headless queue is full"));
        Assert.That(_tells.Waiting(Session), Is.Not.Null);
    }

    [Test]
    public void ASessionTheOwnerStopped_IsNotStarted_AndKeepsTheAnswerForItsNextRun()
    {
        var outcome = _answering.Answer(Target(AnswerReach.HeldBack), "B", Nine, wake: Fail, resume: () => throw new AssertionException("started"));

        Assert.That(outcome.Route, Is.EqualTo(AnswerRoute.Kept));
        Assert.That(QuestionWords.Card(_questions.Read(Session)!), Does.Contain("answered, waiting to be delivered"));
    }

    [Test]
    public void TheWait_StaysAnswered_UntilTheHookDeliversIt_ThenClears()
    {
        _answering.Answer(Target(AnswerReach.Headless), "B", Nine, wake: Fail, resume: Never);

        Assert.That(_questions.Read(Session)!.AnsweredUtc, Is.EqualTo(Nine), "answered, not yet delivered");

        var input = new StopHookInput(Session, "Stop");
        var delivered = HookTurn.Deliver(input, _stops, _tells, Nine.AddMinutes(1));
        HookQuestion.OnEvent(input, _questions, _ => null, Nine.AddMinutes(1), delivered: delivered is not null,
            tellWaiting: id => _tells.Waiting(id) is not null);

        Assert.That(delivered!.Text, Does.Contain("B"));
        Assert.That(_questions.Read(Session), Is.Null, "the wait clears on delivery");
    }

    [Test]
    public void AStopDeliveredInsteadOfTheAnswer_LeavesTheWaitAnswered()
    {
        _answering.Answer(Target(AnswerReach.Headless), "B", Nine, wake: Fail, resume: Never);
        _stops.Write(new PendingStopRequest(Session, "w7", "W7", "Please stop", Nine, Nine.AddDays(1)));

        var input = new StopHookInput(Session, "Stop");
        var delivered = HookTurn.Deliver(input, _stops, _tells, Nine.AddMinutes(1));
        HookQuestion.OnEvent(input, _questions, _ => null, Nine.AddMinutes(1), delivered: delivered is not null,
            tellWaiting: id => _tells.Waiting(id) is not null);

        Assert.That(delivered!.Text, Is.EqualTo("Please stop"));
        Assert.That(_questions.Read(Session)!.AnsweredUtc, Is.Not.Null);
    }

    [Test]
    public void TheAnswer_IsRecordedForEvents()
    {
        _answering.Answer(Target(AnswerReach.HeldBack), "B", Nine, wake: Fail, resume: Never);

        Assert.That(_log.All().Single().Text, Does.Contain("B"));
    }

    [Test]
    public void ABlankAnswer_OrNoOpenQuestion_IsRefused_AndNothingIsQueued()
    {
        Assert.That(_answering.Answer(Target(AnswerReach.Tab), "  ", Nine, wake: Fail, resume: Never).IsError, Is.True);

        _questions.Clear(Session);

        Assert.That(_answering.Answer(Target(AnswerReach.Tab), "B", Nine, wake: Fail, resume: Never).IsError, Is.True);
        Assert.That(_tells.Waiting(Session), Is.Null);
    }

    [Test]
    public void AnAnsweredQuestion_IsNotAnsweredTwice()
    {
        _answering.Answer(Target(AnswerReach.HeldBack), "B", Nine, wake: Fail, resume: Never);

        Assert.That(_answering.Answer(Target(AnswerReach.HeldBack), "A", Nine, wake: Fail, resume: Never).IsError, Is.True);
        Assert.That(_tells.Waiting(Session)!.Text, Does.Not.Contain("A:"));
    }

    private static OwnerAnswerTarget Target(AnswerReach reach) => new("w7", "W7", Session, reach);

    private static bool Fail() => throw new AssertionException("woken");

    private static string? Never() => throw new AssertionException("resumed");
}
