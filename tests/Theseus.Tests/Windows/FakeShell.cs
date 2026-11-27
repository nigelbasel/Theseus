using Theseus.App.ViewModels;
using Theseus.Core.Abstractions;

namespace Theseus.Tests.Windows;

/// <summary>
/// The shell as <see cref="MainWindowFixture"/>'s window binds it: a real board over fake stores, and recording
/// commands for the keys and menus under test. A member MainWindow binds that is not here leaves its binding empty;
/// add one when a test needs it.
/// </summary>
internal sealed class FakeShell : System.ComponentModel.INotifyPropertyChanged
{
    private string _notice = string.Empty;
    private NoticeSeverity _noticeSeverity;
    private RecordingCommand? _startSelected;
    private RecordingCommand? _stopSelected;
    private RecordingCommand? _archiveSelected;

    /// <inheritdoc />
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The status bar's notice.</summary>
    public string Notice
    {
        get => _notice;
        set
        {
            _notice = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Notice)));
        }
    }

    /// <summary>How serious the notice is.</summary>
    public NoticeSeverity NoticeSeverity
    {
        get => _noticeSeverity;
        set
        {
            _noticeSeverity = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(NoticeSeverity)));
        }
    }

    /// <summary>Creates a shell over a board, with a drawer over the same plans.</summary>
    public FakeShell(BoardViewModel board, IPlanStore plans)
    {
        Board = board;
        Drawer = new DetailDrawerViewModel(plans, new NoRunLog(), new NoWorkflowRuns(), board.Persist);
        Refreshing = new RefreshRun(() => { }, () => RefreshCheck.Task, () => Notice, text => Notice = text, () => Task.CompletedTask);
    }

    /// <summary>
    /// Whether the board's card commands can run: false is the project view, where no card can be ticked. Setting it gives
    /// the window new commands, so its buttons ask them again.
    /// </summary>
    public bool CardCommandsCanRun
    {
        get;
        set
        {
            field = value;
            _startSelected = _stopSelected = _archiveSelected = null;

            foreach (string name in new[] { nameof(StartSelectedCommand), nameof(StopSelectedCommand), nameof(ArchiveSelectedCommand) })
            {
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
            }
        }
    } = true;

    /// <summary>Start selected, which runs only while <see cref="CardCommandsCanRun"/>.</summary>
    public RecordingCommand StartSelectedCommand => _startSelected ??= Card();

    /// <summary>Stop selected, which runs only while <see cref="CardCommandsCanRun"/>.</summary>
    public RecordingCommand StopSelectedCommand => _stopSelected ??= Card();

    /// <summary>Archive selected, which runs only while <see cref="CardCommandsCanRun"/>.</summary>
    public RecordingCommand ArchiveSelectedCommand => _archiveSelected ??= Card();

    /// <summary>The toolbar's Board / Projects toggle, View's item and Ctrl+B.</summary>
    public RecordingCommand ToggleViewCommand { get; } = new();

    /// <summary>The toggle's words, as the shell gives them on the board.</summary>
    public string ToggleViewText => IsProjectViewShown ? "Board" : "Projects";

    /// <summary>The toggle's tooltip.</summary>
    public string ToggleViewToolTip => "Open the project view where you left it (Ctrl+B)";

    /// <summary>Whether the project view, a project or All projects, fills the main area.</summary>
    public bool IsProjectViewShown { get; private set; }

    /// <summary>Shows the project view, as opening a project does, and tells the window.</summary>
    public void ShowProjectView()
    {
        IsProjectViewShown = true;
        IsProjectOpen = true;
        IsBoardShown = false;

        foreach (string name in new[] { nameof(IsProjectViewShown), nameof(IsProjectOpen), nameof(IsBoardShown), nameof(ToggleViewText) })
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
        }
    }

    /// <summary>The update check a refresh waits for: a test answers it.</summary>
    public TaskCompletionSource<string> RefreshCheck { get; } = new();

    /// <summary>Refresh as it runs, the real one over a check the test answers.</summary>
    public RefreshRun Refreshing { get; }

    /// <summary>Refresh (F5, the toolbar and View › Refresh).</summary>
    public RelayCommand RefreshCommand => Refreshing.Command;

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
    public FakeChains Chains { get; } = new();

    /// <summary>The Sessions panel.</summary>
    public FakeSessions Sessions { get; } = new();

    /// <summary>The day report.</summary>
    public FakeOpener DayReport { get; } = new();

    /// <summary>Opens a project tab by number.</summary>
    public RecordingCommand SelectProjectTabCommand { get; } = new();

    /// <summary>Opens the next project tab.</summary>
    public RecordingCommand NextProjectTabCommand { get; } = new();

    /// <summary>Opens the previous project tab.</summary>
    public RecordingCommand PreviousProjectTabCommand { get; } = new();

    /// <summary>The All projects tab's kanban, or null while there is none.</summary>
    public object? AllProjectsView { get; private set; }

    /// <summary>Whether All projects fills the main area.</summary>
    public bool IsAllProjectsOpen { get; private set; }

    /// <summary>The strip's tabs.</summary>
    public IReadOnlyList<ProjectTabViewModel> ProjectTabs { get; private set; } = [];

    /// <summary>The strip's open tab.</summary>
    public ProjectTabViewModel? OpenProjectTab
    {
        get => ProjectTabs.FirstOrDefault(tab => tab.IsOpen);
        set { }
    }

    /// <summary>View › All projects layout › Fold all rows.</summary>
    public RecordingCommand KanbanFoldAllCommand { get; } = new();

    /// <summary>Opens All projects on a kanban, as Ctrl+0 does, and tells the window.</summary>
    public void ShowAllProjects(object kanban, IReadOnlyList<ProjectTabViewModel> projects)
    {
        AllProjectsView = kanban;
        IsAllProjectsOpen = true;
        IsProjectViewShown = true;
        IsBoardShown = false;
        ProjectTabs = [ProjectTabViewModel.AllProjects(true, null), .. projects];

        foreach (string name in new[] { nameof(AllProjectsView), nameof(IsAllProjectsOpen), nameof(IsProjectViewShown), nameof(IsBoardShown),
                     nameof(ProjectTabs), nameof(OpenProjectTab), nameof(ToggleViewText) })
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
        }
    }

    private RecordingCommand Card()
    {
        bool canRun = CardCommandsCanRun;

        return new RecordingCommand(_ => canRun);
    }
}
