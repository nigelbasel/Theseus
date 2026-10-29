using Theseus.Core.Domain;
using Theseus.Core.Links;
using Theseus.Core.Proposals;

namespace Theseus.Tests.Proposals;

[TestFixture]
public class ProposalCheckTests
{
    private static readonly DateTimeOffset Nine = new(2026, 10, 29, 9, 0, 0, TimeSpan.Zero);

    private string _directory = string.Empty;
    private string _brief = string.Empty;
    private BoardSnapshot _board = BoardSnapshot.Empty;
    private SessionPathRule _paths = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-proposal-check-{Guid.NewGuid():N}");
        _brief = Path.Combine(_directory, "proposals", "cache-spike", "BRIEF.md");
        Directory.CreateDirectory(Path.GetDirectoryName(_brief)!);
        File.WriteAllText(_brief, "Find out whether the token cache can be dropped.");

        _board = new BoardSnapshot(
        [
            Card("auth-research", jiraKeys: ["APPS-1412"]),
            Card("token-spike"),
            Card("old-work", archived: true),
        ], Nine);
        _paths = new SessionPathRule(Path.Combine(_directory, "plans"), Path.Combine(_directory, "data"));
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
    public void Check_PassesAProposalWithAnExistingBriefInTheBriefFolder()
    {
        var result = Check(Request());

        Assert.That(result.Refusal, Is.Null);
        Assert.That(result.Warnings, Is.Empty);
    }

    [Test]
    public void Check_RefusesABriefThatDoesNotExist()
    {
        var result = Check(Request() with { Brief = Path.Combine(_directory, "missing.md") });

        Assert.That(result.Refusal, Does.Contain("does not exist"));
    }

    [Test]
    public void Check_RefusesARelativeBrief()
    {
        var result = Check(Request() with { Brief = @"proposals\cache-spike\BRIEF.md" });

        Assert.That(result.Refusal, Does.Contain("absolute path"));
    }

    [Test]
    public void Check_RefusesAHandoffAsTheBrief()
    {
        var handoff = Path.Combine(_directory, "HANDOFF-token-spike.md");
        File.WriteAllText(handoff, "Where I got to.");

        var result = Check(Request() with { Brief = handoff });

        Assert.That(result.Refusal, Does.Contain("handoff of Token spike"));
    }

    [Test]
    public void Check_RefusesAJiraKeyTheProposerDoesNotHold()
    {
        var result = Check(Request() with { JiraKeys = ["APPS-1412", "NDC-680"] });

        Assert.That(result.Refusal, Does.Contain("NDC-680 is not on Auth research's card"));
    }

    [Test]
    public void Check_AllowsAJiraKeyTheProposerHolds()
    {
        Assert.That(Check(Request() with { JiraKeys = ["apps-1412"] }).Refusal, Is.Null);
    }

    [Test]
    public void Check_RefusesAnArchivedProposer()
    {
        var result = ProposalCheck.Check(Request(), "old-work", _board, [], _paths, BriefFolder.DefaultName);

        Assert.That(result.Refusal, Does.Contain("archived"));
    }

    [Test]
    public void Check_LetsAChildProposeButNotAGrandchild()
    {
        var links = new List<Link>
        {
            Link("root", "auth-research"),
        };

        Assert.That(Check(Request(), links).Refusal, Is.Null, "a child may propose");

        links.Add(Link("grand-root", "root"));

        Assert.That(Check(Request(), links).Refusal, Does.Contain("two hand-overs below"));
    }

    [Test]
    public void Check_CountsRemovedHandOversSoRemovingALinkCannotResetTheDepth()
    {
        var links = new List<Link>
        {
            Link("root", "auth-research") with { RemovedUtc = Nine },
            Link("grand-root", "root") with { RemovedUtc = Nine },
        };

        Assert.That(Check(Request(), links).Refusal, Does.Contain("two hand-overs below"));
    }

    [Test]
    public void Check_WarnsWhenTheTitleMatchesAnExistingCard()
    {
        var result = Check(Request() with { Title = "token spike" });

        Assert.That(result.Refusal, Is.Null);
        Assert.That(result.Warnings, Has.Some.Contains("Looks like the existing card Token spike (token-spike)"));
    }

    [Test]
    public void Check_WarnsWhenTheBriefMentionsABranchOrAPush()
    {
        File.WriteAllText(_brief, "Make a branch for this, then git push it.");

        var result = Check(Request());

        Assert.That(result.Warnings, Has.Some.Contains("a branch or worktree"));
        Assert.That(result.Warnings, Has.Some.Contains("a push"));
    }

    [Test]
    public void Check_WarnsWhenTheDoneCriteriaAskForAJiraItemOrAPullRequest()
    {
        var result = Check(Request() with { DoneCriteria = "Raise a Jira ticket for the fix and open a PR." });

        Assert.That(result.Warnings, Has.Some.Contains("raising a Jira item"));
        Assert.That(result.Warnings, Has.Some.Contains("a pull request"));
    }

    [Test]
    public void Check_WarnsWhenTheBriefIsOutsideTheBriefFolder()
    {
        var elsewhere = Path.Combine(_directory, "notes", "BRIEF.md");
        Directory.CreateDirectory(Path.GetDirectoryName(elsewhere)!);
        File.WriteAllText(elsewhere, "Somewhere else.");

        var result = Check(Request() with { Brief = elsewhere });

        Assert.That(result.Warnings, Has.Some.Contains("outside the brief folder"));
    }

    [Test]
    public void Check_RequiresDoneCriteria()
    {
        Assert.That(Check(Request() with { DoneCriteria = " " }).Refusal, Does.Contain("done_criteria is required"));
    }

    private ProposalCheckResult Check(ProposalRequest request, IReadOnlyList<Link>? links = null) =>
        ProposalCheck.Check(request, "auth-research", _board, links ?? [], _paths, BriefFolder.DefaultName);

    private ProposalRequest Request() =>
        new("Cache spike", _brief, true, null, "A verdict on dropping the token cache.", [], "The cache is in the way.");

    private BoardSnapshotCard Card(string key, IReadOnlyList<string>? jiraKeys = null, bool archived = false) =>
        new(new Workstream(key, Title(key), "#208DD7", false, _directory, [], "Carry on.", 0, "New", false, false,
                archived, null, jiraKeys ?? [], null, Nine),
            Path.Combine(_directory, $"HANDOFF-{key}.md"), SessionLifecycle.NotStarted, false, false, null, false);

    private static Link Link(string from, string to) =>
        new($"lk-{from}", LinkKind.HandsOver, from, to, Nine, Theseus.Core.Links.Link.MadeOnTheBoard);

    private static string Title(string key)
    {
        var words = key.Replace('-', ' ');

        return char.ToUpperInvariant(words[0]) + words[1..];
    }
}
