using System.Text.Json.Nodes;
using Theseus.Core.Attention;
using Theseus.Core.Mcp;

namespace Theseus.Tests.Mcp;

/// <summary>
/// gates(), accept_spec and events() (1.1e AC 27a, 28): answered by the board for the calling card only; gates and events
/// read-only and pre-approved, accept_spec passing the person in the tab.
/// </summary>
[TestFixture]
public class McpGateToolsTests
{
    private static readonly DateTimeOffset Nine = new(2026, 11, 22, 9, 0, 0, TimeSpan.Zero);
    private string _directory = string.Empty;
    private RecordingGates _gates = null!;
    private McpHandler _handler = null!;
    private List<string> _eventsAsked = [];

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-mcp-gates-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _gates = new RecordingGates();
        _eventsAsked = [];
        _handler = new McpHandler(
            new WorkLog(Path.Combine(_directory, "work.jsonl")),
            () => [],
            sessionId => sessionId == "w7-session" ? new McpSession("w7-session", "w7", "W7") : null,
            () => Nine,
            TimeZoneInfo.Utc,
            gates: _gates,
            events: key =>
            {
                _eventsAsked.Add(key);

                return "Nothing new from the owner on the board since you last asked.";
            });
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, recursive: true);

    [Test]
    public void GatesAndEvents_RunWithoutAsking_AcceptSpecDoesNot()
    {
        Assert.That(TheseusMcp.ToolNames, Is.SupersetOf(new[] { "gates", "accept_spec", "events" }));
        Assert.That(TheseusMcp.PreApproved, Does.Contain("gates").And.Contain("events").And.Not.Contain("accept_spec"));
    }

    [Test]
    public void Gates_IsAnsweredForTheCallingCard()
    {
        Assert.That(Text(Call("gates", new JsonObject())), Is.EqualTo("gates of w7"));
    }

    [Test]
    public void AcceptSpec_PassesTheOwnersWordsToTheBoard_AndSaysNothingIsAcceptedYet()
    {
        var result = Call("accept_spec", new JsonObject { ["slug"] = "w7", ["version"] = "abc1234", ["owner_words"] = "yes, accept it" });

        Assert.That(_gates.Reports.Single(), Is.EqualTo(("w7", "w7", "abc1234", "yes, accept it")));
        Assert.That(Text(result), Does.StartWith("Reported. Nothing is accepted until the owner confirms it on the board"));
    }

    [Test]
    public void AcceptSpec_ReturnsTheBoardsRefusal_AsAnError()
    {
        _gates.Refusal = "Version abc1234 is not the spec's current text";

        var result = Call("accept_spec", new JsonObject { ["slug"] = "w7", ["version"] = "abc1234", ["owner_words"] = "yes" });

        Assert.That((result["isError"]?.GetValue<bool>(), Text(result)), Is.EqualTo(((bool?)true, "Version abc1234 is not the spec's current text")));
    }

    [Test]
    public void Events_IsAnsweredForTheCallingCard()
    {
        Assert.That(Text(Call("events", new JsonObject())), Does.StartWith("Nothing new"));
        Assert.That(_eventsAsked, Is.EqualTo(new[] { "w7" }));
    }

    private JsonNode Call(string tool, JsonObject arguments)
    {
        var request = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 1,
            ["method"] = "tools/call",
            ["params"] = new JsonObject { ["name"] = tool, ["arguments"] = arguments },
        };

        return JsonNode.Parse(_handler.Handle("w7-session", request.ToJsonString(), null).Body!)!["result"]!;
    }

    private static string Text(JsonNode result) => result["content"]![0]!["text"]!.GetValue<string>();

    private sealed class RecordingGates : ISpecGates
    {
        public List<(string, string, string, string)> Reports { get; } = [];

        public string? Refusal { get; set; }

        public string Gates(string callerKey) => $"gates of {callerKey}";

        public string? ReportAcceptance(string callerKey, string slug, string version, string ownerWords)
        {
            if (Refusal is not null)
            {
                return Refusal;
            }

            Reports.Add((callerKey, slug, version, ownerWords));

            return null;
        }
    }
}
