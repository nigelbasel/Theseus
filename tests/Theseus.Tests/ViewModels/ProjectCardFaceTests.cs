using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Core.Projects;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// Where a project is, on its card (1.1d AC 39, 43, 44): the project view's own sentence and counts, the zeros left out,
/// read from the same pieces of work; whether any of its cards has a session; and a dimmed card emptied of all that.
/// </summary>
[TestFixture]
public class ProjectCardFaceTests
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

    private ProjectCardViewModel TheseusCard(SessionLifecycle git22 = SessionLifecycle.Idle)
    {
        var card = new ProjectCardViewModel(TheseusProject, "Theseus bootstrap");
        ProjectCardFace.Fill(card, TheseusPieces(_folder, git22));

        return card;
    }

    [Test]
    public void OnTheTheseusFixture_TheCardsWaitingChip_AndTheViewsBothRead3()
    {
        var card = TheseusCard();
        var view = _views.Theseus();

        Assert.That(card.Counts.Single(chip => chip.State == StageNodeState.WaitingOnYou).Text, Is.EqualTo("◆ 3 waiting on you"));
        Assert.That(view.Counts.Single(chip => chip.State == StageNodeState.WaitingOnYou).Count, Is.EqualTo(3));
    }

    [Test]
    public void TheCard_ShowsTheViewsSentence_AndItsCountChips_WithTheZerosLeftOut()
    {
        var card = TheseusCard();
        var view = _views.Theseus();

        Assert.That(card.Sentence, Is.EqualTo(view.Sentence));
        Assert.That(card.Sentence, Is.EqualTo("12 pieces of work across 5 objectives: 7 done, 3 waiting on you, 2 still to do or not started. "
            + "git-22 is idle on projects-and-method-ui. Next: you accept the spec of projects-and-method-ui."));
        Assert.That(card.Counts.Select(chip => chip.Text), Is.EqualTo(new[] { "✓ 7 done", "◆ 3 waiting on you", "– 2 to do" }));
        Assert.That(view.Counts.Select(chip => chip.Text), Has.Exactly(6).Items, "the view keeps its zeros (AC 19)");
    }

    [Test]
    public void AProjectWithACardInASession_IsLive_SoSelectLiveChoosesIt()
    {
        Assert.That((TheseusCard(SessionLifecycle.Running).IsLive, TheseusCard(SessionLifecycle.Ended).IsLive), Is.EqualTo((true, false)));
    }

    [Test]
    public void TheCardsStartAndStop_AreTheProjectsOwn_WithTheirReasons()
    {
        var card = TheseusCard(SessionLifecycle.Running);

        Assert.That(card.StartToolTip, Is.EqualTo("No card of Theseus bootstrap can start: git-22 is already running"));
        Assert.That(card.StopToolTip, Is.EqualTo("Stop Theseus bootstrap: ask git-22 to write a handover and exit"));
    }

    [Test]
    public void Fill_SaysWhenWhatStartOrStopCanDoHasChanged_SoTheirButtonsAreLookedAtAgain()
    {
        var card = new ProjectCardViewModel(TheseusProject, "Theseus bootstrap");

        Assert.That(ProjectCardFace.Fill(card, TheseusPieces(_folder, SessionLifecycle.Ended)), Is.True, "the first fill");
        Assert.That(ProjectCardFace.Fill(card, TheseusPieces(_folder, SessionLifecycle.Ended)), Is.False, "nothing moved");
        Assert.That(ProjectCardFace.Fill(card, TheseusPieces(_folder, SessionLifecycle.Running)), Is.True, "git-22 started, so Stop can run");
    }

    [Test]
    public void ADimmedCard_LosesTheSentenceCountsStartAndStop_AndHasNoRunButtons()
    {
        var card = TheseusCard(SessionLifecycle.Running);

        card.IsDimmed = true;
        ProjectCardFace.Dim(card);

        Assert.That((card.HasSentence, card.Counts.Count, card.IsLive, card.StartPlan, card.StopPlan, card.ShowsRunButtons),
            Is.EqualTo((false, 0, false, (ProjectStart?)null, (ProjectStop?)null, false)));
        Assert.That(card.StartToolTip, Is.EqualTo("A project that does not read cannot be started"));
    }
}
