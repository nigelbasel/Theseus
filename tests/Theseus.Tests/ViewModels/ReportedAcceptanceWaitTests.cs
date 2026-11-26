using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Core.Projects;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// A session's reported acceptance gets its Confirm… on the board whatever the piece's pipeline. On 2026-11-26 Nigel
/// accepted a freeform piece's spec in its tab; the wait said "confirm it", but it had no button and Respond to these…
/// offered nothing he could press.
/// </summary>
[TestFixture]
public class ReportedAcceptanceWaitTests
{
    private TestProjectsFolder _folder = null!;
    private ProjectViews _views = null!;

    [SetUp]
    public void SetUp()
    {
        _folder = TestProjectsFolder.Create();
        _views = new ProjectViews(_folder);
    }

    [TearDown]
    public void TearDown() => _folder.Dispose();

    [TestCase("freeform")]
    [TestCase("feature")]
    public void AReportedAcceptance_HasAConfirmButtonThatAcceptsTheSpec(string pipeline)
    {
        var reading = WriteOne(_folder, pipeline, new Dictionary<string, string> { ["spec.md"] = "# Spec\n\nThe spec.\n" });
        var hash = reading.Artefacts[APiece].SpecHash!;
        IReadOnlyList<PieceCard> cards = [Card(ACard, SessionLifecycle.Idle)];
        var report = new ReportedAcceptance(OneProject, APiece, hash, ACard, "A card", "Accept", DateTimeOffset.UnixEpoch);
        IReadOnlyList<PieceOfWork> pieces =
            [.. PiecesOfWork.Build(reading, _ => null, cards, []).Select(piece => piece with { Reported = report })];

        var view = ProjectViewModel.Build(reading, pieces, LinksOn([], cards), _views.Tabs(OneProject), _views.Settings,
            _views.Actions, null, _views.Edits);
        var confirm = view.Waits.Single(wait => wait.Item.Kind == WaitKind.ConfirmAcceptance);

        Assert.That(confirm.HasButton, Is.True, "the wait asks him to confirm, so it offers Confirm…");
        confirm.Command!.Execute(null);
        Assert.That(_views.Ran, Does.Contain($"AcceptSpec:{APiece}"));
    }
}
