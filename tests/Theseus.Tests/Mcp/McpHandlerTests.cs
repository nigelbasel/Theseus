using System.Text.Json.Nodes;
using Theseus.Core.Attention;
using Theseus.Core.Mcp;

namespace Theseus.Tests.Mcp;

[TestFixture]
public class McpHandlerTests
{
    private static readonly DateTimeOffset Nine = new(2026, 9, 17, 9, 0, 0, TimeSpan.Zero);

    private string _directory = string.Empty;
    private List<AttentionInterval> _intervals = [];
    private DateTimeOffset _now;
    private McpHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-mcp-{Guid.NewGuid():N}");
        _intervals = [];
        _now = Nine;
        _handler = new McpHandler(
            new WorkLog(Path.Combine(_directory, "work.jsonl")),
            () => _intervals,
            sessionId => sessionId == "session-1" ? new McpSession("session-1", "gdpr-mcp", "GDPR MCP") : null,
            () => _now,
            TimeZoneInfo.Utc);
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
    public void Initialize_AgreesTheVersionTheClientAsksForWhenItIsSupported()
    {
        var result = Request("initialize", new JsonObject { ["protocolVersion"] = "2025-03-26" })["result"]!;

        Assert.That(result["protocolVersion"]!.GetValue<string>(), Is.EqualTo("2025-03-26"));
        Assert.That(result["capabilities"]!["tools"], Is.Not.Null);
    }

    [Test]
    public void Initialize_OffersItsNewestVersionWhenTheClientsIsNotOneItKnows()
    {
        var result = Request("initialize", new JsonObject { ["protocolVersion"] = "1999-01-01" })["result"]!;

        Assert.That(result["protocolVersion"]!.GetValue<string>(), Is.EqualTo(McpHandler.SupportedVersions[0]));
    }

    [Test]
    public void Initialize_TellsSessionsToSayWhatNotHowLong()
    {
        var instructions = Request("initialize", new JsonObject())["result"]!["instructions"]!.GetValue<string>();

        Assert.That(instructions, Does.Contain("start_work").And.Contain("Never estimate"));
        Assert.That(instructions, Does.Contain("approval"), "Theseus never writes to Jira and sessions must ask first");
    }

    [Test]
    public void ANotification_IsAcceptedWithNoBody()
    {
        var response = _handler.Handle("session-1", """{"jsonrpc":"2.0","method":"notifications/initialized"}""", null);

        Assert.That(response, Is.EqualTo(new McpResponse(202, null)));
    }

    [Test]
    public void AnUnknownMethod_IsMethodNotFound()
    {
        Assert.That(Request("resources/list", null)["error"]!["code"]!.GetValue<int>(), Is.EqualTo(-32601));
    }

    [Test]
    public void AnArrayOfMessages_IsRefused()
    {
        Assert.That(_handler.Handle("session-1", "[]", null).StatusCode, Is.EqualTo(400));
    }

    [Test]
    public void AnUnsupportedProtocolVersionHeader_IsRefused()
    {
        Assert.That(_handler.Handle("session-1", """{"jsonrpc":"2.0","id":1,"method":"ping"}""", "1999-01-01").StatusCode,
            Is.EqualTo(400));
    }

    [Test]
    public void ToolsList_OffersTheFourTools()
    {
        var tools = Request("tools/list", null)["result"]!["tools"]!.AsArray();

        Assert.That(tools.Select(tool => tool!["name"]!.GetValue<string>()),
            Is.EquivalentTo(new[] { "start_work", "stop_work", "time_for", "whoami" }));
        Assert.That(tools.All(tool => tool!["inputSchema"]!["type"]!.GetValue<string>() == "object"), Is.True);
    }

    [Test]
    public void StartWork_RefusesSomethingThatIsNotAJiraKey()
    {
        var result = Call("start_work", new JsonObject { ["issue"] = "the export bug" });

        Assert.That(result["isError"]!.GetValue<bool>(), Is.True);
    }

    [Test]
    public void AToolCall_FromASessionTheseusDidNotLaunch_IsRefused()
    {
        var result = Call("start_work", new JsonObject { ["issue"] = "NDC-680" }, session: "stranger");

        Assert.That(result["isError"]!.GetValue<bool>(), Is.True);
        Assert.That(Text(result), Does.Contain("not launched by Theseus"));
    }

    [Test]
    public void TimeFor_GivesAttendedAndMachineTimeSeparately()
    {
        Call("start_work", new JsonObject { ["issue"] = "NDC-680" });
        _now = Nine.AddHours(1);
        _intervals =
        [
            new AttentionInterval(AttentionKind.Attended, "gdpr-mcp", Nine.AddMinutes(10), Nine.AddMinutes(40)),
            new AttentionInterval(AttentionKind.Machine, "gdpr-mcp", Nine, Nine.AddHours(1)),
        ];

        var report = JsonNode.Parse(Text(Call("time_for", new JsonObject { ["issue"] = "ndc-680", ["from"] = "2026-09-17" })))!;

        Assert.Multiple(() =>
        {
            Assert.That(report["attendedMinutes"]!.GetValue<int>(), Is.EqualTo(30));
            Assert.That(report["machineMinutes"]!.GetValue<int>(), Is.EqualTo(60));
            Assert.That(report["note"]!.GetValue<string>(), Does.Contain("never added together"));
        });
    }

    [Test]
    public void StopWork_EndsTheStretchThatTimeIsCountedIn()
    {
        Call("start_work", new JsonObject { ["issue"] = "NDC-680" });
        _now = Nine.AddMinutes(30);
        Call("stop_work", new JsonObject());
        _now = Nine.AddHours(1);
        _intervals = [new AttentionInterval(AttentionKind.Machine, "gdpr-mcp", Nine, Nine.AddHours(1))];

        var report = JsonNode.Parse(Text(Call("time_for", new JsonObject { ["issue"] = "NDC-680" })))!;

        Assert.That(report["machineMinutes"]!.GetValue<int>(), Is.EqualTo(30));
    }

    [Test]
    public void WhoAmI_SaysWhichWorkstreamThisIsAndWhatItIsOn()
    {
        Call("start_work", new JsonObject { ["issue"] = "NDC-680" });

        var identity = JsonNode.Parse(Text(Call("whoami", new JsonObject())))!;

        Assert.That(identity["workstream"]!.GetValue<string>(), Is.EqualTo("gdpr-mcp"));
        Assert.That(identity["workingOn"]!.GetValue<string>(), Is.EqualTo("NDC-680"));
    }

    private JsonNode Request(string method, JsonObject? parameters, string session = "session-1")
    {
        var message = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 7, ["method"] = method };

        if (parameters is not null)
        {
            message["params"] = parameters;
        }

        var response = _handler.Handle(session, message.ToJsonString(), null);

        Assert.That(response.StatusCode, Is.EqualTo(200));

        return JsonNode.Parse(response.Body!)!;
    }

    private JsonNode Call(string tool, JsonObject arguments, string session = "session-1") =>
        Request("tools/call", new JsonObject { ["name"] = tool, ["arguments"] = arguments }, session)["result"]!;

    private static string Text(JsonNode result) => result["content"]![0]!["text"]!.GetValue<string>();
}
