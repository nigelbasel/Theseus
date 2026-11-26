using System.Text.Json.Nodes;
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
        Stop(Said("All green.\n\nShall I push the branch?"));

        var guess = _store.Read(SessionId)!;

        Assert.That((guess.Text, guess.IsGuess, guess.AskedUtc), Is.EqualTo(("Shall I push the branch?", true, Nine)));
    }

    [Test]
    public void ATurnEndingWithoutAQuestion_ClearsAGuess_ButNotAnAskedQuestion()
    {
        Stop(Said("Shall I push the branch?"));
        Stop(Said("Pushed."));

        Assert.That(_store.Read(SessionId), Is.Null);

        _store.Ask(new OwnerQuestion(SessionId, "Which store?", IsGuess: false, Nine));
        Stop(Said("Working on the rest meanwhile."));

        Assert.That(_store.Read(SessionId)!.Text, Is.EqualTo("Which store?"));
    }

    [Test]
    public void TheOwnerTypingInTheTab_AnswersWhateverWasOpen()
    {
        _store.Ask(new OwnerQuestion(SessionId, "Which store?", IsGuess: false, Nine));

        HookQuestion.OnEvent(new StopHookInput(SessionId, "UserPromptSubmit"), _store, _ => null, Nine, delivered: false);

        Assert.That(_store.Read(SessionId), Is.Null);
    }

    [TestCase(StopRouting.NudgeMarker)]
    [TestCase(StopRouting.HandInMarker)]
    public void AMarkerTheseusTyped_IsNotTheOwnerAnswering(string marker)
    {
        _store.Ask(new OwnerQuestion(SessionId, "Which store?", IsGuess: false, Nine));

        HookQuestion.OnEvent(new StopHookInput(SessionId, "UserPromptSubmit", Prompt: marker), _store, _ => null, Nine, delivered: false);

        Assert.That(_store.Read(SessionId), Is.Not.Null, "a parked question stays on the board through a hand-in or a resume");
    }

    [Test]
    public void AnAnswerThroughTheQuestionTool_AfterTheQuestion_AnswersIt()
    {
        _store.Ask(new OwnerQuestion(SessionId, "Which store?", IsGuess: false, Nine));

        Stop(QuestionToolAsked("t1", Nine.AddMinutes(1)) + QuestionToolAnswered("t1", Nine.AddMinutes(2)) + Said("Using Redis."));

        Assert.That(_store.Read(SessionId), Is.Null, "answering the question tool sends no prompt, so nothing else would clear it");
    }

    [Test]
    public void AnAnswerThroughTheQuestionTool_BeforeTheQuestion_DoesNotAnswerIt()
    {
        _store.Ask(new OwnerQuestion(SessionId, "Which store?", IsGuess: false, Nine));

        Stop(QuestionToolAsked("t1", Nine.AddMinutes(-5)) + QuestionToolAnswered("t1", Nine.AddMinutes(-4)) + Said("Waiting."));

        Assert.That(_store.Read(SessionId), Is.Not.Null);
    }

    [Test]
    public void TheFinalTextClaudeCodeSends_IsPreferredToTheTranscript()
    {
        HookQuestion.OnEvent(new StopHookInput(SessionId, "Stop", TranscriptPath: "transcript", LastAssistantMessage: "Shall I push?"),
            _store, _ => Said("Pushed."), Nine, delivered: false);

        Assert.That(_store.Read(SessionId)!.Text, Is.EqualTo("Shall I push?"),
            "the transcript may not hold the turn's last words yet when the hook runs");
    }

    [TestCase(true, null)]
    [TestCase(false, "agent-1")]
    public void ATurnThisHookExtended_OrASubagents_IsNotJudged(bool delivered, string? agentId)
    {
        HookQuestion.OnEvent(new StopHookInput(SessionId, "Stop", AgentId: agentId, TranscriptPath: "transcript"), _store,
            _ => Said("Shall I push?"), Nine, delivered);

        Assert.That(_store.Read(SessionId), Is.Null);
    }

    [Test]
    public void ATurnAnotherHookExtended_IsJudged_SoItsLastEndingClearsAGuess()
    {
        Stop(Said("Shall I push?"));

        HookQuestion.OnEvent(new StopHookInput(SessionId, "Stop", StopHookActive: true, TranscriptPath: "transcript"), _store,
            _ => Said("Pushed."), Nine, delivered: false);

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

    private void Stop(string transcript) =>
        HookQuestion.OnEvent(new StopHookInput(SessionId, "Stop", TranscriptPath: "transcript"), _store, _ => transcript, Nine, delivered: false);

    private static string Said(string text) => Record("assistant", Nine, new JsonObject { ["type"] = "text", ["text"] = text });

    private static string QuestionToolAsked(string id, DateTimeOffset at) =>
        Record("assistant", at, new JsonObject { ["type"] = "tool_use", ["id"] = id, ["name"] = "AskUserQuestion" });

    private static string QuestionToolAnswered(string id, DateTimeOffset at) =>
        Record("user", at, new JsonObject { ["type"] = "tool_result", ["tool_use_id"] = id });

    private static string Record(string type, DateTimeOffset at, JsonObject block) =>
        new JsonObject
        {
            ["type"] = type,
            ["timestamp"] = at.ToString("O"),
            ["message"] = new JsonObject { ["content"] = new JsonArray(block) },
        }.ToJsonString() + "\n";
}
