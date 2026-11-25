using Theseus.App.ViewModels;
using Theseus.Tests.Projects;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// The clear-up's notice in the project view (Nigel, 2026-11-23): a quiet line under the heading with Undo and ✕ Dismiss,
/// never one of the things that wait for him.
/// </summary>
[TestFixture]
public class ClearUpNoticeLineTests
{
    private const string Notice = "Seven cards were cleared up: stopped, archived, their reports rejected. Undo until 19:24 tomorrow.";

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
    public void TheNotice_IsUnderTheHeading_NotUnderWaitsForYou()
    {
        var view = _views.BuildYourMarket(clearUpNotice: Notice);

        Assert.That(view.ClearUpNotice, Is.EqualTo(Notice));
        Assert.That(view.Waits.Select(wait => wait.Item.Text), Has.None.EqualTo(Notice));
    }

    [Test]
    public void TheNoticeGoing_ChangesTheViewsSignature_SoTheViewIsRedrawn()
    {
        Assert.That(_views.BuildYourMarket(clearUpNotice: Notice).Signature, Is.Not.EqualTo(_views.BuildYourMarket().Signature),
            "review of the follow-ups, M1: after Dismiss the line stayed, its buttons doing nothing");
    }

    [Test]
    public void UndoAndDismiss_ActOnThisProject()
    {
        var undone = new List<string>();
        var dismissed = new List<string>();
        var actions = _views.Actions with { UndoClearVerifiers = undone.Add, DismissClearUp = dismissed.Add };
        var view = _views.BuildYourMarket(actions: actions, clearUpNotice: Notice);

        view.UndoClearUpCommand!.Execute(null);
        view.DismissClearUpCommand!.Execute(null);

        Assert.That(undone, Is.EqualTo(new[] { view.Name }));
        Assert.That(dismissed, Is.EqualTo(new[] { view.Name }));
    }
}
