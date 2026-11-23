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

    private (WaitItemViewModel Checks, List<IReadOnlyList<string>> Verified) StartTheChecks(Func<ChecklistViewModel, IReadOnlyList<string>?> answer)
    {
        var verified = new List<IReadOnlyList<string>>();
        var actions = _views.Actions with { AskChecklist = answer, VerifyMany = keys => verified.Add(keys) };
        var view = _views.BuildYourMarket(actions: actions);

        return (view.Waits.Single(wait => wait.Item.Kind == WaitKind.StartChecks), verified);
    }

    [Test]
    public void StartTheChecks_MakesVerifiersOnlyForTheBuildsTicked()
    {
        ChecklistViewModel? asked = null;
        var (checks, verified) = StartTheChecks(model =>
        {
            asked = model;

            foreach (var row in model.Rows.Skip(1))
            {
                row.IsTicked = false;
            }

            return model.Ticked;
        });

        checks.Command!.Execute(null);

        Assert.That(asked!.Title, Is.EqualTo("Start the checks"), "a checklist, not a message box titled for a spec");
        Assert.That(verified.Single(), Is.EqualTo(new[] { asked.Rows[0].Row.Key }));
    }

    [Test]
    public void StartTheChecks_Cancelled_MakesNothing()
    {
        var (checks, verified) = StartTheChecks(_ => null);

        checks.Command!.Execute(null);

        Assert.That(verified, Is.Empty);
        Assert.That(_views.Ran.Where(ran => ran.StartsWith("Verify:", StringComparison.Ordinal)), Is.Empty);
    }

    [Test]
    public void ThePress_IsOfferedAgainAsSoonAsARowIsTicked()
    {
        var model = new ChecklistViewModel(Checklists.StartChecks([("a", "A")], []));
        model.Rows[0].IsTicked = false;
        var raised = new List<string?>();
        model.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        model.Rows[0].IsTicked = true;

        Assert.That(raised, Does.Contain(nameof(ChecklistViewModel.CanConfirm)), "the button is bound to it");
        Assert.That(model.CanConfirm, Is.True);
    }
}
