using System.Text.Json.Nodes;
using Theseus.Core.Abstractions;
using Theseus.Core.Attention;
using Theseus.Core.Domain;
using Theseus.Core.Links;
using Theseus.Core.Mcp;
using Theseus.Core.Proposals;

namespace Theseus.Tests.Mcp;

/// <summary>
/// A project's session acting on its descendants (1.1e AC 14, 17): only cards made from proposals its card raised and the
/// owner approved, and theirs in turn, decided from Theseus's own records; each refusal returning its reason and touching
/// nothing; and none of the four acting tools pre-approved, so each passes the person in the tab.
/// </summary>
[TestFixture]
public class McpWorkstreamToolsTests
{
    private static readonly DateTimeOffset Nine = new(2026, 11, 21, 9, 0, 0, TimeSpan.Zero);
    private string _directory = string.Empty;
    private ProposalBook _proposals = null!;
    private McpHandler _handler = null!;
    private RecordingControl _control = null!;
    private List<BoardSnapshotCard> _cards = [];

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-mcp-workstreams-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _proposals = new ProposalBook(new ProposalStateStore(Path.Combine(_directory, "proposals.json")), new FixedClock(Nine));
        _control = new RecordingControl();
        _cards = [Card("parent"), Card("child"), Card("grandchild"), Card("other"), Card("owners")];
        _handler = new McpHandler(
            new WorkLog(Path.Combine(_directory, "work.jsonl")),
            () => [],
            sessionId => sessionId switch
            {
                "parent-session" => new McpSession("parent-session", "parent", "Parent"),
                "child-session" => new McpSession("child-session", "child", "Child"),
                _ => null,
            },
            () => Nine,
            TimeZoneInfo.Utc,
            links: new LinkBook(new LinkStateStore(Path.Combine(_directory, "links.json")), new FixedClock(Nine)),
            board: () => new BoardSnapshot(_cards, Nine),
            paths: new SessionPathRule(Path.Combine(_directory, "plans"), Path.Combine(_directory, "data")),
            proposals: _proposals,
            approve: (_, _) => ProposalApproval.Made("x"),
            control: _control);

