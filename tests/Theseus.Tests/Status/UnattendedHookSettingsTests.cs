using System.Text.Json;
using Theseus.Core.Questions;
using Theseus.Core.Status;

namespace Theseus.Tests.Status;

/// <summary>
/// An unattended card's own settings (2.4 W2): the hook is registered on the question tool and on Stop with the argument
/// that tells it so, so it decides from its command line alone and works while Theseus is down.
/// </summary>
[TestFixture]
public class UnattendedHookSettingsTests
{
    private const string Hook = @"C:\Program Files\Theseus\Theseus.Hook.exe";

    [Test]
    public void AnUnattendedCard_HasTheHookOnTheQuestionTool_AndOnStop_WithTheArgument()
    {
        var hooks = JsonDocument.Parse(TheseusHookSettings.Json(Hook, unattended: true)).RootElement.GetProperty("hooks");
        var command = $"\"{Hook}\" {UnattendedHook.Argument}";

        Assert.Multiple(() =>
        {
            Assert.That(hooks.GetProperty("PreToolUse").EnumerateArray()
                    .Any(entry => entry.GetProperty("matcher").GetString() == "AskUserQuestion"
                                  && entry.GetProperty("hooks")[0].GetProperty("command").GetString() == command),
                Is.True);
            Assert.That(hooks.GetProperty("Stop")[0].GetProperty("hooks")[0].GetProperty("command").GetString(), Is.EqualTo(command));
            Assert.That(hooks.GetProperty("PreToolUse")[0].GetProperty("matcher").GetString(), Is.EqualTo("mcp__theseus__.*"),
                "the status hook stays");
        });
    }

    [Test]
    public void AnAttendedCard_HasNeither()
    {
        var json = TheseusHookSettings.Json(Hook, unattended: false);

        Assert.Multiple(() =>
        {
            Assert.That(json, Is.EqualTo(TheseusHookSettings.Json(Hook)));
            Assert.That(json, Does.Not.Contain(UnattendedHook.Argument).And.Not.Contain("AskUserQuestion"));
            Assert.That(JsonDocument.Parse(json).RootElement.GetProperty("hooks").TryGetProperty("Stop", out _), Is.False,
                "the stop hook in the user's settings does the rest");
        });
    }
}
