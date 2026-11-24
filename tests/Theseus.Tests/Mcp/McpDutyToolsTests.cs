using System.Text.Json.Nodes;
using Theseus.Core.Attention;
using Theseus.Core.Domain;
using Theseus.Core.Duties;
using Theseus.Core.Links;
using Theseus.Core.Mcp;
using Theseus.Core.Proposals;
using Theseus.Tests.Duties;

namespace Theseus.Tests.Mcp;

/// <summary>
/// set_duty, duties, pause_duty and end_duty, through the server as a session calls them: on its own card and on the cards
/// start_workstream lets it act on, never on another's, and never changing a duty the owner set.
/// </summary>
[TestFixture]
public class McpDutyToolsTests
{
    private static readonly DateTimeOffset Nine = new(2026, 11, 24, 9, 0, 0, TimeSpan.Zero);
    private string _directory = string.Empty;
    private ProposalBook _proposals = null!;
    private DutyBook _duties = null!;
    private McpHandler _handler = null!;
    private List<BoardSnapshotCard> _cards = [];
    private readonly List<string> _recorded = [];

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-mcp-duties-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        var clock = new DutyTestClock(Nine);
        _proposals = new ProposalBook(new ProposalStateStore(Path.Combine(_directory, "proposals.json")), clock);
        _duties = new DutyBook(new DutyStore(Path.Combine(_directory, "duties.json")), clock, TimeZoneInfo.Utc);
        _recorded.Clear();
        _cards = [Card("parent"), Card("child"), Card("other")];
        _handler = new McpHandler(
            new WorkLog(Path.Combine(_directory, "work.jsonl")),
            () => [],
            sessionId => sessionId == "parent-session" ? new McpSession("parent-session", "parent", "Parent") : null,
            () => Nine,
            TimeZoneInfo.Utc,
            links: new LinkBook(new LinkStateStore(Path.Combine(_directory, "links.json")), clock),
            board: () => new BoardSnapshot(_cards, Nine),
            paths: new SessionPathRule(Path.Combine(_directory, "plans"), Path.Combine(_directory, "data")),
            proposals: _proposals,
            duties: new DutyTools(_duties, TimeZoneInfo.Utc, (card, text) => _recorded.Add($"{card}: {text}")));