        Approved("parent-session", "child");
        Approved("child-session", "grandchild");
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, recursive: true);

    [Test]
    public void Descendants_AreTheCardsOfApprovedProposals_AndTheirsInTurn_NeverTheCallerOrItsParent()
    {
        Assert.That(WorkstreamDescendants.Of("parent", _proposals.All), Is.EqualTo(new[] { "child", "grandchild" }));
        Assert.That(WorkstreamDescendants.Of("child", _proposals.All), Is.EqualTo(new[] { "grandchild" }));
    }

    [Test]
    public void Workstreams_ListsTheDescendants_WithTheirStateAndWhy()
    {
        _cards[1] = Card("child") with { StopLine = "✓ Done" };

        var text = Text(Call("workstreams", new JsonObject(), "parent-session"));

        Assert.That(text, Does.Contain("Child (child): not running; ✓ Done"));
        Assert.That(text, Does.Contain("Grandchild (grandchild): not running"));
        Assert.That(text, Does.Not.Contain("other"));
    }

    [Test]
    public void StartWorkstream_StartsADescendant_WithItsInstructionForThisStartOnly()
    {
        var result = Call("start_workstream", new JsonObject { ["card"] = "grandchild", ["instruction"] = "more work: the chips" }, "parent-session");

        Assert.That(IsError(result), Is.False);
        Assert.That(_control.Calls, Is.EqualTo(new[] { "Start grandchild more work: the chips by parent" }));
    }

    [TestCase("other", "is not one of your descendants")]
    [TestCase("owners", "is not one of your descendants")]
    [TestCase("nobody", "No card on the board is called nobody")]
    public void ACardThatIsNotADescendant_IsRefused_AndNothingIsDone(string card, string reason)
    {
        var result = Call("start_workstream", new JsonObject { ["card"] = card }, "parent-session");

        Assert.That(IsError(result), Is.True);
        Assert.That(Text(result), Does.Contain(reason));
        Assert.That(_control.Calls, Is.Empty);
    }

    [Test]
    public void TheCallersOwnParent_IsRefused()
    {
        var result = Call("stop_workstream", new JsonObject { ["card"] = "parent" }, "child-session");

        Assert.That(Text(result), Does.Contain("is not one of your descendants"));
        Assert.That(_control.Calls, Is.Empty);
    }

    [Test]
    public void ARunningArchivedBlockedOrOwnerPausedDescendant_IsNotStarted()
    {
        _cards[1] = Card("child", live: true);
        _cards[2] = Card("grandchild") with { Workstream = Card("grandchild").Workstream with { IsArchived = true } };

        Assert.That(Text(Call("start_workstream", new JsonObject { ["card"] = "child" }, "parent-session")), Is.EqualTo("Child is running."));
        Assert.That(Text(Call("start_workstream", new JsonObject { ["card"] = "grandchild" }, "parent-session")), Is.EqualTo("Grandchild is archived."));

        _cards[2] = Card("grandchild") with { Workstream = Card("grandchild").Workstream with { IsBlocked = true } };
        Assert.That(Text(Call("start_workstream", new JsonObject { ["card"] = "grandchild" }, "parent-session")), Does.Contain("is blocked"));

        _cards[2] = Card("grandchild") with { Workstream = Card("grandchild").Workstream with { IsPaused = true } };
        Assert.That(Text(Call("start_workstream", new JsonObject { ["card"] = "grandchild" }, "parent-session")), Is.EqualTo("Grandchild is paused by the user."));
        Assert.That(_control.Calls, Is.Empty);
    }

    [Test]
    public void StopWorkstream_StopsARunningDescendant_AndRefusesOneWithNoSession()
    {
        _cards[1] = Card("child", live: true);

        Assert.That(IsError(Call("stop_workstream", new JsonObject { ["card"] = "Child" }, "parent-session")), Is.False, "by title too");
        Assert.That(Text(Call("stop_workstream", new JsonObject { ["card"] = "grandchild" }, "parent-session")), Is.EqualTo("Grandchild has no session to stop."));
        Assert.That(_control.Calls, Is.EqualTo(new[] { "Stop child by parent" }));
    }

    [Test]
    public void PauseAndResume_AreTheCallersOwn()
    {
        Call("pause_workstream", new JsonObject { ["card"] = "grandchild", ["reason"] = "waiting for the spec" }, "parent-session");
        _cards[2] = Card("grandchild") with { Workstream = Card("grandchild").Workstream with { IsPaused = true, PausedBy = "parent" } };

        Assert.That(Text(Call("start_workstream", new JsonObject { ["card"] = "grandchild" }, "parent-session")), Does.Contain("resume it instead"));
        Assert.That(Text(Call("resume_workstream", new JsonObject { ["card"] = "grandchild" }, "child-session")), Is.EqualTo("Grandchild is not paused by you."));
        Assert.That(IsError(Call("resume_workstream", new JsonObject { ["card"] = "grandchild", ["instruction"] = "go on" }, "parent-session")), Is.False);
        Assert.That(_control.Calls, Is.EqualTo(new[] { "Pause grandchild waiting for the spec by parent", "Resume grandchild go on by parent" }));
    }

    [Test]
    public void TheActingTools_PassThePersonInTheTab_AndOnlyTheListIsPreApproved()
    {
        Assert.That(TheseusMcp.PreApproved, Does.Contain("workstreams"));
        Assert.That(TheseusMcp.PreApproved, Has.None.AnyOf("start_workstream", "stop_workstream", "pause_workstream", "resume_workstream"));
    }

    private void Approved(string session, string child)
    {
        var brief = Path.Combine(_directory, "proposals", child, "BRIEF.md");
        Directory.CreateDirectory(Path.GetDirectoryName(brief)!);
        File.WriteAllText(brief, "Do the work.");
        Call("propose_workstream", new JsonObject
        {
            ["title"] = child,
            ["brief"] = brief,
            ["done_criteria"] = "Done.",
            ["report_back"] = true,
        }, session);
        _proposals.Approve(_proposals.Pending.Single().Id, child);
    }

    private BoardSnapshotCard Card(string key, bool live = false) =>
        new(new Workstream(key, char.ToUpperInvariant(key[0]) + key[1..], "#208DD7", false, _directory, [], "Carry on.", 0, "New",
                false, false, false, null, [], null, Nine),
            Path.Combine(_directory, $"HANDOFF-{key}.md"), live ? SessionLifecycle.Running : SessionLifecycle.NotStarted, live, false, null, false);

    private JsonNode Call(string tool, JsonObject arguments, string session)
    {
        var message = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 1,
            ["method"] = "tools/call",
            ["params"] = new JsonObject { ["name"] = tool, ["arguments"] = arguments.DeepClone() },
        };

        return JsonNode.Parse(_handler.Handle(session, message.ToJsonString(), null).Body!)!["result"]!;
    }

    private static string Text(JsonNode result) => result["content"]![0]!["text"]!.GetValue<string>();

    private static bool IsError(JsonNode result) => result["isError"]!.GetValue<bool>();

    private sealed class FixedClock(DateTimeOffset now) : ISystemClock
    {
        public DateTimeOffset UtcNow => now;
    }

    private sealed class RecordingControl : IWorkstreamControl
    {
        public List<string> Calls { get; } = [];

        public string? Start(string key, string? instruction, string caller) => Record($"Start {key} {instruction} by {caller}");

        public string? Stop(string key, string caller) => Record($"Stop {key} by {caller}");

        public string? Pause(string key, string caller, string? reason) => Record($"Pause {key} {reason} by {caller}");

        public string? Resume(string key, string? instruction, string caller) => Record($"Resume {key} {instruction} by {caller}");

        private string? Record(string call)
        {
            Calls.Add(call.Replace("  ", " ", StringComparison.Ordinal));

            return null;
        }
    }

    [TestCase("start_workstream")]
    [TestCase("stop_workstream")]
    [TestCase("pause_workstream")]
    [TestCase("resume_workstream")]
    public void EveryActingTool_RefusesTheOwnersCard_AndAnotherProjectsCard(string tool)
    {
        _cards[3] = Card("other") with { Workstream = Card("other").Workstream with { Project = "another-project" } };

        Assert.That(Text(Call(tool, new JsonObject { ["card"] = "owners" }, "parent-session")), Does.Contain("is not one of your descendants"));
        Assert.That(Text(Call(tool, new JsonObject { ["card"] = "other" }, "parent-session")), Does.Contain("is not one of your descendants"));
        Assert.That(_control.Calls, Is.Empty);
    }

    [Test]
    public void APause_NeverTakesOverTheOwnersPause_OrAnotherCards()
    {
        _cards[2] = Card("grandchild") with { Workstream = Card("grandchild").Workstream with { IsPaused = true } };

        Assert.That(Text(Call("pause_workstream", new JsonObject { ["card"] = "grandchild" }, "parent-session")), Is.EqualTo("Grandchild is paused by the user."));

        _cards[2] = Card("grandchild") with { Workstream = Card("grandchild").Workstream with { IsPaused = true, PausedBy = "child" } };

        Assert.That(Text(Call("pause_workstream", new JsonObject { ["card"] = "grandchild" }, "parent-session")), Is.EqualTo("Grandchild is paused by Child."));
        Assert.That(_control.Calls, Is.Empty, "review of PR 3, finding 1");
    }

    [Test]
    public void ResumeAndPause_RefuseARunningArchivedOrBlockedDescendant()
    {
        _cards[2] = Card("grandchild", live: true) with { Workstream = Card("grandchild").Workstream with { IsPaused = true, PausedBy = "parent" } };
        Assert.That(Text(Call("resume_workstream", new JsonObject { ["card"] = "grandchild" }, "parent-session")), Is.EqualTo("Grandchild is running."));

        _cards[2] = Card("grandchild") with { Workstream = Card("grandchild").Workstream with { IsPaused = true, PausedBy = "parent", IsBlocked = true } };
        Assert.That(Text(Call("resume_workstream", new JsonObject { ["card"] = "grandchild" }, "parent-session")), Does.Contain("is blocked"));

        _cards[2] = Card("grandchild") with { Workstream = Card("grandchild").Workstream with { IsArchived = true } };
        Assert.That(Text(Call("resume_workstream", new JsonObject { ["card"] = "grandchild" }, "parent-session")), Is.EqualTo("Grandchild is archived."));
        Assert.That(Text(Call("pause_workstream", new JsonObject { ["card"] = "grandchild" }, "parent-session")), Is.EqualTo("Grandchild is archived."));
        Assert.That(_control.Calls, Is.Empty);
    }

    [Test]
    public void TwoDescendantsOfOneTitle_AreNotGuessedBetween()
    {
        _cards[1] = Card("child") with { Workstream = Card("child").Workstream with { Title = "Twin" } };
        _cards[2] = Card("grandchild") with { Workstream = Card("grandchild").Workstream with { Title = "Twin" } };

        Assert.That(Text(Call("start_workstream", new JsonObject { ["card"] = "Twin" }, "parent-session")),
            Is.EqualTo("More than one card you may act on is called Twin: name one by its key (child, grandchild)."));
        Assert.That(IsError(Call("start_workstream", new JsonObject { ["card"] = "grandchild" }, "parent-session")), Is.False, "by its key");
    }

    [Test]
    public void AProposalsKeys_StayTaken_SoADeletedDescendantsKeyIsNeverGivenToANewCard()
    {
        var proposals = new Theseus.App.ViewModels.ProposalsViewModel(_proposals, () => [], _ => { });

        Assert.That(proposals.TakenKeys, Is.SupersetOf(new[] { "parent", "child", "grandchild" }), "review of PR 3, finding 2");
    }
}
