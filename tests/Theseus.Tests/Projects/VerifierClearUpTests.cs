using Theseus.Core.Links;
using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// One press of Clear up verifiers… (git-22, 2026-11-23: by hand it was open, Stop, wait, ⋯ › Archive for every card,
/// with Archive grey until its session stopped): it stops each running session, retires each card's report-back links so
/// nothing it said counts as met, and archives each card once its session has gone. Its record outlives a restart, so
/// a stop that lands late, and Undo, are not lost (review of item 5, M3).
/// </summary>
[TestFixture]
public class VerifierClearUpTests
{
    private static readonly DateTimeOffset Nine = new(2026, 11, 23, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void ItStopsTheRunningOnes_AndRetiresEveryReportBackLinkTheyWrite()
    {
        var clearUp = VerifierClearUp.Plan("bym", Wrong(), Links());

        Assert.That(clearUp.ToStop, Is.EqualTo(new[] { "verify-w1" }));
        Assert.That(clearUp.Before.Select(link => link.Id), Is.EquivalentTo(new[] { "reported-w1", "satisfied-w2", "waiting-w2" }),
            "a confirmed one and one with no report yet are retired too (review of item 5, M1, M2); another card's are not");
    }

    [Test]
    public void ACardIsArchivedOnlyOnceItsSessionHasGone()
    {
        var clearUp = VerifierClearUp.Plan("bym", Wrong(), Links());
        var live = new HashSet<string> { "verify-w1" };

        Assert.That(clearUp.Due(live.Contains), Is.EqualTo(new[] { "verify-w2-2" }), "the stopped one goes at once");
        Assert.That(clearUp.IsDone, Is.False);
        Assert.That(clearUp.Due(live.Contains), Is.Empty, "each card is archived once");

        live.Clear();

        Assert.That(clearUp.Due(live.Contains), Is.EqualTo(new[] { "verify-w1" }));
        Assert.That(clearUp.IsDone, Is.True);
        Assert.That(clearUp.Archived, Is.EqualTo(new[] { "verify-w2-2", "verify-w1" }), "what Undo restores");
    }

    [Test]
    public void ItsRecord_CarriesOnAfterARestart()
    {
        var at = new DateTimeOffset(2026, 11, 23, 17, 24, 0, TimeSpan.Zero);
        var clearUp = VerifierClearUp.Plan("bym", Wrong(), Links(), at, round: 2);
        clearUp.Due(key => key == "verify-w1");

        var resumed = VerifierClearUp.From(clearUp.Record);

        Assert.That((resumed.Record.AtUtc, resumed.Record.Round), Is.EqualTo((at, 2)), "its Undo lapses a day after the press");

        Assert.That(resumed.Project, Is.EqualTo("bym"));
        Assert.That(resumed.Archived, Is.EqualTo(new[] { "verify-w2-2" }));
        Assert.That(resumed.Before.Select(link => link.Id), Is.EquivalentTo(clearUp.Before.Select(link => link.Id)));
        Assert.That(resumed.Due(_ => false), Is.EqualTo(new[] { "verify-w1" }), "the stop that landed late is still archived");
    }

    [Test]
    public void ItCoversItsCards_SoASecondPressDoesNotListThemAgain()
    {
        var clearUp = VerifierClearUp.Plan("bym", Wrong(), Links());

        Assert.That(clearUp.Covers("verify-w1"), Is.True);
        Assert.That(clearUp.Covers("verify-kept"), Is.False);
    }

    private static IReadOnlyList<WrongVerifier> Wrong() =>
    [
        new("verify-w1", "Verify: W1", "w1", true, "it ran another project's scenarios"),
        new("verify-w2-2", "Verify: W2 (2)", "w2", false, "a copy"),
    ];

    private static IReadOnlyList<Link> Links() =>
    [
        new("reported-w1", LinkKind.ReportsBack, "verify-w1", "w1", Nine, "board", @"C:\h\results\a.md",
            LatestReport: new LinkReport(Nine, "s", @"C:\h\results\a.md", "x: pass", "done")),
        new("satisfied-w2", LinkKind.ReportsBack, "verify-w2-2", "w2", Nine, "board", @"C:\h\results\b.md", Nine, LinkBook.ConfirmedReport,
            LatestReport: new LinkReport(Nine, "s", @"C:\h\results\b.md", "x: pass", "done")),
        new("waiting-w2", LinkKind.ReportsBack, "verify-w2-2", "w2", Nine, "board", @"C:\h\results\c.md"),
        new("handover-w2", LinkKind.HandsOver, "w2", "verify-w2-2", Nine, "board"),
        new("other", LinkKind.ReportsBack, "verify-kept", "w2", Nine, "board", @"C:\h\results\d.md",
            LatestReport: new LinkReport(Nine, "s", @"C:\h\results\d.md", "x: pass", "done")),
    ];
}
