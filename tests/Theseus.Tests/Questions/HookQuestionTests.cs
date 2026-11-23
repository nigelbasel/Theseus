using Theseus.Core.Questions;
using Theseus.Core.Stopping;

namespace Theseus.Tests.Questions;

/// <summary>What the hook does about questions on each event (1.6 spec items 4, 7 to 10).</summary>
[TestFixture]
public class HookQuestionTests
{
    private const string SessionId = "11111111-2222-3333-4444-555555555555";
    private static readonly DateTimeOffset Nine = new(2026, 11, 23, 9, 0, 0, TimeSpan.Zero);

    private string _root = string.Empty;
    private OwnerQuestionStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), $"theseus-hook-questions-{Guid.NewGuid():N}");
        _store = new OwnerQuestionStore(_root);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Test]
    public void ATurnEndingOnAProseQuestion_LeavesAGuess()
    {
        Stop("All green.\n\nShall I push the branch?");

        var guess = _store.Read(SessionId)!;

        Assert.That((guess.Text, guess.IsGuess, guess.AskedUtc), Is.EqualTo(("Shall I push the branch?", true, Nine)));
    }

    [Test]
    public void ATurnEndingWithoutAQuestion_ClearsAGuess_ButNotAnAskedQuestion()
    {
        Stop("Shall I push the branch?");
        Stop("Pushed.");

        Assert.That(_store.Read(SessionId), Is.Null);

        _store.Ask(new OwnerQuestion(SessionId, "Which store?", IsGuess: false, Nine));
        Stop("Working on the rest meanwhile.");

        Assert.That(_store.Read(SessionId)!.Text, Is.EqualTo("Which store?"));
    }

    [Test]
    public void TheOwnerTypingInTheTab_AnswersWhateverWasOpen()
    {
        _store.Ask(new OwnerQuestion(SessionId, "Which store?", IsGuess: false, Nine));

        HookQuestion.OnEvent(new StopHookInput(SessionId, "UserPromptSubmit"), _store, _ => null, Nine, delivered: false);

        Assert.That(_store.Read(SessionId), Is.Null);
    }

    [TestCase(true, false, null)]
    [TestCase(false, true, null)]
    [TestCase(false, false, "agent-1")]
    public void ATurnTheHookExtended_OrASubagents_IsNotJudged(bool delivered, bool stopHookActive, string? agentId)
    {
        HookQuestion.OnEvent(new StopHookInput(SessionId, "Stop", stopHookActive, agentId, "transcript"), _store,
            _ => "Shall I push?", Nine, delivered);

        Assert.That(_store.Read(SessionId), Is.Null);
    }

    [Test]
    public void TheTranscriptNamedByTheHook_IsTheOneRead()
    {
        string? asked = null;

        HookQuestion.OnEvent(new StopHookInput(SessionId, "Stop", TranscriptPath: "C:/t.jsonl"), _store,
            path => { asked = path; return null; }, Nine, delivered: false);

        Assert.That(asked, Is.EqualTo("C:/t.jsonl"));
    }

    private void Stop(string lastText) =>
        HookQuestion.OnEvent(new StopHookInput(SessionId, "Stop", TranscriptPath: "transcript"), _store, _ => lastText, Nine, delivered: false);
}
