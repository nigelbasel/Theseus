using System.Text.Json.Nodes;
using Theseus.Core.Abstractions;
using Theseus.Core.Attention;
using Theseus.Core.Domain;
using Theseus.Core.Links;
using Theseus.Core.Mcp;
using Theseus.Core.Projects;
using Theseus.Core.Proposals;

namespace Theseus.Tests.Mcp;

/// <summary>
/// A project's sessions run the project (sessions-run-the-project items 1 to 11): propose_workstream makes the card at
/// once under an objective, uncapped; the four acting tools and workstreams reach every card of the caller's project but
/// its own, by membership alone, never another project's or an ad hoc card; any session's pause may be taken over.
/// </summary>
[TestFixture]
public class McpProjectWorkstreamToolsTests
{
    private const string Project = "theseus";
    private static readonly DateTimeOffset Nine = new(2026, 11, 23, 9, 0, 0, TimeSpan.Zero);
    private string _directory = string.Empty;
    private ProposalBook _proposals = null!;
    private StartRequestBook _requests = null!;
    private McpHandler _handler = null!;
    private RecordingControl _control = null!;
    private List<BoardSnapshotCard> _cards = [];
    private List<string> _created = [];
    private string? _createRefusal;
    private string? _shortfall;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-mcp-project-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _proposals = new ProposalBook(new ProposalStateStore(Path.Combine(_directory, "proposals.json")), new FixedClock(Nine));
        _control = new RecordingControl();
        _requests = new StartRequestBook(new StartRequestStore(Path.Combine(_directory, "start-requests.json")), new FixedClock(Nine));
        _created = [];
        _createRefusal = null;
        _shortfall = null;
        _cards = [Card("lead", Project), Card("sibling", Project), Card("elsewhere", "another-project"), Card("adhoc", null)];
        _handler = new McpHandler(
            new WorkLog(Path.Combine(_directory, "work.jsonl")),
            () => [],
            sessionId => sessionId switch
            {
                "lead-session" => new McpSession("lead-session", "lead", "Lead"),
                "adhoc-session" => new McpSession("adhoc-session", "adhoc", "Adhoc"),
                _ => null,
            },
            () => Nine,
            TimeZoneInfo.Utc,
            links: new LinkBook(new LinkStateStore(Path.Combine(_directory, "links.json")), new FixedClock(Nine)),
            board: () => new BoardSnapshot(_cards, Nine),
            paths: new SessionPathRule(Path.Combine(_directory, "plans"), Path.Combine(_directory, "data")),
            proposals: _proposals,
            approve: (_, _) => ProposalApproval.Made("approved"),
            control: _control,
            startRequests: _requests,
            create: (proposal, startNow) =>
            {
                if (_createRefusal is { } refusal)
                {
                    return ProposalApproval.Refused(refusal);
                }

                var key = proposal.Request.Slug!;
                _created.Add($"{key} start:{startNow}");
                _proposals.Create(proposal.Id, key);
                _cards.Add(Card(key, Project));

                return ProposalApproval.Made(key) with { Shortfall = _shortfall };
            });
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, recursive: true);

    [Test]
    public void ProposeInAProject_CreatesTheCardAtOnce_UnderItsObjective()
    {
        var result = Propose("New piece", objective: "1.6", slug: "new-piece");

        Assert.Multiple(() =>
        {
            Assert.That(IsError(result), Is.False, Text(result));
            Assert.That(Text(result), Does.StartWith("Created New piece as new-piece under objective 1.6 of theseus"));
            Assert.That(Text(result), Does.Contain("It does not start until you call start_workstream."));
            Assert.That(_created, Is.EqualTo(new[] { "new-piece start:False" }));
            Assert.That(_proposals.Pending, Is.Empty);
        });
    }

    [Test]
    public void StartNow_StartsTheCreatedCard()
    {
        var result = Propose("New piece", objective: "1.6", slug: "new-piece", startNow: true);

        Assert.Multiple(() =>
        {
            Assert.That(Text(result), Does.Contain("Its session is starting."));
            Assert.That(_created, Is.EqualTo(new[] { "new-piece start:True" }));
        });
    }

    [TestCase(null, "new-piece", "name the objective")]
    [TestCase("1.6", null, "name the new workstream's records folder (slug)")]
    public void ProposeInAProject_WithoutAnObjectiveOrSlug_IsRefused_AndNothingIsMade(string? objective, string? slug, string reason)
    {
        var result = Propose("New piece", objective, slug);

        Assert.Multiple(() =>
        {
            Assert.That(IsError(result), Is.True);
            Assert.That(Text(result), Does.Contain(reason));
            Assert.That(_created, Is.Empty);
            Assert.That(_proposals.All, Is.Empty);
        });
    }

    [Test]
    public void ACardThatCannotBeMade_LeavesNothingWaiting()
    {
        _createRefusal = "It cannot be approved yet: It names objective 9.9, which the project file does not have.";

        var result = Propose("New piece", objective: "9.9", slug: "new-piece");

        Assert.Multiple(() =>
        {
            Assert.That(Text(result), Is.EqualTo("Nothing was made: It cannot be approved yet: It names objective 9.9, which the project file does not have."));
            Assert.That(_proposals.Pending, Is.Empty);
            Assert.That(_proposals.All.Single().Status, Is.EqualTo(ProposalStatus.Withdrawn));
        });
    }

    [Test]
    public void ACardMadeWithoutItsRow_IsSaidToBeAdHoc_NotClaimedInTheProject()
    {
        _shortfall = "Created New piece, not in theseus: theseus's project.md changed since Theseus read it";

        var text = Text(Propose("New piece", objective: "1.6", slug: "new-piece"));

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("but not in theseus"));
            Assert.That(text, Does.Contain("It is ad hoc until the user adds it to the project."));
            Assert.That(text, Does.Not.Contain("its row in the project file"));
        });
    }

    [Test]
    public void AFailedCreate_LeavesAnEarlierWaitingProposalOfTheSameTitleAlone()
    {
        var brief = Path.Combine(_directory, "work", "new-piece", "BRIEF.md");
        Directory.CreateDirectory(Path.GetDirectoryName(brief)!);
        File.WriteAllText(brief, "Do the work.");
        var earlier = _proposals.Propose("lead", "lead-session", new ProposalRequest("New piece", brief, false, null, "Done.", [], null),
            new ProposalCheckResult(null, [])).Proposal!;
        _createRefusal = "It cannot be approved yet: the file changed.";

        Propose("New piece", objective: "1.6", slug: "new-piece");

        Assert.That(_proposals.Find(earlier.Id)!.IsPending, Is.True, "review of PR 1, finding 3");
    }

    [Test]
    public void ACreateThatThrows_LeavesNothingWaiting()
    {
        var handler = new McpHandler(
            new WorkLog(Path.Combine(_directory, "work2.jsonl")), () => [],
            _ => new McpSession("lead-session", "lead", "Lead"), () => Nine, TimeZoneInfo.Utc,
            links: new LinkBook(new LinkStateStore(Path.Combine(_directory, "links2.json")), new FixedClock(Nine)),
            board: () => new BoardSnapshot(_cards, Nine),
            paths: new SessionPathRule(Path.Combine(_directory, "plans"), Path.Combine(_directory, "data")),
            proposals: _proposals, control: _control,
            create: (_, _) => throw new IOException("the plan folder is locked"));
        var brief = Path.Combine(_directory, "work", "new-piece", "BRIEF.md");
        Directory.CreateDirectory(Path.GetDirectoryName(brief)!);
        File.WriteAllText(brief, "Do the work.");
        var message = new JsonObject
        {
            ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "tools/call",
            ["params"] = new JsonObject
            {
                ["name"] = "propose_workstream",
                ["arguments"] = new JsonObject
                {
                    ["title"] = "New piece", ["brief"] = brief, ["done_criteria"] = "Done.", ["report_back"] = false,
                    ["objective"] = "1.6", ["slug"] = "new-piece",
                },
            },
        };

        var result = JsonNode.Parse(handler.Handle("lead-session", message.ToJsonString(), null).Body!)!["result"]!;

        Assert.Multiple(() =>
        {
            Assert.That(Text(result), Is.EqualTo("Nothing was made: Theseus could not make it: the plan folder is locked"));
            Assert.That(_proposals.Pending, Is.Empty);
        });
    }

    [Test]
    public void AProjectsSession_IsNotCapped()
    {
        for (var each = 1; each <= ProposalBook.MaximumPending + 2; each++)
        {
            Assert.That(IsError(Propose($"Piece {each}", "1.6", $"piece-{each}")), Is.False);
        }

        Assert.That(_created, Has.Count.EqualTo(ProposalBook.MaximumPending + 2));
    }

    [Test]
    public void ProposalStatus_SaysCreated_ForACardItsSessionMade()
    {
        Propose("New piece", objective: "1.6", slug: "new-piece");

        var status = Text(Call("proposal_status", new JsonObject(), "lead-session"));

        Assert.That(status, Does.Contain("\"status\": \"created\""));
    }

    [Test]
    public void AnAdHocSession_StillProposes_AndWaitsForTheUser()
    {
        var result = Propose("Ad hoc piece", null, null, session: "adhoc-session");

        Assert.Multiple(() =>
        {
            Assert.That(Text(result), Does.Contain("is waiting for the user on the board"));
            Assert.That(_created, Is.Empty);
            Assert.That(_proposals.Pending, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void Workstreams_ListsEveryOtherCardOfTheProject_WithWhoMadeAndPausedIt()
    {
        Propose("New piece", objective: "1.6", slug: "new-piece");
        _cards[1] = Card("sibling", Project) with { Workstream = Card("sibling", Project).Workstream with { IsPaused = true, PausedBy = "new-piece" } };

        var text = Text(Call("workstreams", new JsonObject(), "lead-session"));

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("Sibling (sibling): not running; paused by New-piece"));
            Assert.That(text, Does.Contain("New-piece (new-piece): not running; created by Lead; yours"));
            Assert.That(text, Does.Not.Contain("(lead)"), "never the caller's own card");
            Assert.That(text, Does.Not.Contain("elsewhere").And.Not.Contain("adhoc"));
        });
    }

    [TestCase("start_workstream")]
    [TestCase("stop_workstream")]
    [TestCase("pause_workstream")]
    public void AnyCardOfTheProject_MayBeActedOn_NotOnlyDescendants(string tool)
    {
        _cards[1] = Card("sibling", Project, live: tool == "stop_workstream");

        var result = Call(tool, new JsonObject { ["card"] = "sibling" }, "lead-session");

        Assert.That(IsError(result), Is.False, Text(result));
        Assert.That(_control.Calls, Has.Count.EqualTo(1));
    }

    [TestCase("elsewhere", "is not a card of theseus")]
    [TestCase("adhoc", "is not a card of theseus")]
    public void AnotherProjectsCard_OrAnAdHocCard_IsRefused(string card, string reason)
    {
        foreach (var tool in new[] { "start_workstream", "stop_workstream", "pause_workstream", "resume_workstream" })
        {
            var result = Call(tool, new JsonObject { ["card"] = card }, "lead-session");

            Assert.That(Text(result), Does.Contain(reason), tool);
        }

        Assert.That(_control.Calls, Is.Empty);
    }

    [Test]
    public void TheCallersOwnCard_IsRefused()
    {
        var result = Call("stop_workstream", new JsonObject { ["card"] = "lead" }, "lead-session");

        Assert.That(Text(result), Is.EqualTo("That is this workstream's own card: to stop, write your handover and end your turn."));
        Assert.That(_control.Calls, Is.Empty);
    }

    [Test]
    public void ACardMovedOutOfTheProject_IsRefusedAtOnce()
    {
        _cards[1] = Card("sibling", null);

        Assert.That(Text(Call("start_workstream", new JsonObject { ["card"] = "sibling" }, "lead-session")), Does.Contain("is not a card of theseus"));
    }

    [Test]
    public void AnotherSessionsPause_MayBeClearedOrTakenOver_ButNotTheUsers()
    {
        var paused = Card("sibling", Project).Workstream with { IsPaused = true, PausedBy = "someone" };
        _cards[1] = Card("sibling", Project) with { Workstream = paused };

        Assert.That(Text(Call("start_workstream", new JsonObject { ["card"] = "sibling" }, "lead-session")), Does.Contain("resume it instead"));
        Assert.That(IsError(Call("pause_workstream", new JsonObject { ["card"] = "sibling", ["reason"] = "mine now" }, "lead-session")), Is.False);
        Assert.That(IsError(Call("resume_workstream", new JsonObject { ["card"] = "sibling" }, "lead-session")), Is.False);

        _cards[1] = Card("sibling", Project) with { Workstream = paused with { PausedBy = null } };

        Assert.Multiple(() =>
        {
            Assert.That(Text(Call("resume_workstream", new JsonObject { ["card"] = "sibling" }, "lead-session")),
                Does.StartWith("Sibling is paused by the user, so it is not started"), "a request, not a resume (PR 2)");
            Assert.That(Text(Call("pause_workstream", new JsonObject { ["card"] = "sibling" }, "lead-session")), Is.EqualTo("Sibling is paused by the user."));
            Assert.That(_control.Calls, Is.EqualTo(new[] { "Pause sibling mine now by lead", "Resume sibling by lead" }));
        });
    }

    [TestCase("start_workstream")]
    [TestCase("resume_workstream")]
    public void StartingACardTheUserPaused_FilesARequest_AndStartsNothing(string tool)
    {
        _cards[1] = Card("sibling", Project) with { Workstream = Card("sibling", Project).Workstream with { IsPaused = true } };

        var result = Call(tool, new JsonObject { ["card"] = "sibling", ["instruction"] = "go on", ["reason"] = "its spec is accepted" },
            "lead-session");
        var request = _requests.Pending.Single();

        Assert.Multiple(() =>
        {
            Assert.That(IsError(result), Is.False);
            Assert.That(Text(result), Does.StartWith($"Sibling is paused by the user, so it is not started: your request ({request.Id}) waits for them"));
            Assert.That((request.CardKey, request.RequesterKey, request.Instruction, request.Reason),
                Is.EqualTo(("sibling", "lead", "go on", "its spec is accepted")));
            Assert.That(_control.Calls, Is.Empty);
        });
    }

    [Test]
    public void AskingAgain_UpdatesTheWaitingRequest()
    {
        _cards[1] = Card("sibling", Project) with { Workstream = Card("sibling", Project).Workstream with { IsPaused = true } };
        Call("start_workstream", new JsonObject { ["card"] = "sibling" }, "lead-session");

        var again = Text(Call("start_workstream", new JsonObject { ["card"] = "sibling", ["reason"] = "still waiting" }, "lead-session"));

        Assert.Multiple(() =>
        {
            Assert.That(again, Does.Contain("was already waiting and is updated"));
            Assert.That(_requests.Pending.Single().Reason, Is.EqualTo("still waiting"));
        });
    }

    [Test]
    public void TheUsersPause_IsNeverTakenOver_NorARunningOrBlockedCardRequested()
    {
        var paused = Card("sibling", Project).Workstream with { IsPaused = true };
        _cards[1] = Card("sibling", Project) with { Workstream = paused };

        Assert.That(Text(Call("pause_workstream", new JsonObject { ["card"] = "sibling" }, "lead-session")), Is.EqualTo("Sibling is paused by the user."));

        _cards[1] = Card("sibling", Project) with { Workstream = paused with { IsBlocked = true } };

        Assert.That(Text(Call("start_workstream", new JsonObject { ["card"] = "sibling" }, "lead-session")), Does.Contain("is blocked"));
        Assert.That(_requests.Pending, Is.Empty);
    }

    [Test]
    public void AnAdHocSession_IsStillRefused_TheUsersPausedCard()
    {
        _cards[3] = Card("adhoc", null);
        _proposals.Approve(_proposals.Propose("adhoc", "adhoc-session", new ProposalRequest("Kid", "b.md", false, null, "d", [], null),
            new ProposalCheckResult(null, [])).Proposal!.Id, "kid");
        _cards.Add(Card("kid", null) with { Workstream = Card("kid", null).Workstream with { IsPaused = true } });

        Assert.That(Text(Call("start_workstream", new JsonObject { ["card"] = "kid" }, "adhoc-session")), Is.EqualTo("Kid is paused by the user."));
        Assert.That(_requests.Pending, Is.Empty);
    }

    [Test]
    public void ResumingACardNobodyPaused_SaysSo()
    {
        Assert.That(Text(Call("resume_workstream", new JsonObject { ["card"] = "sibling" }, "lead-session")), Is.EqualTo("Sibling is not paused."));
    }

    [Test]
    public void TheInstructions_SayWhatAProjectsSessionMayDo_AndToCatchUpOnStarting()
    {
        Assert.Multiple(() =>
        {
            Assert.That(McpHandler.Instructions, Does.Contain("In a project, propose_workstream creates the card at once"));
            Assert.That(McpHandler.Instructions, Does.Contain("in a project, every card of the project but your own"));
            Assert.That(McpHandler.Instructions, Does.Contain("Call events() when you start"));
            Assert.That(McpHandler.Instructions, Does.Contain("starting a card the user paused waits for their Allow"));
            Assert.That(McpHandler.Instructions, Does.Not.Contain("Any other card is the user's"));
        });
    }

    private JsonNode Propose(string title, string? objective, string? slug, bool startNow = false, string session = "lead-session")
    {
        var brief = Path.Combine(_directory, "work", slug ?? "ad-hoc", "BRIEF.md");
        Directory.CreateDirectory(Path.GetDirectoryName(brief)!);
        File.WriteAllText(brief, "Do the work.");
        var arguments = new JsonObject
        {
            ["title"] = title,
            ["brief"] = brief,
            ["done_criteria"] = "Done.",
            ["report_back"] = false,
            ["start_now"] = startNow,
        };

        if (objective is not null)
        {
            arguments["objective"] = objective;
        }

        if (slug is not null)
        {
            arguments["slug"] = slug;
        }

        return Call("propose_workstream", arguments, session);
    }

    private BoardSnapshotCard Card(string key, string? project, bool live = false) =>
        new(new Workstream(key, char.ToUpperInvariant(key[0]) + key[1..], "#208DD7", false, _directory, [], "Carry on.", 0, "New",
                false, false, false, null, [], null, Nine) { Project = project },
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
}
