using Theseus.Core.Questions;

namespace Theseus.Tests.Questions;

/// <summary>The prompts folder: one file per session, swept once a session is no longer waiting.</summary>
[TestFixture]
public class PermissionPromptStoreTests
{
    private const string Waiting = "11111111-2222-3333-4444-555555555555";
    private const string Working = "66666666-7777-8888-9999-000000000000";

    private string _directory = string.Empty;
    private PermissionPromptStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-prompts-{Guid.NewGuid():N}");
        _store = new PermissionPromptStore(_directory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Test]
    public void ANameThatIsNotASessionId_NeverBecomesAPath()
    {
        _store.Write(new PermissionPrompt(@"..\..\evil", "x", DateTimeOffset.UtcNow));

        Assert.That(Directory.Exists(_directory), Is.False);
        Assert.That(_store.Read(@"..\..\evil"), Is.Null);
    }

    [Test]
    public void ASweep_KeepsAWaitingSessionsPrompt_AndAFreshOne_AndRemovesAnAnsweredOne()
    {
        var now = DateTimeOffset.UtcNow;

        _store.Write(new PermissionPrompt(Waiting, "Claude needs your permission to use Bash", now));
        _store.Write(new PermissionPrompt(Working, "Claude needs your permission to use Edit", now));

        _store.RemoveExcept([Waiting], now);

        Assert.That(_store.Read(Working), Is.Not.Null, "the registry may not say waiting yet for a prompt just written");

        _store.RemoveExcept([Waiting], now + PermissionPromptStore.Grace + TimeSpan.FromSeconds(1));

        Assert.That(_store.Read(Waiting), Is.Not.Null);
        Assert.That(_store.Read(Working), Is.Null, "answered: the session works again, and no hook event says so");
    }
}
