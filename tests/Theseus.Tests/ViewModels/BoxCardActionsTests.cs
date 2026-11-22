using Theseus.App.ViewModels;
using Theseus.Core.Projects;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ClarityFixtures;

namespace Theseus.Tests.ViewModels;

/// <summary>A box's menu offers its card's own board actions (Nigel, 2026-11-22: cards he could not archive from the view).</summary>
[TestFixture]
public class BoxCardActionsTests
{
    private TestProjectsFolder _folder = null!;

    [SetUp]
    public void SetUp() => _folder = TestProjectsFolder.Create();

    [TearDown]
    public void TearDown() => _folder.Dispose();

    [Test]
    public void ABoxsMenu_OffersTheCardActionsTheShellGivesForItsPiece()
    {
        var asked = new List<string>();
        ProjectViewActions actions = new ProjectViews(_folder).Actions with
        {
            CardActions = piece =>
            {
                asked.Add(piece.CardKey);
                return [new PieceActionViewModel("Archive…", () => { }, false, "has a session: stop it before archiving it")];
            },
        };

        var view = ProjectViewModel.Build(WriteBym(_folder), BymPieces(_folder), ProjectViewFixtures.LinksOn(BymLinks(), BymBoard()), [],
            new MemorySettingsStore(), actions);
        var box = view.AllNodes.Single(node => node.Name == Spec2);
        var archive = box.Actions.Single(action => action.Label == "Archive…");

        Assert.That(asked, Does.Contain(box.Piece.CardKey));
        Assert.That((archive.IsEnabled, archive.ToolTip), Is.EqualTo((false, "has a session: stop it before archiving it")),
            "disabled with its reason while the card has a session");
    }
}
