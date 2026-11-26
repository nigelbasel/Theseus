using Theseus.App.ViewModels;
using Theseus.Core.Questions;

namespace Theseus.Tests.ViewModels;

/// <summary>A question asked with ask_owner, answered in Theseus (2.4e).</summary>
[TestFixture]
public class AnswerQuestionViewModelTests
{
    private static readonly OwnerQuestion Asked = new("s", "Which route?", IsGuess: false, DateTimeOffset.UnixEpoch)
    {
        Options = ["A: stub harness", "B: I sign in"],
        Why = "Neither runs without one",
    };

    [Test]
    public void AnAskOwnerWait_ShowsTheQuestion_ItsWhy_AndEachOption()
    {
        var model = new AnswerQuestionViewModel("Verify: W4", Asked, hasTab: false, "when it next runs");

        Assert.That((model.Heading, model.Question, model.Why), Is.EqualTo(("Verify: W4 asks", "Which route?", "Neither runs without one")));
        Assert.That(model.Options.Select(option => option.Text), Is.EqualTo(new[] { "A: stub harness", "B: I sign in" }));
    }

    [Test]
    public void ChoosingAnOption_GivesIt()
    {
        var model = new AnswerQuestionViewModel("W4", Asked, hasTab: true, "now");
        var finished = 0;
        model.Finished += (_, _) => finished++;

        model.Options[1].Command.Execute(null);

        Assert.That((model.Answer, model.GoToTab, finished), Is.EqualTo(("B: I sign in", false, 1)));
    }

    [Test]
    public void AnAnswerInHisOwnWords_IsOffWhileEmpty_AndGivenTrimmed()
    {
        var model = new AnswerQuestionViewModel("W4", Asked, hasTab: false, "now");

        Assert.That(model.SendCommand.CanExecute(null), Is.False);

        model.OwnWords = "  neither: report both as not run  ";
        model.SendCommand.Execute(null);

        Assert.That(model.Answer, Is.EqualTo("neither: report both as not run"));
    }

    [Test]
    public void GoToItsTab_IsOfferedOnlyForASessionWithATab_AndAnswersNothing()
    {
        Assert.That(new AnswerQuestionViewModel("W4", Asked, hasTab: false, "now").GoToTabCommand.CanExecute(null), Is.False);

        var model = new AnswerQuestionViewModel("W4", Asked, hasTab: true, "now");
        model.GoToTabCommand.Execute(null);

        Assert.That((model.Answer, model.GoToTab), Is.EqualTo(((string?)null, true)));
    }
}
