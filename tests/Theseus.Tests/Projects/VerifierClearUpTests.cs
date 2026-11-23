using Theseus.Core.Links;
using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// One press of Clear up verifiers… (git-22, 2026-11-23: by hand it was open, Stop, wait, ⋯ › Archive for every card,
/// with Archive grey until its session stopped): it stops each running session, rejects each card's report so it never
/// counts as met, and archives each card once its session has gone.
/// </summary>
[TestFixture]
public class VerifierClearUpTests
{
    private static readonly DateTimeOffset Nine = new(2026, 11, 23, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void ItStopsTheRunningOnes_AndRejectsEveryReportTheyFiled()
    {
        var clearUp = VerifierClearUp.Plan(Wrong(), Links());

        Assert.That(clearUp.ToStop, Is.EqualTo(new[] { "verify-w1" }));
        Assert.That(clearUp.ReportsToReject, Is.EqualTo(new[] { "reported-w1" }),
            "a satisfied link and one with no report have nothing to reject");
    }

    [Test]
    public void ACardIsArchivedOnlyOnceItsSessionHasGone()
    {
        var clearUp = VerifierClearUp.Plan(Wrong(), Links());
        var live = new HashSet<string> { "verify-w1" };

        Assert.That(clearUp.Due(live.Contains), Is.EqualTo(new[] { "verify-w2-2" }), "the stopped one goes at once");
        Assert.That(clearUp.IsDone, Is.False);
        Assert.That(clearUp.Due(live.Contains), Is.Empty, "each card is archived once");

        live.Clear();

        Assert.That(clearUp.Due(live.Contains), Is.EqualTo(new[] { "verify-w1" }));
        Assert.That(clearUp.IsDone, Is.True);
        Assert.That(clearUp.Archived, Is.EqualTo(new[] { "verify-w2-2", "verify-w1" }), "what Undo restores");
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
        new("other", LinkKind.ReportsBack, "verify-kept", "w2", Nine, "board", @"C:\h\results\d.md",
            LatestReport: new LinkReport(Nine, "s", @"C:\h\results\d.md", "x: pass", "done")),
    ];
}
