using System.Text.Json.Nodes;
using Theseus.Core.Abstractions;
using Theseus.Core.Attention;
using Theseus.Core.Domain;
using Theseus.Core.Links;
using Theseus.Core.Mcp;
using Theseus.Core.Proposals;

namespace Theseus.Tests.Mcp;

[TestFixture]
public class McpProposalToolsTests
{
    private static readonly DateTimeOffset Nine = new(2026, 10, 29, 9, 0, 0, TimeSpan.Zero);

    private string _directory = string.Empty;
    private string _brief = string.Empty;
    private LinkBook _links = null!;
    private ProposalBook _proposals = null!;
    private McpHandler _handler = null!;
    private List<(WorkstreamProposal Proposal, bool StartNow)> _approved = [];
    private ProposalApproval _approval = ProposalApproval.Made("cache-spike");

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-mcp-proposals-{Guid.NewGuid():N}");
        _brief = Path.Combine(_directory, "proposals", "cache-spike", "BRIEF.md");
        Directory.CreateDirectory(Path.GetDirectoryName(_brief)!);
        File.WriteAllText(_brief, "Find out whether the token cache can be dropped.");

        _links = new LinkBook(new LinkStateStore(Path.Combine(_directory, "links.json")), new FixedClock(Nine));
        _proposals = new ProposalBook(new ProposalStateStore(Path.Combine(_directory, "proposals.json")), new FixedClock(Nine));

        var board = new BoardSnapshot([Card("auth-research"), Card("token-spike")], Nine);

        _handler = new McpHandler(
            new WorkLog(Path.Combine(_directory, "work.jsonl")),
            () => [],
            sessionId => sessionId switch
            {
                "research-session" => new McpSession("research-session", "auth-research", "Auth research"),
                "spike-session" => new McpSession("spike-session", "token-spike", "Token spike"),
                _ => null,
            },
            () => Nine,
            TimeZoneInfo.Utc,
            links: _links,
            board: () => board,
            paths: new SessionPathRule(Path.Combine(_directory, "plans"), Path.Combine(_directory, "data")),
            proposals: _proposals,
            approve: (proposal, startNow) =>
            {
                _approved.Add((proposal, startNow));

                return _approval;
            });
        _approved = [];
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
    public void ProposeWorkstream_FilesAPendingProposalAndCreatesNothingElse()
    {
        var result = Call("propose_workstream", Proposal(), "research-session");

        Assert.That(result["isError"]!.GetValue<bool>(), Is.False);
        Assert.That(Text(result), Does.Contain("waiting for the user on the board"));
        Assert.That(_proposals.Pending.Single().RequesterKey, Is.EqualTo("auth-research"));
        Assert.That(_proposals.Pending.Single().SessionId, Is.EqualTo("research-session"));
        Assert.That(_links.All, Is.Empty, "no link exists until Nigel approves");
        Assert.That(Directory.Exists(Path.Combine(_directory, "plans")), Is.False, "no plan file is written");
    }

    [Test]
    public void ProposeWorkstream_SentTwiceIsOneProposal()
    {
        Call("propose_workstream", Proposal(), "research-session");
        var again = Call("propose_workstream", Proposal(), "research-session");

        Assert.That(Text(again), Does.Contain("updated rather than filed twice"));
        Assert.That(_proposals.Pending, Has.Count.EqualTo(1));
    }

    [Test]
    public void ProposeWorkstream_NeedsReportBackSaidOutright()
    {
        var arguments = Proposal();
        arguments.Remove("report_back");

        var result = Call("propose_workstream", arguments, "research-session");

        Assert.That(result["isError"]!.GetValue<bool>(), Is.True);
        Assert.That(Text(result), Does.Contain("report_back is required"));
    }

    [Test]
    public void ProposeWorkstream_PassesOnTheChecksRefusal()
    {
        var arguments = Proposal();
        arguments["brief"] = Path.Combine(_directory, "missing.md");

        var result = Call("propose_workstream", arguments, "research-session");

        Assert.That(result["isError"]!.GetValue<bool>(), Is.True);
        Assert.That(Text(result), Does.Contain("does not exist"));
        Assert.That(_proposals.All, Is.Empty);
    }

    [Test]
    public void ProposalStatus_ShowsOnlyTheCallersProposalsAndWhatWasDecided()
    {
        Call("propose_workstream", Proposal(), "research-session");
        var id = _proposals.Pending.Single().Id;
        _proposals.Reject(id, "Fold it into the research.");

        var mine = JsonNode.Parse(Text(Call("proposal_status", new JsonObject(), "research-session")))!;
        var theirs = JsonNode.Parse(Text(Call("proposal_status", new JsonObject(), "spike-session")))!;

        Assert.That(mine["proposals"]![0]!["status"]!.GetValue<string>(), Is.EqualTo("rejected"));
        Assert.That(mine["proposals"]![0]!["note"]!.GetValue<string>(), Is.EqualTo("Fold it into the research."));
        Assert.That(theirs["proposals"]!.AsArray(), Is.Empty);
    }

    [Test]
    public void WithdrawProposal_TakesBackOnlyTheCallersOwn()
    {
        Call("propose_workstream", Proposal(), "research-session");
        var id = _proposals.Pending.Single().Id;

        var stranger = Call("withdraw_proposal", new JsonObject { ["proposal_id"] = id }, "spike-session");
        var owner = Call("withdraw_proposal", new JsonObject { ["proposal_id"] = id }, "research-session");

        Assert.That(stranger["isError"]!.GetValue<bool>(), Is.True);
        Assert.That(owner["isError"]!.GetValue<bool>(), Is.False);
        Assert.That(_proposals.Find(id)!.Status, Is.EqualTo(ProposalStatus.Withdrawn));
    }

