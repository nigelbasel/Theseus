using Theseus.App.ViewModels;
using Theseus.Core.Abstractions;

namespace Theseus.Tests.Windows;

/// <summary>
/// The shell as <see cref="MainWindowFixture"/>'s window binds it: a real board over fake stores, and recording
/// commands for the keys and menus under test. A member MainWindow binds that is not here leaves its binding empty;
/// add one when a test needs it.
/// </summary>
internal sealed class FakeShell
{
    /// <summary>Creates a shell over a board, with a drawer over the same plans.</summary>
    public FakeShell(BoardViewModel board, IPlanStore plans)
    {
        Board = board;
        Drawer = new DetailDrawerViewModel(plans, new NoRunLog(), new NoWorkflowRuns(), board.Persist);
    }

    /// <summary>The detail drawer, closed until a test shows a card in it.</summary>
    public DetailDrawerViewModel Drawer { get; }

    /// <summary>The board, with the fake cards.</summary>
    public BoardViewModel Board { get; }

    /// <summary>Whether the board, rather than a project, fills the main area.</summary>
    public bool IsBoardShown { get; set; } = true;

    /// <summary>Whether a project fills the main area.</summary>
    public bool IsProjectOpen { get; set; }

    /// <summary>Whether the toolbar is shown.</summary>
    public bool IsToolbarVisible { get; set; } = true;

    /// <summary>Whether the status bar is shown.</summary>
    public bool IsStatusBarVisible { get; set; } = true;

    /// <summary>Whether the live sessions panel is open.</summary>
    public bool IsSessionsPanelOpen { get; set; }

    /// <summary>Opens or closes the live sessions panel.</summary>
    public RecordingCommand ToggleSessionsPanelCommand { get; } = new();

    /// <summary>The locks panel.</summary>
    public FakePanel Locks { get; } = new();

    /// <summary>The chains panel.</summary>
    public FakePanel Chains { get; } = new();

    /// <summary>The day report.</summary>
    public FakeOpener DayReport { get; } = new();

    /// <summary>Opens a project tab by number.</summary>
    public RecordingCommand SelectProjectTabCommand { get; } = new();

    /// <summary>Opens the next project tab.</summary>
    public RecordingCommand NextProjectTabCommand { get; } = new();

    /// <summary>Opens the previous project tab.</summary>
    public RecordingCommand PreviousProjectTabCommand { get; } = new();
}
