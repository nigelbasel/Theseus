using Theseus.App.ViewModels;
using Theseus.Core.Projects;
using Theseus.Tests.Projects;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// The ticks of a checklist confirmation decide what is done (review of the checklist, M1): a row left unticked is left
/// out, and Cancel does nothing at all.
/// </summary>
[TestFixture]
public class ChecklistTicksTests
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

    [Test]
    public void ThePress_IsOfferedAgainAsSoonAsARowIsTicked()
    {
        var model = new ChecklistViewModel(new Checklist("Clear up", "Intro.", "Clear up", [new ChecklistRow("a", "A", "Why")], ticked => $"{ticked.Count} ticked"));
        model.Rows[0].IsTicked = false;
        var raised = new List<string?>();
        model.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        model.Rows[0].IsTicked = true;

        Assert.That(raised, Does.Contain(nameof(ChecklistViewModel.CanConfirm)), "the button is bound to it");
        Assert.That(model.CanConfirm, Is.True);
    }
}
