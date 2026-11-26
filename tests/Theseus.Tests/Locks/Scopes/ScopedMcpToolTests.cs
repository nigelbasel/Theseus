using System.Text.Json.Nodes;
using Theseus.Core.Abstractions;
using Theseus.Core.Attention;
using Theseus.Core.Locks;
using Theseus.Core.Mcp;

namespace Theseus.Tests.Locks.Scopes;

[TestFixture]
public class ScopedMcpToolTests
{
    private static readonly DateTimeOffset Nine = new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);

    private string _directory = string.Empty;
    private LockBook _book = null!;
    private McpHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-scoped-mcp-{Guid.NewGuid():N}");
        _book = new LockBook(
            new LockStateStore(Path.Combine(_directory, "locks.json")),
            new LockHistory(Path.Combine(_directory, "locks-history.jsonl")),
            new McpLiveness(),
            new McpClock());
        _handler = new McpHandler(
            new WorkLog(Path.Combine(_directory, "work.jsonl")),
            () => new List<AttentionInterval>(),
            sessionId => sessionId switch
            {
                "members" => new McpSession("members", "members-hub", "Members Hub"),
                "leadpro" => new McpSession("leadpro", "leadpro-data", "LeadPro data"),
                "arbiter" => new McpSession("arbiter", "lock-arbiter", "Lock arbiter"),
                _ => null,
            },
            () => Nine,
            TimeZoneInfo.Utc,
            _book,
            arbiter: () => "lock-arbiter");
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
    public void TwoStacksWithTheirOwnProjectsAndPorts_AreHeldAtOnce()
    {
        string members = Text(Call("lock_acquire", new JsonObject { ["scope"] = new JsonArray("compose:members-hub", "port:8080"), ["purpose"] = "stack" }, "members"));
        string leadpro = Text(Call("lock_acquire", new JsonObject { ["scope"] = new JsonArray("compose:leadpro", "port:8081"), ["purpose"] = "data" }, "leadpro"));
        string listing = Text(Call("locks", new JsonObject(), "members"));

        Assert.That(members, Does.StartWith("You hold"));
        Assert.That(leadpro, Does.StartWith("You hold"));
        Assert.That(listing, Does.Contain("Members Hub for stack: compose:members-hub + port:8080"));
        Assert.That(listing, Does.Contain("dev-stack: free"));
    }

    [Test]
    public void ReleasingByTheScopeAsAsked_LetsItGo()
    {
        Call("lock_acquire", new JsonObject { ["scope"] = new JsonArray("port:8080", "compose:members-hub"), ["purpose"] = "stack" }, "members");

        JsonNode released = Call("lock_release", new JsonObject { ["scope"] = new JsonArray("compose:members-hub", "port:8080") }, "members");

        Assert.That(released["isError"]!.GetValue<bool>(), Is.False);
        Assert.That(_book.Snapshot(), Is.Empty);
    }

    [Test]
    public void RuleResource_IsListedAndAllowedOnlyForTheArbiter()
    {
        JsonObject arguments = new() { ["name"] = "gpu", ["capacity"] = 1, ["clashesWith"] = new JsonArray("llama"), ["why"] = "model server" };

        Assert.That(ToolNames("members"), Does.Not.Contain("rule_resource"));
        Assert.That(ToolNames("arbiter"), Does.Contain("rule_resource"));
        Assert.That(Call("rule_resource", arguments.DeepClone().AsObject(), "members")["isError"]!.GetValue<bool>(), Is.True);
        Assert.That(Call("rule_resource", arguments, "arbiter")["isError"]!.GetValue<bool>(), Is.False);
        Assert.That(_book.Table.Rule("gpu")!.RuledBy, Is.EqualTo("Lock arbiter"));
    }

    private IEnumerable<string> ToolNames(string session) =>
        Request("tools/list", null, session)["result"]!["tools"]!.AsArray().Select(tool => tool!["name"]!.GetValue<string>());

    private JsonNode Request(string method, JsonObject? parameters, string session)
    {
        JsonObject message = new() { ["jsonrpc"] = "2.0", ["id"] = 7, ["method"] = method };

        if (parameters is not null)
        {
            message["params"] = parameters;
        }

        return JsonNode.Parse(_handler.Handle(session, message.ToJsonString(), null).Body!)!;
    }

    private JsonNode Call(string tool, JsonObject arguments, string session) =>
        Request("tools/call", new JsonObject { ["name"] = tool, ["arguments"] = arguments }, session)["result"]!;

    private static string Text(JsonNode result) => result["content"]![0]!["text"]!.GetValue<string>();

    private sealed class McpLiveness : ILockLiveness
    {
        public string? StartOf(int processId) => null;

        public bool IsSessionAlive(string sessionId) => true;
    }

    private sealed class McpClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Nine;
    }
}
