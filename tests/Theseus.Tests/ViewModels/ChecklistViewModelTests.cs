using Theseus.App.ViewModels;
using Theseus.Core.Projects;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// The checklist dialog's rows (Nigel, 2026-11-23): all ticked to begin with, one left out with a click, the foot recounted
/// as it changes, and the press refused with nothing ticked.
/// </summary>
[TestFixture]
public class ChecklistViewModelTests
{
    private static Checklist List() => new("Clear up cards", "These are not kept.", "Clear up",
        [new ChecklistRow("a", "A", "Checks X · running", "A copy."), new ChecklistRow("b", "B", "Checks Y · not running", "A copy.")],
        ticked => $"{ticked.Count} ticked");

    [Test]
    public void EveryRow_IsTickedToBeginWith()
    {
        var model = new ChecklistViewModel(List());

        Assert.That(model.Rows.All(row => row.IsTicked), Is.True);
        Assert.That(model.Foot, Is.EqualTo("2 ticked"));
        Assert.That(model.Ticked, Is.EqualTo(new[] { "a", "b" }));
    }

    [Test]
    public void LeavingOneOut_RecountsTheFoot_AndLeavesItOutOfThePress()
    {
        var model = new ChecklistViewModel(List());
        var raised = new List<string?>();
        model.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        model.Rows[0].IsTicked = false;

        Assert.That(model.Foot, Is.EqualTo("1 ticked"));
        Assert.That(raised, Does.Contain(nameof(ChecklistViewModel.Foot)));
        Assert.That(model.Ticked, Is.EqualTo(new[] { "b" }));
        Assert.That(model.CanConfirm, Is.True);
    }

    [Test]
    public void WithNothingTicked_ThePressIsRefused()
    {
        var model = new ChecklistViewModel(List());

        foreach (var row in model.Rows)
        {
            row.IsTicked = false;
        }

        Assert.That(model.CanConfirm, Is.False);
    }
}
