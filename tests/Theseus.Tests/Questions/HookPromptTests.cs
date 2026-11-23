using System.Text.Json;
using Theseus.Core.Questions;
using Theseus.Core.Status;
using Theseus.Core.Stopping;

namespace Theseus.Tests.Questions;

/// <summary>What the hook records of a prompt that stops a session (1.6, sessions run unattended).</summary>
[TestFixture]
public class HookPromptTests
{
    private const string SessionId = "11111111-2222-3333-4444-555555555555";
    private static readonly DateTimeOffset Nine = new(2026, 11, 23, 9, 0, 0, TimeSpan.Zero);

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
    public void APermissionPrompt_IsRecorded_AsClaudeCodeSendsIt()
    {
        var input = StopHookInput.Parse($$"""
            {"session_id":"{{SessionId}}","transcript_path":"t.jsonl","cwd":"C:\\work","hook_event_name":"Notification",
             "message":"Claude needs your permission to use Bash","notification_type":"permission_prompt"}
            """)!;

        HookPrompt.OnEvent(input, _store, Nine);

        Assert.That(_store.Read(SessionId), Is.EqualTo(new PermissionPrompt(SessionId, "Claude needs your permission to use Bash", Nine)));
    }

    [Test]
    public void AnIdleNudge_IsNotAPrompt()
    {
        HookPrompt.OnEvent(Notification("idle_prompt", "Claude is waiting for your input"), _store, Nine);

        Assert.That(_store.Read(SessionId), Is.Null, "an idle session is already shown as idle or asking; this says nothing new");
    }

    [Test]
    public void ADialog_IsAPrompt_OnOneLine()
    {
        HookPrompt.OnEvent(Notification("elicitation_dialog", "Server asks:\r\n which branch?"), _store, Nine);

        Assert.That(_store.Read(SessionId)!.Text, Is.EqualTo("Server asks: which branch?"));
    }

    [TestCase("UserPromptSubmit")]
    [TestCase("Stop")]
    public void TheOwnerTyping_OrTheTurnEnding_ClearsIt(string hookEvent)
    {
        HookPrompt.OnEvent(Notification("permission_prompt", "Claude needs your permission to use Bash"), _store, Nine);

        HookPrompt.OnEvent(new StopHookInput(SessionId, hookEvent), _store, Nine);

        Assert.That(_store.Read(SessionId), Is.Null);
    }

    [Test]
    public void TheLaunchSettings_RegisterTheHookOnExactlyThePromptsThatStopASession()
    {
        var notification = JsonDocument.Parse(TheseusHookSettings.Json(@"C:\Theseus\Theseus.Hook.exe")).RootElement
            .GetProperty("hooks").GetProperty("Notification")[0];

        Assert.That(notification.GetProperty("matcher").GetString(), Is.EqualTo("permission_prompt|elicitation_dialog"));
        Assert.That(notification.GetProperty("hooks")[0].GetProperty("command").GetString(), Is.EqualTo("\"C:\\Theseus\\Theseus.Hook.exe\""));
    }

    private static StopHookInput Notification(string type, string message) =>
        new(SessionId, "Notification", NotificationType: type, Message: message);
}
