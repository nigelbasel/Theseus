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
        _book.Add(LinkKind.Feeds, "token-spike", "auth-implementation");

        _chains.Update();

        Assert.That(Find("auth-implementation").LinkLine, Is.EqualTo("⧗ waits on token-spike"));
        Assert.That(Find("auth-research").LinkLine, Is.Empty);
    }

    [Test]
    public void Update_PutsAChildUnderTheCardThatSpawnedIt()
    {
        _book.Add(LinkKind.Spawned, "auth-research", "token-spike");

        _chains.Update();

        Assert.That(_chains.Roots.Single().Key, Is.EqualTo("auth-research"));
        Assert.That(_chains.Roots.Single().Children.Single().Key, Is.EqualTo("token-spike"));
    }

    [Test]
    public void Update_ListsAndCountsReadyCards()
    {
        var link = _book.Add(LinkKind.Feeds, "token-spike", "auth-implementation").Link!;
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
        _chains.Kind = LinkKindChoice.All.Single(choice => choice is { Kind: LinkKind.Feeds, ThisCardIsFrom: false });
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
        _chains.Kind = LinkKindChoice.All.Single(choice => choice is { Kind: LinkKind.Feeds, ThisCardIsFrom: false });

        _chains.Target = _chains.Targets.Single(target => target.Key == "token-spike");

        Assert.That(_chains.Output, Is.EqualTo(BriefFolder.ReportPath(@"C:\work", BriefFolder.DefaultName, "token-spike")));
    }

    [Test]
    public void Target_ReplacesTheSuggestionWhenAnotherUpstreamIsChosen()
    {
        _chains.ShowFor("auth-implementation");
        _chains.Kind = LinkKindChoice.All.Single(choice => choice is { Kind: LinkKind.Feeds, ThisCardIsFrom: false });
        _chains.Target = _chains.Targets.Single(target => target.Key == "token-spike");

        _chains.Target = _chains.Targets.Single(target => target.Key == "auth-research");

        Assert.That(_chains.Output, Is.EqualTo(BriefFolder.ReportPath(@"C:\work", BriefFolder.DefaultName, "auth-research")));
    }

    [Test]
    public void Target_KeepsADocumentTypedByHand()
    {
        _chains.ShowFor("auth-implementation");
        _chains.Kind = LinkKindChoice.All.Single(choice => choice is { Kind: LinkKind.Feeds, ThisCardIsFrom: false });
        _chains.Target = _chains.Targets.Single(target => target.Key == "token-spike");
        _chains.Output = @"C:\work\notes\spike.md";

        _chains.Target = _chains.Targets.Single(target => target.Key == "auth-research");

        Assert.That(_chains.Output, Is.EqualTo(@"C:\work\notes\spike.md"));
    }

    [Test]
    public void AddLink_SaysWhyALinkWasRefused()
    {
        _book.Add(LinkKind.Feeds, "token-spike", "auth-implementation");
        _chains.Update();
        _chains.ShowFor("token-spike");
        _chains.Kind = LinkKindChoice.All.Single(choice => choice is { Kind: LinkKind.Feeds, ThisCardIsFrom: false });
        _chains.Target = _chains.Targets.Single(target => target.Key == "auth-implementation");

        _chains.AddLinkCommand.Execute(null);

        Assert.That(_chains.LinkRefusal, Does.Contain("cycle"));
        Assert.That(_changes, Is.Zero);
    }

    [Test]
    public void Spawned_RecordsTheParentAndAReportBackForAFollowOn()
    {
        _chains.Spawned("auth-research", "token-spike", @"C:\work\proposals\token-spike\REPORT.md");

        Assert.That(_book.Active.Select(link => (link.Kind, link.From, link.To)), Is.EquivalentTo(new[]
        {
            (LinkKind.Spawned, "auth-research", "token-spike"),
            (LinkKind.Feeds, "token-spike", "auth-research"),
        }));
    }

    [Test]
    public void ShowFor_ListsTheCardsLinksForTheDrawer()
    {
        _book.Add(LinkKind.Spawned, "auth-research", "token-spike");
        _chains.Update();

        _chains.ShowFor("token-spike");

        Assert.That(_chains.Related.Single().Description, Is.EqualTo("Spawned by auth-research"));
        Assert.That(_chains.Targets.Select(target => target.Key), Does.Not.Contain("token-spike"));
    }

    [Test]
    public void ShowFor_GroupsTheLinksByWhichWayTheyRun()
    {
        _book.Add(LinkKind.Spawned, "auth-research", "token-spike");
        _book.Add(LinkKind.Spawned, "token-spike", "auth-implementation");
        _book.Add(LinkKind.Feeds, "auth-research", "token-spike", output: @"C:\work\a.md");
        _book.Add(LinkKind.Feeds, "token-spike", "auth-implementation", output: @"C:\work\b.md");
        _chains.Update();

        _chains.ShowFor("token-spike");

        Assert.That(_chains.Parents.Single().OtherKey, Is.EqualTo("auth-research"));
        Assert.That(_chains.Children.Single().OtherKey, Is.EqualTo("auth-implementation"));
        Assert.That(_chains.Inputs.Single().OtherKey, Is.EqualTo("auth-research"));
        Assert.That(_chains.Outputs.Single().OtherKey, Is.EqualTo("auth-implementation"));
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
        _book.Add(LinkKind.Spawned, "auth-research", "token-spike");
        _chains.Update();
        _chains.ShowFor("token-spike");
        _chains.BeginAddLinkCommand.Execute(null);
        _chains.Kind = LinkKindChoice.All.Single(choice => choice is { Kind: LinkKind.Spawned, ThisCardIsFrom: false });
        _chains.Target = _chains.Targets.Single(target => target.Key == "auth-implementation");

        _chains.AddLinkCommand.Execute(null);

        Assert.That(_chains.IsAddingLink, Is.True);
        Assert.That(_chains.LinkRefusal, Does.Contain("already spawned"));
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
        _book.Add(LinkKind.Spawned, "auth-research", "token-spike");

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

    private WorkstreamCardViewModel Find(string key) => _cards.Single(card => card.Key == key);

    private static WorkstreamCardViewModel Card(string key) =>
        new(new Workstream(key, key, "#208DD7", false, @"C:\work", [], "Carry on.", 0, "New", false, false,
            false, null, [], null, Nine));

    private sealed class FixedClock(DateTimeOffset now) : ISystemClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
