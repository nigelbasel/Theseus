using System.Text.Json.Nodes;
using Theseus.Core.Abstractions;
using Theseus.Core.Attention;
using Theseus.Core.Domain;
using Theseus.Core.Links;
using Theseus.Core.Mcp;

namespace Theseus.Tests.Mcp;

[TestFixture]
public class McpLinkToolsTests
{
    private const string Output = @"C:\work\proposals\token-spike\REPORT.md";
    private static readonly DateTimeOffset Nine = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);

    private string _directory = string.Empty;
    private LinkBook _book = null!;
    private BoardSnapshot _board = BoardSnapshot.Empty;
    private int _boardReads;
    private McpHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-mcp-links-{Guid.NewGuid():N}");
        _book = new LinkBook(new LinkStateStore(Path.Combine(_directory, "links.json")), new FixedClock(Nine));
        _board = new BoardSnapshot(
        [
            Card("token-spike", SessionLifecycle.Running, live: true),
            Card("auth-research", SessionLifecycle.Ended),
            Card("auth-implementation", SessionLifecycle.NotStarted),
        ], Nine);
        _boardReads = 0;
        _handler = new McpHandler(
            new WorkLog(Path.Combine(_directory, "work.jsonl")),
            () => [],
            sessionId => sessionId switch
            {
                "spike-session" => new McpSession("spike-session", "token-spike", "Token spike"),
                "research-session" => new McpSession("research-session", "auth-research", "Auth research"),
                _ => null,
            },
            () => Nine,
            TimeZoneInfo.Utc,
            links: _book,
            board: () =>
            {
                _boardReads++;
                return _board;
            },
            paths: new SessionPathRule(Path.Combine(_directory, "plans"), _directory));
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
    public void Related_GivesEachLinkWithTheOtherCardsLifecycleAndPaths()
    {
        _book.Add(LinkKind.HandsOver, "auth-research", "token-spike", output: @"C:\work\proposals\auth-research\HANDOVER.md");
        _book.Add(LinkKind.ReportsBack, "token-spike", "auth-research", output: Output);

        var related = JsonNode.Parse(Text(Call("related", new JsonObject(), "spike-session")))!;
        var links = related["links"]!.AsArray();

        Assert.That(links, Has.Count.EqualTo(2));
        Assert.That(links[0]!["kind"]!.GetValue<string>(), Is.EqualTo("hands-over"));
        Assert.That(links[0]!["you"]!.GetValue<string>(), Is.EqualTo("child"));
        Assert.That(links[0]!["youWrite"]!.GetValue<bool>(), Is.False);
        Assert.That(links[0]!["document"]!.GetValue<string>(), Is.EqualTo(@"C:\work\proposals\auth-research\HANDOVER.md"));
        Assert.That(links[0]!["other"]!["lifecycle"]!.GetValue<string>(), Is.EqualTo("Ended"));
        Assert.That(links[0]!["other"]!["handoff"]!.GetValue<string>(), Is.EqualTo(@"C:\work\HANDOFF-auth-research.md"));
        Assert.That(links[1]!["kind"]!.GetValue<string>(), Is.EqualTo("reports-back"));
        Assert.That(links[1]!["you"]!.GetValue<string>(), Is.EqualTo("child"));
        Assert.That(links[1]!["youWrite"]!.GetValue<bool>(), Is.True);
        Assert.That(links[1]!["state"]!.GetValue<string>(), Is.EqualTo("waiting"));
        Assert.That(links[1]!["document"]!.GetValue<string>(), Is.EqualTo(Output));
    }

    [Test]
    public void Related_ReadsTheSnapshotAndWritesNothing()
    {
        _book.Add(LinkKind.HandsOver, "token-spike", "auth-research", output: Output);
        var before = File.ReadAllText(Path.Combine(_directory, "links.json"));
        var changes = 0;
        _book.Changed += (_, _) => changes++;

        Call("related", new JsonObject(), "research-session");

        Assert.That(_boardReads, Is.EqualTo(1));
        Assert.That(changes, Is.Zero);
        Assert.That(File.ReadAllText(Path.Combine(_directory, "links.json")), Is.EqualTo(before));
    }

    [Test]
    public void Related_GivesTheLatestReportToTheDownstream()
    {
        _book.Add(LinkKind.HandsOver, "token-spike", "auth-research", output: Output);
        _book.Report("token-spike", "spike-session", [], Output, "Found the lifetime.", "go");

        var link = JsonNode.Parse(Text(Call("related", new JsonObject(), "research-session")))!["links"]![0]!;

        Assert.That(link["state"]!.GetValue<string>(), Is.EqualTo("reported"));
        Assert.That(link["latestReport"]!["summary"]!.GetValue<string>(), Is.EqualTo("Found the lifetime."));
        Assert.That(link["latestReport"]!["verdict"]!.GetValue<string>(), Is.EqualTo("go"));
    }

    [Test]
    public void ReportBack_RecordsTheReportAndLeavesTheLinkReported()
    {
        _book.Add(LinkKind.HandsOver, "token-spike", "auth-research", output: Output);

        var result = Call("report_back", new JsonObject { ["document"] = Output, ["summary"] = "Found it.", ["verdict"] = "go" },
            "spike-session");

        Assert.That(result["isError"]!.GetValue<bool>(), Is.False, Text(result));
        Assert.That(Text(result), Does.Contain("not satisfied until the user confirms"));
        Assert.That(_book.Active.Single().LatestReport!.SessionId, Is.EqualTo("spike-session"));
        Assert.That(_book.Active.Single().SatisfiedUtc, Is.Null);
    }

    [Test]
    public void ReportBack_IsRefusedWhenTheCallerIsNotTheUpstream()
    {
        var link = _book.Add(LinkKind.HandsOver, "token-spike", "auth-research", output: Output).Link!;

        var result = Call("report_back",
            new JsonObject { ["links"] = new JsonArray(link.Id), ["document"] = Output, ["summary"] = "Done." },
            "research-session");

        Assert.That(result["isError"]!.GetValue<bool>(), Is.True);
        Assert.That(_book.Active.Single().LatestReport, Is.Null);
    }

    [Test]
    public void ReportBack_IsRefusedWhenTheDocumentIsNotTheLinksOutput()
    {
        _book.Add(LinkKind.HandsOver, "token-spike", "auth-research", output: Output);

        var result = Call("report_back",
            new JsonObject { ["document"] = @"C:\work\proposals\token-spike\OTHER.md", ["summary"] = "Done." }, "spike-session");

        Assert.That(result["isError"]!.GetValue<bool>(), Is.True);
        Assert.That(Text(result), Does.Contain(Output));
    }

    [Test]
    public void ReportBack_IsRefusedWhenSeveralLinksWaitAndNoneIsNamed()
    {
        _book.Add(LinkKind.HandsOver, "token-spike", "auth-research", output: Output);
        _book.Add(LinkKind.HandsOver, "token-spike", "auth-implementation", output: Output);

        var result = Call("report_back", new JsonObject { ["document"] = Output, ["summary"] = "Done." }, "spike-session");

        Assert.That(result["isError"]!.GetValue<bool>(), Is.True);
        Assert.That(Text(result), Does.Contain("More than one"));
    }

    [Test]
    public void ReportBack_RefusesAHandoffEvenWhenTheLinkNamesIt()
    {
        const string handoff = @"C:\work\HANDOFF-token-spike.md";
        _book.Add(LinkKind.HandsOver, "token-spike", "auth-research", output: handoff);

        var result = Call("report_back", new JsonObject { ["document"] = handoff, ["summary"] = "Done." }, "spike-session");

        Assert.That(result["isError"]!.GetValue<bool>(), Is.True);
        Assert.That(Text(result), Does.Contain("handoff"));
        Assert.That(_book.Active.Single().LatestReport, Is.Null);
    }

    [Test]
    public void ASessionTheseusDidNotLaunch_CannotReport()
    {
        _book.Add(LinkKind.HandsOver, "token-spike", "auth-research", output: Output);

        var result = Call("report_back", new JsonObject { ["document"] = Output, ["summary"] = "Done." }, "stranger");

        Assert.That(result["isError"]!.GetValue<bool>(), Is.True);
        Assert.That(_book.Active.Single().LatestReport, Is.Null);
    }

    [Test]
    public void Initialize_TellsSessionsAReportNeverSatisfiesALink()
    {
        var instructions = Request("initialize", new JsonObject(), "spike-session")["result"]!["instructions"]!.GetValue<string>();

        Assert.That(instructions, Does.Contain("related").And.Contain("report_back").And.Contain("never satisfies"));
    }

    private JsonNode Request(string method, JsonObject? parameters, string session)
    {
        var message = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = method };

        if (parameters is not null)
        {
            message["params"] = parameters;
        }

        return JsonNode.Parse(_handler.Handle(session, message.ToJsonString(), null).Body!)!;
    }

    [Test]
    public void LinkWorkstream_HandsOverFromThisCardWhenItIsTheParent()
    {
        var result = Call("link_workstream", new JsonObject { ["other"] = "Auth implementation", ["this_is"] = "parent" },
            "research-session");

        var link = _book.Active.Single();

        Assert.That(result["isError"]!.GetValue<bool>(), Is.False, Text(result));
        Assert.That((link.Kind, link.From, link.To), Is.EqualTo((LinkKind.HandsOver, "auth-research", "auth-implementation")));
        Assert.That(link.Output, Is.Null);
        Assert.That(link.CreatedBy, Is.EqualTo("session research-session"));
    }

    [Test]
    public void LinkWorkstream_AsTheChildWithAReportBackToTheParentsBriefFolder()
    {
        var result = Call("link_workstream",
            new JsonObject { ["other"] = "auth-research", ["this_is"] = "child", ["report_back"] = true }, "spike-session");

        Assert.That(result["isError"]!.GetValue<bool>(), Is.False, Text(result));
        Assert.That(_book.Active.Select(link => (link.Kind, link.From, link.To, link.Output)), Is.EquivalentTo(new[]
        {
            (LinkKind.HandsOver, "auth-research", "token-spike", (string?)null),
            (LinkKind.ReportsBack, "token-spike", "auth-research", (string?)@"C:\work\proposals\token-spike\REPORT.md"),
        }));
    }

    [Test]
    public void LinkWorkstream_PassesOnTheBooksRefusal()
    {
        _book.Add(LinkKind.HandsOver, "auth-implementation", "token-spike");

        var result = Call("link_workstream", new JsonObject { ["other"] = "auth-research", ["this_is"] = "child" }, "spike-session");

        Assert.That(result["isError"]!.GetValue<bool>(), Is.True);
        Assert.That(Text(result), Does.Contain("takes over from one card only"));
    }

    [Test]
    public void LinkWorkstream_RefusesAnUnknownCardOrAHandoffAsTheDocument()
    {
        var unknown = Call("link_workstream", new JsonObject { ["other"] = "Nowhere", ["this_is"] = "parent" }, "research-session");
        var handoff = Call("link_workstream",
            new JsonObject { ["other"] = "token-spike", ["this_is"] = "parent", ["document"] = @"C:\work\HANDOFF-token-spike.md" },
            "research-session");
        var sideless = Call("link_workstream", new JsonObject { ["other"] = "token-spike" }, "research-session");

        Assert.That(Text(unknown), Does.Contain("no card Nowhere"));
        Assert.That(Text(handoff), Does.Contain("handoff"));
        Assert.That(Text(sideless), Does.Contain("parent or child"));
        Assert.That(_book.All, Is.Empty);
    }

    [Test]
    public void UnlinkWorkstream_RemovesOnlyLinksTouchingThisCard()
    {
        var mine = _book.Add(LinkKind.HandsOver, "auth-research", "token-spike").Link!;
        var theirs = _book.Add(LinkKind.HandsOver, "token-spike", "auth-implementation").Link!;

        var refused = Call("unlink_workstream", new JsonObject { ["link_id"] = theirs.Id }, "research-session");
        var removed = Call("unlink_workstream", new JsonObject { ["link_id"] = mine.Id }, "research-session");

        Assert.That(Text(refused), Does.Contain("does not touch this workstream"));
        Assert.That(removed["isError"]!.GetValue<bool>(), Is.False, Text(removed));
        Assert.That(_book.Active.Single().Id, Is.EqualTo(theirs.Id));
    }

    private JsonNode Call(string tool, JsonObject arguments, string session) =>
        Request("tools/call", new JsonObject { ["name"] = tool, ["arguments"] = arguments }, session)["result"]!;

    private static string Text(JsonNode result) => result["content"]![0]!["text"]!.GetValue<string>();

    private static BoardSnapshotCard Card(string key, SessionLifecycle lifecycle, bool live = false) =>
        new(new Workstream(key, Title(key), "#208DD7", false, @"C:\work", [], "Carry on.", 0, "New", false, false,
                false, null, [], null, Nine),
            $@"C:\work\HANDOFF-{key}.md", lifecycle, live, false, null, false);

    private static string Title(string key)
    {
        var words = key.Replace('-', ' ');

        return char.ToUpperInvariant(words[0]) + words[1..];
    }

    private sealed class FixedClock(DateTimeOffset now) : ISystemClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
