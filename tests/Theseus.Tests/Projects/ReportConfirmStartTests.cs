using Theseus.Core.Domain;
using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// Confirming a report starts a stopped parent in a project (sessions-run-the-project item 16a); the ordinary board only
/// tells, and the owner's pause, a block or the archive hold it back.
/// </summary>
[TestFixture]
public class ReportConfirmStartTests
{
    private static readonly DateTimeOffset Nine = new(2026, 11, 23, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void AStoppedProjectParent_IsStarted() => Assert.That(ReportConfirmStart.StartsParent(Parent(), false), Is.True);

    [Test]
    public void ASessionsPause_DoesNotHoldItBack() =>
        Assert.That(ReportConfirmStart.StartsParent(Parent() with { IsPaused = true, PausedBy = "someone" }, false), Is.True);

    [Test]
    public void TheOrdinaryBoard_RunningOwnerPausedBlockedOrArchived_IsNotStarted()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ReportConfirmStart.StartsParent(Parent() with { Project = null }, false), Is.False, "ad hoc: told only");
            Assert.That(ReportConfirmStart.StartsParent(Parent(), true), Is.False, "running: told with its next turn");
            Assert.That(ReportConfirmStart.StartsParent(Parent() with { IsPaused = true }, false), Is.False);
            Assert.That(ReportConfirmStart.StartsParent(Parent() with { IsBlocked = true }, false), Is.False);
            Assert.That(ReportConfirmStart.StartsParent(Parent() with { IsArchived = true }, false), Is.False);
        });
    }

    private static Workstream Parent() =>
        new("parent", "Parent", "#208DD7", false, Path.GetTempPath(), [], "Carry on.", 0, "New", false, false, false, null, [], null, Nine)
        {
            Project = "theseus",
        };
}
