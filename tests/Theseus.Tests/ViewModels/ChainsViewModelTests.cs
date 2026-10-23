using Theseus.App.Services;
using Theseus.App.ViewModels;
using Theseus.Core.Abstractions;
using Theseus.Core.Domain;
using Theseus.Core.Links;

namespace Theseus.Tests.ViewModels;

[TestFixture]
public class ChainsViewModelTests
{
    private static readonly DateTimeOffset Nine = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);

    private string _directory = string.Empty;
    private LinkBook _book = null!;
    private List<WorkstreamCardViewModel> _cards = null!;
    private int _changes;
    private ChainsViewModel _chains = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-chains-{Guid.NewGuid():N}");
        _book = new LinkBook(new LinkStateStore(Path.Combine(_directory, "links.json")), new FixedClock(Nine));
        _cards = [Card("auth-research"), Card("token-spike"), Card("auth-implementation")];
        _changes = 0;
        _chains = new ChainsViewModel(_book, () => _cards, () => BriefFolder.DefaultName, () => _changes++, _ => { });
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
    public void Update_GivesEveryCardItsLinkLine()
    {
        _book.Add(LinkKind.HandsOver, "token-spike", "auth-implementation", output: @"C:\work\REPORT.md");

        _chains.Update();

        Assert.That(Find("auth-implementation").LinkLine, Is.EqualTo("⧗ waits on token-spike · ↑ from token-spike"));
        Assert.That(Find("auth-research").LinkLine, Is.Empty);
    }

    [Test]
    public void Update_PutsAChildUnderTheCardThatHandedOverToIt()
    {
        _book.Add(LinkKind.HandsOver, "auth-research", "token-spike");

        _chains.Update();

        Assert.That(_chains.Roots.Single().Key, Is.EqualTo("auth-research"));
        Assert.That(_chains.Roots.Single().Children.Single().Key, Is.EqualTo("token-spike"));
    }

    [Test]
    public void Update_NestsEachStageUnderTheOneBefore()
    {
        _book.Add(LinkKind.HandsOver, "auth-research", "token-spike");
        _book.Add(LinkKind.HandsOver, "token-spike", "auth-implementation");

        _chains.Update();

        var spike = _chains.Roots.Single().Children.Single();
        Assert.That(spike.Key, Is.EqualTo("token-spike"));
        Assert.That(spike.Children.Single().Key, Is.EqualTo("auth-implementation"));
    }

    [Test]
    public void Update_ShowsAReportBackAsALineAndNeverAsNesting()
    {
        _book.Add(LinkKind.HandsOver, "auth-research", "token-spike");
        _book.Add(LinkKind.ReportsBack, "token-spike", "auth-research", output: @"C:\work\REPORT.md");

        _chains.Update();

        var spike = _chains.Roots.Single().Children.Single();
        Assert.That(spike.Children, Is.Empty);
        Assert.That(spike.ReportsBack, Is.EqualTo("↩ reports back to auth-research ⧗"));
    }

    [Test]
    public void Update_ListsAndCountsReadyCards()
    {
        var link = _book.Add(LinkKind.HandsOver, "token-spike", "auth-implementation", output: @"C:\work\REPORT.md").Link!;
        _book.Satisfy(link.Id, "read it");

        _chains.Update();

        Assert.That(_chains.Ready.Single().Key, Is.EqualTo("auth-implementation"));
        Assert.That(_chains.ButtonText, Is.EqualTo("Chains · 1 ready"));
        Assert.That(Find("auth-implementation").IsLinkReady, Is.True);
    }

    [Test]
    public void AddLink_MakesTheLinkChosenInTheDrawerAndRefreshesTheBoard()
    {
        _chains.Update();
        _chains.ShowFor("auth-implementation");
        _chains.Kind = LinkKindChoice.All.Single(choice => choice is { Kind: LinkKind.HandsOver, ThisCardIsFrom: false });
        _chains.Target = _chains.Targets.Single(target => target.Key == "token-spike");

        _chains.AddLinkCommand.Execute(null);

        Assert.That(_book.Active.Single(), Has.Property(nameof(Link.From)).EqualTo("token-spike")
            .And.Property(nameof(Link.To)).EqualTo("auth-implementation"));
        Assert.That(_chains.Output, Is.Empty, "the suggestion is cleared once the link is made");
        Assert.That(_changes, Is.EqualTo(1));
    }

    [Test]
    public void Kind_SuggestsTheUpstreamsReportUnderItsOwnWorkingDirectory()
    {
        _chains.ShowFor("auth-implementation");
        _chains.Kind = LinkKindChoice.All.Single(choice => choice is { Kind: LinkKind.HandsOver, ThisCardIsFrom: false });

        _chains.Target = _chains.Targets.Single(target => target.Key == "token-spike");

        Assert.That(_chains.Output, Is.EqualTo(BriefFolder.ReportPath(@"C:\work", BriefFolder.DefaultName, "token-spike")));
    }

    [Test]
    public void Target_ReplacesTheSuggestionWhenAnotherUpstreamIsChosen()
    {
        _chains.ShowFor("auth-implementation");
        _chains.Kind = LinkKindChoice.All.Single(choice => choice is { Kind: LinkKind.HandsOver, ThisCardIsFrom: false });
        _chains.Target = _chains.Targets.Single(target => target.Key == "token-spike");

        _chains.Target = _chains.Targets.Single(target => target.Key == "auth-research");

        Assert.That(_chains.Output, Is.EqualTo(BriefFolder.ReportPath(@"C:\work", BriefFolder.DefaultName, "auth-research")));
    }

    [Test]
    public void Target_KeepsADocumentTypedByHand()
    {
        _chains.ShowFor("auth-implementation");
        _chains.Kind = LinkKindChoice.All.Single(choice => choice is { Kind: LinkKind.HandsOver, ThisCardIsFrom: false });
        _chains.Target = _chains.Targets.Single(target => target.Key == "token-spike");
        _chains.Output = @"C:\work\notes\spike.md";

        _chains.Target = _chains.Targets.Single(target => target.Key == "auth-research");

        Assert.That(_chains.Output, Is.EqualTo(@"C:\work\notes\spike.md"));
    }

    [Test]
    public void AddLink_SaysWhyALinkWasRefused()
    {
        _book.Add(LinkKind.HandsOver, "token-spike", "auth-implementation");
        _chains.Update();
        _chains.ShowFor("token-spike");
        _chains.Kind = LinkKindChoice.All.Single(choice => choice is { Kind: LinkKind.HandsOver, ThisCardIsFrom: false });
        _chains.Target = _chains.Targets.Single(target => target.Key == "auth-implementation");

        _chains.AddLinkCommand.Execute(null);

        Assert.That(_chains.LinkRefusal, Does.Contain("ancestor"));
        Assert.That(_changes, Is.Zero);
    }

    [Test]
    public void FollowedOn_RecordsTheHandOverAndAReportBackForAFollowOn()
    {
        _chains.FollowedOn("auth-research", "token-spike", @"C:\work\proposals\token-spike\REPORT.md");

        Assert.That(_book.Active.Select(link => (link.Kind, link.From, link.To)), Is.EquivalentTo(new[]
        {
            (LinkKind.HandsOver, "auth-research", "token-spike"),
            (LinkKind.ReportsBack, "token-spike", "auth-research"),
        }));
        Assert.That(_book.Active.Single(link => link.Kind == LinkKind.HandsOver).Output, Is.Null,
            "a follow-on starts from what Nigel wrote for it, so it waits on nothing");
    }

    [Test]
    public void ShowFor_ListsTheCardsLinksForTheDrawer()
    {
        _book.Add(LinkKind.HandsOver, "auth-research", "token-spike");
        _chains.Update();

        _chains.ShowFor("token-spike");

        Assert.That(_chains.Related.Single().Description, Is.EqualTo("Takes over from auth-research"));
        Assert.That(_chains.Targets.Select(target => target.Key), Does.Not.Contain("token-spike"));
    }

    [Test]
    public void ShowFor_GroupsTheLinksByWhichWayTheyRun()
    {
        _book.Add(LinkKind.HandsOver, "auth-research", "token-spike");
        _book.Add(LinkKind.HandsOver, "token-spike", "auth-implementation");
        _book.Add(LinkKind.ReportsBack, "token-spike", "auth-research", output: @"C:\work\a.md");
        _book.Add(LinkKind.ReportsBack, "auth-implementation", "token-spike", output: @"C:\work\b.md");
        _chains.Update();

        _chains.ShowFor("token-spike");

        Assert.That(_chains.Parents.Single().OtherKey, Is.EqualTo("auth-research"));
        Assert.That(_chains.Children.Single().OtherKey, Is.EqualTo("auth-implementation"));
        Assert.That(_chains.Inputs.Single().OtherKey, Is.EqualTo("auth-implementation"));
        Assert.That(_chains.Outputs.Single().OtherKey, Is.EqualTo("auth-research"));
    }

    [Test]
    public void AddLink_ClosesTheFormOnceTheLinkIsMade()
    {
        _chains.Update();
        _chains.ShowFor("auth-implementation");
        _chains.BeginAddLinkCommand.Execute(null);
        _chains.Target = _chains.Targets.Single(target => target.Key == "token-spike");

        _chains.AddLinkCommand.Execute(null);

        Assert.That(_chains.IsAddingLink, Is.False);
    }

    [Test]
    public void AddLink_KeepsTheFormOpenWhenTheLinkIsRefused()
    {
        _book.Add(LinkKind.HandsOver, "auth-research", "token-spike");
        _chains.Update();
        _chains.ShowFor("token-spike");
        _chains.BeginAddLinkCommand.Execute(null);
        _chains.Kind = LinkKindChoice.All.Single(choice => choice is { Kind: LinkKind.HandsOver, ThisCardIsFrom: false });
        _chains.Target = _chains.Targets.Single(target => target.Key == "auth-implementation");

        _chains.AddLinkCommand.Execute(null);

        Assert.That(_chains.IsAddingLink, Is.True);
        Assert.That(_chains.LinkRefusal, Does.Contain("already hands over to token-spike"));
    }

    [Test]
    public void CancelAddLink_ClosesTheFormAndForgetsTheChoice()
    {
        _chains.ShowFor("auth-implementation");
        _chains.BeginAddLinkCommand.Execute(null);
        _chains.Target = _chains.Targets.Single(target => target.Key == "token-spike");

        _chains.CancelAddLinkCommand.Execute(null);

        Assert.That(_chains.IsAddingLink, Is.False);
        Assert.That(_chains.Target, Is.Null);
    }

    [Test]
    public void ShowFor_ClosesAFormLeftOpenOnAnotherCard()
    {
        _chains.ShowFor("auth-implementation");
        _chains.BeginAddLinkCommand.Execute(null);

        _chains.ShowFor("token-spike");

        Assert.That(_chains.IsAddingLink, Is.False);
    }

    [Test]
    public void Deleted_RemovesTheCardsLinksAndKeepsItsKeyTaken()
    {
        _book.Add(LinkKind.HandsOver, "auth-research", "token-spike");

        _chains.Deleted("token-spike");

        Assert.That(_book.Active, Is.Empty);
        Assert.That(_chains.TakenKeys, Does.Contain("token-spike"));
    }

    [Test]
    public void HasNothingThroughWhichItCouldLaunchASession()
    {
        var dependencies = typeof(ChainsViewModel).GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .Concat(typeof(ChainsViewModel)
                .GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Select(field => field.FieldType));

        Assert.That(dependencies, Has.None.AssignableTo(typeof(LaunchCoordinator))
            .And.None.EqualTo(typeof(ShellViewModel))
            .And.None.AssignableTo(typeof(IProcessLauncher)));
    }

    [Test]
    public void Update_PutsAReportUnderNeedsYouAndCountsItOnTheButton()
    {
        _book.Add(LinkKind.HandsOver, "token-spike", "auth-research", output: @"C:\work\REPORT.md");
        _book.Report("token-spike", "session-1", [], @"C:\work\REPORT.md", "Found it.", "partial");

        _chains.Update();

        var row = _chains.NeedsYou.Single();
        Assert.That(row.IsReported, Is.True);
        Assert.That(row.Detail, Does.Contain("verdict partial"));
        Assert.That(_chains.ButtonText, Is.EqualTo("Chains · 1 need you"));
        Assert.That(Find("auth-research").LinkLine, Does.Contain("1 to confirm"));
        Assert.That(Find("token-spike").LinkLine, Does.Contain("reported"));
    }

    [Test]
    public void NoReportYet_AfterACleanStopNeverReachesNeedsYou()
    {
        _book.Add(LinkKind.HandsOver, "token-spike", "auth-research", output: @"C:\work\REPORT.md");
        Find("token-spike").LastStartedUtc = Nine.AddHours(1);

        _chains.Update();

        Assert.That(_chains.NeedsYou, Is.Empty);
        Assert.That(Find("token-spike").LinkLine, Does.Contain("no report yet"));
    }

    [Test]
    public void AReportLost_ReachesNeedsYou()
    {
        _book.Add(LinkKind.HandsOver, "token-spike", "auth-research", output: @"C:\work\REPORT.md");
        Find("token-spike").LastStartedUtc = Nine.AddHours(1);
        Find("token-spike").LastRunCrashed = true;

        _chains.Update();

        Assert.That(_chains.NeedsYou.Single().IsLost, Is.True);
        Assert.That(_chains.NeedsYou.Single().Detail, Does.Contain("ended unexpectedly"));
    }

    [Test]
    public void Confirm_OnAGoReportSatisfiesTheLinkInOnePress()
    {
        _book.Add(LinkKind.HandsOver, "token-spike", "auth-research", output: @"C:\work\REPORT.md");
        _book.Report("token-spike", "session-1", [], @"C:\work\REPORT.md", "Found it.", "go");
        _chains.Update();

        _chains.NeedsYou.Single().ConfirmCommand.Execute(null);

        Assert.That(_book.Active.Single().SatisfiedBy, Is.EqualTo(LinkBook.ConfirmedReport));
        Assert.That(_changes, Is.EqualTo(1));
    }

    [Test]
    public void Confirm_OnAnyOtherVerdictAsksForAReasonWithTheVerdictInIt()
    {
        _book.Add(LinkKind.HandsOver, "token-spike", "auth-research", output: @"C:\work\REPORT.md");
        _book.Report("token-spike", "session-1", [], @"C:\work\REPORT.md", "Half of it.", "partial");
        _chains.Update();
        var row = _chains.NeedsYou.Single();

        row.ConfirmCommand.Execute(null);

        Assert.That(row.IsAskingReason, Is.True);
        Assert.That(row.Reason, Does.Contain("partial"));
        Assert.That(_book.Active.Single().SatisfiedUtc, Is.Null);
    }

    [Test]
    public void NotYet_LeavesTheReportReportedAndMovesItLast()
    {
        _cards.Add(Card("cache-spike"));
        _book.Add(LinkKind.HandsOver, "token-spike", "auth-research", output: @"C:\work\A.md");
        _book.Add(LinkKind.HandsOver, "auth-research", "cache-spike");
        _book.Add(LinkKind.ReportsBack, "cache-spike", "auth-research", output: @"C:\work\B.md");
        _book.Report("token-spike", "session-1", [], @"C:\work\A.md", "A.", null);
        _book.Report("cache-spike", "session-2", [], @"C:\work\B.md", "B.", null);
        _chains.Update();
        var first = _chains.NeedsYou[0];

        first.NotYetCommand.Execute(null);

        Assert.That(_chains.NeedsYou.Last().Link.Id, Is.EqualTo(first.Link.Id));
        Assert.That(_chains.NeedsYou.Last().IsSetAside, Is.True);
        Assert.That(_book.Active.All(link => link.SatisfiedUtc is null), Is.True);
    }

    [Test]
    public void ReportArrived_IsToldOnceForANewReportAndNotForOnesAlreadyThere()
    {
        _book.Add(LinkKind.HandsOver, "token-spike", "auth-research", output: @"C:\work\REPORT.md");
        _book.Add(LinkKind.HandsOver, "auth-research", "auth-implementation");
        _book.Add(LinkKind.ReportsBack, "auth-implementation", "auth-research", output: @"C:\work\OTHER.md");
        _book.Report("token-spike", "session-1", [], @"C:\work\REPORT.md", "Old.", null);
        var told = new List<string>();
        _chains.ReportArrived = told.Add;
        _chains.Update();

        _book.Report("auth-implementation", "session-2", [], @"C:\work\OTHER.md", "New.", "go");
        _chains.Update();
        _chains.Update();

        Assert.That(told, Is.EqualTo(new[] { "auth-implementation reported to auth-research (go). Confirm it in Chains." }));
    }

    [Test]
    public void Highlight_OutlinesOnlyTheCardsLinkedToTheOneUnderThePointer()
    {
        _book.Add(LinkKind.HandsOver, "auth-research", "token-spike");
        _chains.Update();

        _chains.Highlight("auth-research");

        Assert.That(Find("token-spike").IsLinkHighlighted, Is.True);
        Assert.That(Find("auth-implementation").IsLinkHighlighted, Is.False);
        Assert.That(Find("auth-research").IsLinkHighlighted, Is.False);

        _chains.Highlight(null);

        Assert.That(_cards.Any(card => card.IsLinkHighlighted), Is.False);
    }

    private WorkstreamCardViewModel Find(string key) => _cards.Single(card => card.Key == key);

    private static WorkstreamCardViewModel Card(string key) =>
        new(new Workstream(key, key, "#208DD7", false, @"C:\work", [], "Carry on.", 0, "New", false, false,
            false, null, [], null, Nine));

    private sealed class FixedClock(DateTimeOffset now) : ISystemClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
