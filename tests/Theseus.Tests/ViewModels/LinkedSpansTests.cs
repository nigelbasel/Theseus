using Theseus.App.ViewModels;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ClarityFixtures;

namespace Theseus.Tests.ViewModels;

/// <summary>Objective numbers and W-codes in the view's sentences are links, and only where they lead somewhere (I42).</summary>
[TestFixture]
public class LinkedSpansTests
{
    private TestProjectsFolder _folder = null!;

    [SetUp]
    public void SetUp() => _folder = TestProjectsFolder.Create();

    [TearDown]
    public void TearDown() => _folder.Dispose();

    [Test]
    public void ATokenThatLeadsSomewhere_IsALink_AndTheRestStaysText()
    {
        var spans = LinkedSpans.Of("W4 serves Objective 1.4; W99 is gone.",
            token => token is "W4" or "Objective 1.4" ? (() => { }, "Go") : null);

        Assert.That(spans.Select(span => (span.Text, span.Follow is not null)), Is.EqualTo(new[]
        {
            ("W4", true), (" serves ", false), ("Objective 1.4", true), ("; W99 is gone.", false),
        }));
    }

    [Test]
    public void TheView_ResolvesObjectivesAndWCodes_ToWhatItShows_AndNothingElse()
    {
        var view = ProjectViewModel.Build(WriteBym(_folder), BymPieces(_folder), ProjectViewFixtures.LinksOn(BymLinks(), BymBoard()), [],
            new MemorySettingsStore(), new ProjectViews(_folder).Actions);

        Assert.That(view.LinkTarget("Objective 1.4")?.ToolTip, Is.EqualTo("Go to objective 1.4"));
        Assert.That(view.LinkTarget("Objective 9.9"), Is.Null, "no such objective: no link");
        Assert.That(view.LinkTarget("W4")?.ToolTip, Does.StartWith("Go to "));
        Assert.That(view.LinkTarget("W99"), Is.Null);

        view.LinkTarget("W4")!.Value.Follow();

        Assert.That(view.Selection, Is.InstanceOf<PieceNodeViewModel>());
    }
}
