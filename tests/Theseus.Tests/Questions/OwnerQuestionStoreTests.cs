using Theseus.Core.Questions;

namespace Theseus.Tests.Questions;

[TestFixture]
public class OwnerQuestionStoreTests
{
    private const string SessionId = "11111111-2222-3333-4444-555555555555";
    private const string Other = "66666666-7777-8888-9999-000000000000";
    private static readonly DateTimeOffset Nine = new(2026, 11, 23, 9, 0, 0, TimeSpan.Zero);

    private string _root = string.Empty;
    private OwnerQuestionStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), $"theseus-questions-{Guid.NewGuid():N}");
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
    public void Ask_KeepsTheQuestionWithItsOptionsAndWhy()
    {
        _store.Ask(new OwnerQuestion(SessionId, "Push now?", IsGuess: false, Nine) { Options = ["Yes", "Later"], Why = "CI is idle" });

        var read = _store.Read(SessionId)!;

        Assert.That((read.Text, read.IsGuess, read.Why), Is.EqualTo(("Push now?", false, "CI is idle")));
        Assert.That(read.Options, Is.EqualTo(new[] { "Yes", "Later" }));
    }

    [Test]
    public void AGuess_NeverReplacesAQuestionTheSessionAsked()
    {
        _store.Ask(new OwnerQuestion(SessionId, "Push now?", IsGuess: false, Nine));
        _store.Guess(new OwnerQuestion(SessionId, "Shall I?", IsGuess: true, Nine));

        Assert.That(_store.Read(SessionId)!.Text, Is.EqualTo("Push now?"));
    }

    [Test]
    public void AnAskedQuestion_ReplacesAGuess()
    {
        _store.Guess(new OwnerQuestion(SessionId, "Shall I?", IsGuess: true, Nine));
        _store.Ask(new OwnerQuestion(SessionId, "Push now?", IsGuess: true, Nine));

        var read = _store.Read(SessionId)!;

        Assert.That((read.Text, read.IsGuess), Is.EqualTo(("Push now?", false)), "Ask records an asked question, whatever it is given");
    }

    [Test]
    public void ClearGuess_LeavesAnAskedQuestion_AndClearsAGuess()
    {
        _store.Ask(new OwnerQuestion(SessionId, "Push now?", IsGuess: false, Nine));
        _store.Guess(new OwnerQuestion(Other, "Shall I?", IsGuess: true, Nine));

        _store.ClearGuess(SessionId);
        _store.ClearGuess(Other);

        Assert.That(_store.Read(SessionId), Is.Not.Null);
        Assert.That(_store.Read(Other), Is.Null);
    }

    [Test]
    public void Clear_RemovesWhateverWasOpen()
    {
        _store.Ask(new OwnerQuestion(SessionId, "Push now?", IsGuess: false, Nine));

        _store.Clear(SessionId);

        Assert.That(_store.Read(SessionId), Is.Null);
    }

    [Test]
    public void AnIdentifierThatIsNotASessionId_NeverBecomesAPath()
    {
        _store.Ask(new OwnerQuestion(@"..\..\escape", "Push now?", IsGuess: false, Nine));

        Assert.That(Directory.Exists(_root), Is.False, "the identifier arrives from outside, so it must never become a path unchecked");
        Assert.That(_store.Read(@"..\..\escape"), Is.Null);
    }

    [Test]
    public void RemoveExcept_RemovesOldQuestionsOfSessionsNotRunning_AndKeepsTheRest()
    {
        _store.Ask(new OwnerQuestion(SessionId, "Push now?", IsGuess: false, Nine));
        _store.Guess(new OwnerQuestion(Other, "Shall I?", IsGuess: true, Nine));

        _store.RemoveExcept([SessionId.ToUpperInvariant()], DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.That(_store.Read(SessionId), Is.Not.Null, "a running session's question stays, whatever the case of its id");
        Assert.That(_store.Read(Other), Is.Null);
    }

    [Test]
    public void RemoveExcept_KeepsAQuestionJustWritten_EvenForASessionNotSeenRunning()
    {
        _store.Ask(new OwnerQuestion(SessionId, "Push now?", IsGuess: false, Nine));

        _store.RemoveExcept([], DateTimeOffset.UtcNow.AddMinutes(-10));

        Assert.That(_store.Read(SessionId), Is.Not.Null, "a registry read that comes back short must not lose a question just asked");
    }

    [Test]
    public void AnUnreadableFile_ReadsAsNoQuestion()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, $"{SessionId}.json"), "{ not json");

        Assert.That(_store.Read(SessionId), Is.Null);
    }
}
