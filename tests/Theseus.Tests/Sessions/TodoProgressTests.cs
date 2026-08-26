using Theseus.Core.Sessions;

namespace Theseus.Tests.Sessions;

[TestFixture]
public class TodoProgressTests
{
    [Test]
    public void Of_SaysWhatTheSessionIsDoingRatherThanMerelyThatItIsBusy()
    {
        var progress = TodoProgress.Of([
            Todo("1", "completed"),
            Todo("2", "in_progress", "Porting TextRenderingTests"),
            Todo("3", "pending"),
        ]);

        Assert.That(progress.Summary, Is.EqualTo("Porting TextRenderingTests · 1/3"));
    }

    [Test]
    public void Of_FallsBackToTheSubjectWhenAnItemHasNoActiveForm()
    {
        var progress = TodoProgress.Of([Todo("1", "in_progress", activeForm: null)]);

        Assert.That(progress.Summary, Does.StartWith("Item 1"));
    }

    [Test]
    public void Of_TakesTheFirstItemInProgressWhenSeveralAre()
    {
        var progress = TodoProgress.Of([
            Todo("1", "in_progress", "Reading the bindings"),
            Todo("2", "in_progress", "Writing the shaper"),
        ]);

        Assert.That(progress.ActiveForm, Is.EqualTo("Reading the bindings"));
    }

    [Test]
    public void Of_CountsProgressWhenNothingIsInHand()
    {
        Assert.That(TodoProgress.Of([Todo("1", "completed"), Todo("2", "pending")]).Summary,
            Is.EqualTo("1/2 done"));
    }

    [Test]
    public void Of_SaysSoWhenAListIsFinished()
    {
        Assert.That(TodoProgress.Of([Todo("1", "completed"), Todo("2", "completed")]).Summary,
            Is.EqualTo("all 2 done"));
    }

    [Test]
    public void Of_ShowsNothingForASessionThatKeepsNoList()
    {
        var progress = TodoProgress.Of([]);

        Assert.That(progress.IsKnown, Is.False);
        Assert.That(progress.Summary, Is.Empty);
    }

    [Test]
    public void Of_ReadsTheStatusWhateverItsCasing()
    {
        Assert.That(TodoProgress.Of([Todo("1", "IN_PROGRESS", "Doing it")]).ActiveForm, Is.EqualTo("Doing it"));
        Assert.That(TodoProgress.Of([Todo("1", "Completed")]).Completed, Is.EqualTo(1));
    }

    [Test]
    public void Of_TreatsAnItemWithNoStatusAsUnfinishedRatherThanGuessing()
    {
        var progress = TodoProgress.Of([Todo("1", status: null)]);

        Assert.That(progress.Completed, Is.Zero);
        Assert.That(progress.Summary, Is.EqualTo("0/1 done"));
    }

    private static SessionTodo Todo(string id, string? status, string? activeForm = "Doing something") =>
        new(id, $"Item {id}", activeForm, status);
}