    [Test]
    public void NoOtherToolDecidesAProposal()
    {
        Call("propose_workstream", Proposal(), "research-session");

        foreach (var tool in TheseusMcp.ToolNames.Where(tool => tool is not ("withdraw_proposal" or "approve_proposal" or "reject_proposal")))
        {
            Call(tool, new JsonObject { ["proposal_id"] = _proposals.Pending.Single().Id }, "research-session");
        }

        Assert.That(_proposals.Pending.Single().Status, Is.EqualTo(ProposalStatus.Pending));
        Assert.That(_approved, Is.Empty);
    }

    [Test]
    public void ProposalStatus_GivesWhatTheUserNeedsToReviewIt()
    {
        var arguments = Proposal();
        arguments["done_criteria"] = "A verdict on dropping the token cache; push it to main.";
        Call("propose_workstream", arguments, "research-session");

        var entry = JsonNode.Parse(Text(Call("proposal_status", new JsonObject(), "research-session")))!["proposals"]![0]!;

        Assert.That(entry["brief"]!.GetValue<string>(), Is.EqualTo(_brief));
        Assert.That(entry["doneCriteria"]!.GetValue<string>(), Is.EqualTo("A verdict on dropping the token cache; push it to main."));
        Assert.That(entry["reportBack"]!.GetValue<bool>(), Is.True);
        Assert.That(entry["reason"]!.GetValue<string>(), Is.EqualTo("The cache is in the way."));
        Assert.That(entry["warnings"]!.AsArray().Select(warning => warning!.GetValue<string>()),
            Has.Some.Contains("push"), "the warning the board would show");
    }

    [Test]
    public void ApproveProposal_CreatesTheCardThroughTheBoard()
    {
        Call("propose_workstream", Proposal(), "research-session");
        var id = _proposals.Pending.Single().Id;

        var result = Call("approve_proposal", new JsonObject { ["proposal_id"] = id, ["start_now"] = true }, "research-session");

        Assert.That(result["isError"]!.GetValue<bool>(), Is.False, Text(result));
        Assert.That(_approved.Single().Proposal.Id, Is.EqualTo(id));
        Assert.That(_approved.Single().StartNow, Is.True);
        Assert.That(Text(result), Does.Contain("cache-spike"));
    }

    [Test]
    public void ApproveProposal_LeavesItWaitingWhenTheBoardRefuses()
    {
        Call("propose_workstream", Proposal(), "research-session");
        _approval = ProposalApproval.Refused("The new workstream dialog is open on the board.");

        var result = Call("approve_proposal", new JsonObject { ["proposal_id"] = _proposals.Pending.Single().Id }, "research-session");

        Assert.That(result["isError"]!.GetValue<bool>(), Is.True);
        Assert.That(Text(result), Does.Contain("dialog is open"));
        Assert.That(_approved.Single().StartNow, Is.False, "a card starts only when asked");
        Assert.That(_proposals.Pending, Has.Count.EqualTo(1));
    }

    [Test]
    public void ApproveAndReject_TouchOnlyTheCallersOwnPendingProposals()
    {
        Call("propose_workstream", Proposal(), "research-session");
        var id = _proposals.Pending.Single().Id;

        var approve = Call("approve_proposal", new JsonObject { ["proposal_id"] = id }, "spike-session");
        var reject = Call("reject_proposal", new JsonObject { ["proposal_id"] = id }, "spike-session");

        Assert.That(approve["isError"]!.GetValue<bool>(), Is.True);
        Assert.That(reject["isError"]!.GetValue<bool>(), Is.True);
        Assert.That(Text(reject), Does.Contain("another workstream"));
        Assert.That(_approved, Is.Empty);
        Assert.That(_proposals.Find(id)!.Status, Is.EqualTo(ProposalStatus.Pending));
    }

    [Test]
    public void RejectProposal_KeepsTheUsersNote()
    {
        Call("propose_workstream", Proposal(), "research-session");
        var id = _proposals.Pending.Single().Id;

        var result = Call("reject_proposal", new JsonObject { ["proposal_id"] = id, ["note"] = "Fold it into the research." },
            "research-session");
        var again = Call("approve_proposal", new JsonObject { ["proposal_id"] = id }, "research-session");

        Assert.That(result["isError"]!.GetValue<bool>(), Is.False);
        Assert.That(_proposals.Find(id)!.DecisionNote, Is.EqualTo("Fold it into the research."));
        Assert.That(Text(again), Does.Contain("already rejected"));
        Assert.That(_approved, Is.Empty);
    }

    private JsonObject Proposal() => new()
    {
        ["title"] = "Cache spike",
        ["brief"] = _brief,
        ["done_criteria"] = "A verdict on dropping the token cache.",
        ["report_back"] = true,
        ["reason"] = "The cache is in the way.",
    };

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

    private BoardSnapshotCard Card(string key) =>
        new(new Workstream(key, key, "#208DD7", false, _directory, [], "Carry on.", 0, "New", false, false,
                false, null, [], null, Nine),
            Path.Combine(_directory, $"HANDOFF-{key}.md"), SessionLifecycle.Running, true, false, null, false);

    private sealed class FixedClock(DateTimeOffset now) : ISystemClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
