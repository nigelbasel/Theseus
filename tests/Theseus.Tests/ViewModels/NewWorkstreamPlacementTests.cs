using Theseus.App.ViewModels;
using Theseus.Core.Abstractions;
using Theseus.Core.Colour;
using Theseus.Core.Domain;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// A workstream is created where the person making it says, rather than in the first cell.
///
/// Until 2026-09-23 there was no choice: every new workstream appeared top left whatever it was for,
/// and had to be dragged to where it belonged.
/// </summary>
[TestFixture]
public class NewWorkstreamPlacementTests
{
    private static readonly string[] Stages = ["New", "In progress", "Done"];
    private static readonly string[] Areas = ["Research", "Dev"];

    [Test]
    public void Open_OffersTheBoardsCellsAndStartsOnTheFirstOne()
    {
        var composer = Composer(out _);

        composer.OpenCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(composer.Stages, Is.EqualTo(Stages));
            Assert.That(composer.Areas, Is.EqualTo(Areas));
            Assert.That(composer.Stage, Is.EqualTo("New"));
            Assert.That(composer.Area, Is.EqualTo("Research"));
            Assert.That(composer.HasAreas, Is.True);
        });
    }

    [Test]
    public void Create_SaysWhichCellTheWorkstreamBelongsIn()
    {
        var composer = Composer(out var created);

        composer.OpenCommand.Execute(null);
        composer.Title = "Contact forms";
        composer.WorkingDirectory = Path.GetTempPath();
        composer.Prompt = "Add validation.";
        composer.Stage = "In progress";
        composer.Area = "Dev";
        composer.StartNow = false;
        composer.CreateCommand.Execute(null);

        Assert.That(created.Single(), Is.EqualTo(new NewWorkstream(created.Single().Key, "In progress", "Dev", false)));
    }

    [Test]
    public void HasAreas_IsFalseOnABoardWithOneRowBecauseThatIsNotAChoice()
    {
        var composer = Composer(out _, areas: ["Research"]);

        composer.OpenCommand.Execute(null);

        Assert.That(composer.HasAreas, Is.False);
    }

    [Test]
    public void Place_PutsTheCardInTheCellItWasCreatedIn()
    {
        var fixture = new BoardFixture(lanes: Stages, areas: Areas).With("one");
        fixture.Board.Refresh();

        fixture.Board.Place("one", "Done", "Dev");

        Assert.That(fixture.Card("one")!.Column, Is.EqualTo("Done"));
        Assert.That(fixture.Card("one")!.Area, Is.EqualTo("Dev"));
    }

    [Test]
    public void Place_KeepsTheCardOnTheBoardWhenItIsGivenACellTheBoardDoesNotHave()
    {
        var fixture = new BoardFixture(lanes: Stages, areas: Areas).With("one", "In progress", "Dev");
        fixture.Board.Refresh();

        fixture.Board.Place("one", "Nowhere", "Nobody");

        Assert.That(fixture.Card("one")!.Column, Is.EqualTo("New"));
        Assert.That(fixture.Card("one")!.Area, Is.EqualTo("Research"));
    }

    [Test]
    public void Place_DoesNothingForAWorkstreamTheBoardDoesNotHold()
    {
        var fixture = new BoardFixture(lanes: Stages, areas: Areas).With("one");
        fixture.Board.Refresh();

        Assert.DoesNotThrow(() => fixture.Board.Place("never-made", "Done", "Dev"));
    }

    private static NewWorkstreamViewModel Composer(
        out List<NewWorkstream> created, IReadOnlyList<string>? areas = null)
    {
        var made = new List<NewWorkstream>();

        created = made;

        return new NewWorkstreamViewModel(new FakePlans(), new FakeColours(), () => Stages, () => areas ?? Areas, made.Add);
    }

    private sealed class FakePlans : IPlanStore
    {
        private readonly Dictionary<string, SessionPlan> _plans = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<string> Keys() => [.. _plans.Keys];

        public string PathFor(string key) => $@"C:\plans\{key}.json";

        public SessionPlan? TryRead(string key) => _plans.GetValueOrDefault(key);

        public void Write(string key, SessionPlan plan) => _plans[key] = plan;

        public void Delete(string key) => _plans.Remove(key);
    }

    private sealed class FakeColours : ITabColourAllocator
    {
        public string Allocate(ColourAllocationState state) => "#208DD7";
    }
}
