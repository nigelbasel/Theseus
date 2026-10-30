using Theseus.Core.Domain;
using Theseus.Core.Settings;

namespace Theseus.Tests.Domain;

/// <summary>The one-off tidy of the grid agreed on 2026-10-30, run against the board as it was that day.</summary>
[TestFixture]
public class BoardTidyTests
{
    private static readonly DateTimeOffset Nine = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);

    private static TheseusSettings Before() => TheseusSettings.Default with
    {
        Lanes = ["New", "Planning", "In progress", "Testing", "Paused", "Done"],
        Areas = ["Admin Tasks", "Research", "Spikes", "Dev", "Support", "Side Project"],
        CollapsedAreas = ["Research"],
    };

    [Test]
    public void Apply_DropsPausedRenamesTestingAndMergesTheSparseRows()
    {
        var (settings, _) = BoardTidy.Apply(Before(), []);

        Assert.Multiple(() =>
        {
            Assert.That(settings.Lanes, Is.EqualTo(new[] { "New", "Planning", "In progress", "Review", "Done" }));
            Assert.That(settings.Areas, Is.EqualTo(new[] { "Support & admin", "Research & spikes", "Dev", "Side projects" }));
            Assert.That(settings.CollapsedAreas, Is.EqualTo(new[] { "Research & spikes" }));
            Assert.That(settings.CollapsedStages, Does.Contain("Done"), "Done starts folded");
            Assert.That(settings.BoardRevision, Is.EqualTo(BoardTidy.Revision));
            Assert.That(BoardTidy.IsNeeded(settings), Is.False, "it runs once");
        });
    }

    [Test]
    public void Apply_MarksPausedCardsAndPutsThemWhereTheyCanBeStartedFrom()
    {
        var (_, sidecars) = BoardTidy.Apply(Before(),
        [
            Sidecar("ran-before", "Paused", "Dev", "session-1"),
            Sidecar("never-ran", "Paused", "Spikes", null),
            Sidecar("testing", "Testing", "Support", "session-2"),
            Sidecar("busy", "In progress", "Research", "session-3"),
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(Place(sidecars, "ran-before"), Is.EqualTo(("In progress", "Dev", true)));
            Assert.That(Place(sidecars, "never-ran"), Is.EqualTo(("New", "Research & spikes", true)));
            Assert.That(Place(sidecars, "testing"), Is.EqualTo(("Review", "Support & admin", false)));
            Assert.That(Place(sidecars, "busy"), Is.EqualTo(("In progress", "Research & spikes", false)));
        });
    }

    [Test]
    public void IsNeeded_OnlyForABoardNotYetTidied()
    {
        Assert.That(BoardTidy.IsNeeded(Before()), Is.True);
        Assert.That(BoardTidy.IsNeeded(Before() with { BoardRevision = BoardTidy.Revision }), Is.False);
    }

    private static (string Column, string? Area, bool IsPaused) Place(IReadOnlyList<WorkstreamSidecar> sidecars, string key) =>
        sidecars.Single(sidecar => sidecar.Key == key) is var found ? (found.Column, found.Area, found.IsPaused) : default;

    private static WorkstreamSidecar Sidecar(string key, string column, string area, string? lastSession) =>
        new(key, column, IsBlocked: false, IsArchived: false, ColorIsManual: false, ReopenTomorrow: true, [], null,
            lastSession, Nine, Nine) { Area = area };
}