        var brief = Path.Combine(_directory, "proposals", "child", "BRIEF.md");
        Directory.CreateDirectory(Path.GetDirectoryName(brief)!);
        File.WriteAllText(brief, "Do the work.");
        Call("propose_workstream", new JsonObject { ["title"] = "child", ["brief"] = brief, ["done_criteria"] = "Done.", ["report_back"] = true });
        _proposals.Approve(_proposals.Pending.Single().Id, "child");
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, recursive: true);

    [Test]
    public void SetDuty_OnItsOwnCard_ByDefault()
    {
        var result = Call("set_duty", new JsonObject { ["name"] = "Check", ["instruction"] = "look for new bugs", ["every"] = "20m" });

        Assert.Multiple(() =>
        {
            Assert.That(IsError(result), Is.False, Text(result));
            Assert.That(Text(result), Does.StartWith("Set duty \"Check\" on Parent: every 20 min, no end; next run Tue 09:20."));
            Assert.That(_duties.Find("parent", "Check")!.SetBy, Is.EqualTo("parent"));
            Assert.That(_recorded, Is.EqualTo(new[] { "parent: Set duty \"Check\" (every 20 min) by Parent" }));
        });
    }

    [Test]
    public void SetDuty_OnADescendant_AtSetTimes()
    {
        var result = Call("set_duty", new JsonObject
        {
            ["card"] = "Child", ["name"] = "Re-triage", ["instruction"] = "re-run your brief", ["at"] = "07:00", ["days"] = "Mon-Fri",
        });

        Assert.Multiple(() =>
        {
            Assert.That(IsError(result), Is.False, Text(result));
            Assert.That(_duties.Find("child", "Re-triage")!.Cadence.Describe(), Is.EqualTo("at 07:00 on Mon–Fri"));
        });
    }

    [TestCase("other", "is not a card you may set duties on")]
    [TestCase("nobody", "No card on the board is called nobody")]
    public void SetDuty_OnACardItMayNotActOn_IsRefused(string card, string reason)
    {
        var result = Call("set_duty", new JsonObject { ["card"] = card, ["name"] = "Check", ["instruction"] = "x", ["every"] = "1h" });

        Assert.Multiple(() =>
        {
            Assert.That(IsError(result), Is.True);
            Assert.That(Text(result), Does.Contain(reason));
            Assert.That(_duties.All, Is.Empty);
        });
    }

    [TestCase("""{ "name": "Check", "instruction": "x" }""", "Give every or at")]
    [TestCase("""{ "name": "Check", "instruction": "x", "every": "1m" }""", "at most every 5 minutes")]
    [TestCase("""{ "name": "Check", "instruction": "x", "every": "soon" }""", "is not an interval")]
    [TestCase("""{ "name": "Check", "instruction": "x", "at": "7am" }""", "is not a list of times")]
    [TestCase("""{ "name": "Check", "instruction": "x", "every": "1h", "until": "later" }""", "is not an end")]
    [TestCase("""{ "name": "Check", "instruction": "x", "every": "1h", "until": "criterion: done" }""", "Only a card in a project")]
    public void SetDuty_WithACadenceOrEndItCannotRead_SaysWhy(string arguments, string reason)
    {
        var result = Call("set_duty", JsonNode.Parse(arguments)!.AsObject());

        Assert.Multiple(() =>
        {
            Assert.That(IsError(result), Is.True);
            Assert.That(Text(result), Does.Contain(reason));
        });
    }

    [Test]
    public void ADutyTheOwnerSet_CannotBeChangedPausedOrEndedByASession()
    {
        _duties.Set("child", "Check", "owner's words", DutyCadence.Every(TimeSpan.FromHours(1)), DutyEnd.Never, null);

        Assert.Multiple(() =>
        {
            Assert.That(Text(Call("set_duty", new JsonObject { ["card"] = "child", ["name"] = "Check", ["instruction"] = "x", ["every"] = "1h" })),
                Does.Contain("set by the user"));
            Assert.That(Text(Call("pause_duty", new JsonObject { ["card"] = "child", ["name"] = "Check" })), Does.Contain("only they pause it"));
            Assert.That(Text(Call("end_duty", new JsonObject { ["card"] = "child", ["name"] = "Check" })), Does.Contain("only they end it"));
            Assert.That(_duties.Find("child", "Check")!.Instruction, Is.EqualTo("owner's words"));
        });
    }

    [Test]
    public void PauseDuty_ThenSetDutyAgain_ResumesIt_AndEndDutyEndsIt()
    {
        Call("set_duty", new JsonObject { ["name"] = "Check", ["instruction"] = "x", ["every"] = "1h" });

        Call("pause_duty", new JsonObject { ["name"] = "Check", ["why"] = "nothing to watch tonight" });
        var paused = _duties.Find("parent", "Check")!;
        Call("set_duty", new JsonObject { ["name"] = "Check", ["instruction"] = "x", ["every"] = "1h" });
        var resumed = _duties.Find("parent", "Check")!;
        Call("end_duty", new JsonObject { ["name"] = "Check", ["why"] = "the queue is empty" });

        Assert.Multiple(() =>
        {
            Assert.That((paused.PausedBy, paused.PauseReason), Is.EqualTo(("parent", "nothing to watch tonight")));
            Assert.That(resumed.IsPaused, Is.False);
            Assert.That(_duties.Find("parent", "Check"), Is.Null);
            Assert.That(_duties.Get(paused.Id)!.EndReason, Is.EqualTo("the queue is empty"));
        });
    }

    [Test]
    public void Duties_ListsTheCardsItMayActOn_Only()
    {
        Call("set_duty", new JsonObject { ["card"] = "child", ["name"] = "Check", ["instruction"] = "look", ["every"] = "2h" });
        _duties.Set("other", "Secret", "not yours", DutyCadence.Every(TimeSpan.FromHours(1)), DutyEnd.Never, null);

        var text = Text(Call("duties", new JsonObject()));

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("Child (child): \"Check\", every 2 h, next Tue 11:00; set by parent. look"));
            Assert.That(text, Does.Not.Contain("Secret"));
        });
    }

    [Test]
    public void TheDutyToolsAreListed_AndOnlyTheReadingOneIsPreApproved()
    {
        var listed = JsonNode.Parse(_handler.Handle("parent-session", """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""", null).Body!)!
            ["result"]!["tools"]!.AsArray().Select(tool => tool!["name"]!.GetValue<string>()).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(listed, Is.SupersetOf(DutyTools.Names));
            Assert.That(DutyTools.Names, Is.SubsetOf(TheseusMcp.ToolNames));
            Assert.That(TheseusMcp.PreApproved, Does.Contain(DutyTools.ListDuties));
            Assert.That(TheseusMcp.PreApproved, Has.None.AnyOf(DutyTools.SetDuty, DutyTools.PauseDuty, DutyTools.EndDuty));
        });
    }

    private BoardSnapshotCard Card(string key) =>
        new(new Workstream(key, char.ToUpperInvariant(key[0]) + key[1..], "#208DD7", false, _directory, [], "Carry on.", 0, "New",
                false, false, false, null, [], null, Nine),
            Path.Combine(_directory, $"HANDOFF-{key}.md"), SessionLifecycle.NotStarted, false, false, null, false);

    private JsonNode Call(string tool, JsonObject arguments)
    {
        var message = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 1,
            ["method"] = "tools/call",
            ["params"] = new JsonObject { ["name"] = tool, ["arguments"] = arguments.DeepClone() },
        };

        return JsonNode.Parse(_handler.Handle("parent-session", message.ToJsonString(), null).Body!)!["result"]!;
    }

    private static string Text(JsonNode result) => result["content"]![0]!["text"]!.GetValue<string>();

    private static bool IsError(JsonNode result) => result["isError"]!.GetValue<bool>();
}
