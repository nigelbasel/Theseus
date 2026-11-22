using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>Back and Forward through the owner's jumps (1.1f I46, D14), as the spec's own walk-through has it.</summary>
[TestFixture]
public class ViewHistoryTests
{
    private static readonly ViewPlace Project = new("bym", "rounds", null);
    private static readonly ViewPlace Objective = new("bym", "rounds", "objective:1.7");
    private static readonly ViewPlace W6 = new("bym", "rounds", "bym-mcp-gui-table-reads");

    [Test]
    public void BoardToProjectToObjectiveToW6_ThenBackThrice_ThenForwardThrice()
    {
        var history = new ViewHistory();
        history.Jump(Project);
        history.Jump(Objective);
        history.Jump(W6);

        Assert.That(history.Back(), Is.EqualTo(Objective));
        Assert.That(history.Back(), Is.EqualTo(Project), "the project, nothing selected");
        Assert.That(history.Back(), Is.EqualTo(ViewPlace.Board), "Back from a project's first view returns to the board");
        Assert.That(history.Back(), Is.Null);
        Assert.That(new[] { history.Forward(), history.Forward(), history.Forward() }, Is.EqualTo(new[] { Project, Objective, W6 }));
        Assert.That(history.CanGoForward, Is.False);
    }

    [Test]
    public void AJumpAfterBack_DropsWhatForwardWouldHaveReached()
    {
        var history = new ViewHistory();
        history.Jump(Project);
        history.Jump(W6);
        history.Back();

        history.Jump(Objective);

        Assert.That(history.CanGoForward, Is.False);
        Assert.That(history.Back(), Is.EqualTo(Project));
    }

    [Test]
    public void EscIsNotAJump_ItReplacesWhereTheOwnerIs()
    {
        var history = new ViewHistory();
        history.Jump(W6);

        history.Replace(Project);

        Assert.That(history.Current, Is.EqualTo(Project));
        Assert.That(history.Back(), Is.EqualTo(ViewPlace.Board));
    }

    [Test]
    public void AJumpToWhereTheOwnerIs_RecordsNothing()
    {
        var history = new ViewHistory();
        history.Jump(Project);
        history.Jump(Project);

        Assert.That(history.Back(), Is.EqualTo(ViewPlace.Board));
    }
}
