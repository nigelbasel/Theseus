using Theseus.Core.Abstractions;
using Theseus.Core.Links;

namespace Theseus.Tests.Links;

/// <summary>
/// Rejecting a report (git-22, 2026-11-23: Nigel pressed Not yet on three wrong verifier reports and they stayed in NEEDS
/// YOU, with nothing saying what had happened): a rejected report never satisfies its link, cannot be confirmed, leaves
/// NEEDS YOU, and is kept with its reason; a newer report on the link is judged on its own.
/// </summary>
[TestFixture]
public class LinkBookRejectTests
{
    private const string Output = @"C:\holdout\results\close-step-20261123-090000.md";
    private static readonly DateTimeOffset Nine = new(2026, 11, 23, 9, 0, 0, TimeSpan.Zero);

    private string _directory = string.Empty;
    private MovableClock _clock = null!;
    private LinkBook _book = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-reject-{Guid.NewGuid():N}");
        _clock = new MovableClock(Nine);
        _book = Open();
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
    public void Reject_KeepsTheReason_AndTheLinkStaysUnsatisfied()
    {
        var link = Reported();

        var change = _book.Reject(link.Id, "it ran another project's scenarios");

        Assert.That(change.Refusal, Is.Null);
        var stored = Open().Active.Single(each => each.Kind == LinkKind.ReportsBack);
        Assert.That(stored.SatisfiedUtc, Is.Null, "a rejected report never satisfies the link");
        Assert.That(stored.Rejection, Is.EqualTo(new LinkRejection(Nine, "it ran another project's scenarios", Nine)));
        Assert.That(stored.ReportIsRejected, Is.True);
    }

    [Test]
    public void ARejectedReport_CannotBeConfirmed()
    {
        var link = Reported();
        _book.Reject(link.Id, "wrong scenarios");

        Assert.That(_book.Confirm(link.Id).Refusal, Does.Contain("rejected"));
    }

    [Test]
    public void Reject_NeedsAReason_AndAReport()
    {
        var link = Reported();
        _book.Add(LinkKind.HandsOver, "close-step", "verify-two");
        var empty = _book.Add(LinkKind.ReportsBack, "verify-two", "close-step", output: Output).Link!;

        Assert.That(_book.Reject(link.Id, "  ").Refusal, Does.Contain("Say why"));
        Assert.That(_book.Reject(empty.Id, "wrong").Refusal, Does.Contain("no report"));
    }

    [Test]
    public void ASatisfiedLink_IsNotRejected()
    {
        var link = Reported();
        _book.Confirm(link.Id);

        Assert.That(_book.Reject(link.Id, "changed my mind").Refusal, Does.Contain("already satisfied"));
    }

    [Test]
    public void ANewerReport_IsNotRejected()
    {
        var link = Reported();
        _book.Reject(link.Id, "wrong scenarios");
        _clock.Advance(TimeSpan.FromHours(1));

        _book.Report("verify-close-step", "session-2", [link.Id], Output, "All three pass.", "done");

        Assert.That(_book.Active.Single(each => each.Kind == LinkKind.ReportsBack).ReportIsRejected, Is.False, "the new report is judged on its own");
    }

    [Test]
    public void ARejectedReport_ReadsAsRejected_NotReported()
    {
        var link = Reported();
        _book.Reject(link.Id, "wrong scenarios");
        var status = new LinkStatus(_book.Active,
            [new LinkCard("verify-close-step", "Verify: close-step", false, false, false, null), new LinkCard("close-step", "close-step", false, false, false, null)]);

        Assert.That(status.StateOf(_book.Active.Single(each => each.Kind == LinkKind.ReportsBack)), Is.EqualTo(LinkState.Rejected));
    }

    [Test]
    public void Unreject_PutsTheReportBack()
    {
        var link = Reported();
        _book.Reject(link.Id, "wrong scenarios");

        Assert.That(_book.Unreject(link.Id).Refusal, Is.Null);
        Assert.That(_book.Active.Single(each => each.Kind == LinkKind.ReportsBack).ReportIsRejected, Is.False);
        Assert.That(_book.Confirm(link.Id).Refusal, Is.Null);
    }

    private Link Reported()
    {
        _book.Add(LinkKind.HandsOver, "close-step", "verify-close-step");
        var link = _book.Add(LinkKind.ReportsBack, "verify-close-step", "close-step", output: Output).Link!;
        _book.Report("verify-close-step", "session-1", [link.Id], Output, "lock contention: fail", "partial");

        return link;
    }

    private LinkBook Open() => new(new LinkStateStore(Path.Combine(_directory, "links.json")), _clock);

    private sealed class MovableClock(DateTimeOffset start) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; private set; } = start;

        public void Advance(TimeSpan span) => UtcNow += span;
    }
}
