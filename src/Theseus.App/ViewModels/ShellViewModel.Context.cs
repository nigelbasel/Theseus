using System.Windows.Input;

namespace Theseus.App.ViewModels;

/// <summary>
/// The toolbar and menus act on what is shown (project-view-as-tabs, Nigel 2026-11-26: "We need all the options to act on
/// the currently viewed project"). On the board they act on the ticked cards. On a project's tab they act on that project.
/// On 1.5b's All projects tab the project commands say "Open a project's tab to act on it". One Board / Projects toggle
/// moves between the board and the project view, and the tab strip above the view holds All projects and each project.
/// </summary>
public sealed partial class ShellViewModel
{
    private object? _allProjectsView;
    private bool _isAllProjectsOpen;
    private bool _lastWasAllProjects;
    private IReadOnlyList<ProjectTabViewModel> _projectTabs = [];
    private string? _newOpening;
    private string? _newIntoProject;
    private ReasonedCommand? _toggleView;
    private ReasonedCommand? _openAllProjects;
    private ReasonedCommand? _newInContext;
    private ReasonedCommand? _projectStart;
    private ReasonedCommand? _projectStop;
    private ReasonedCommand? _projectClose;
    private ReasonedCommand? _projectArchive;
    private ReasonedCommand? _projectProperties;
    private ReasonedCommand? _projectFocusLead;
    private ReasonedCommand? _projectNew;
    private ReasonedCommand? _projectFoldAll;
    private ReasonedCommand? _projectUnfoldAll;

    /// <summary>
    /// 1.5b's kanban, set by its code: while it is null there is no All projects tab. The main window shows it, through an
    /// implicit data template of 1.5b's, while <see cref="IsAllProjectsOpen"/>.
    /// </summary>
    public object? AllProjectsView
    {
        get => _allProjectsView;
        set
        {
            if (Set(ref _allProjectsView, value))
            {
                if (value is null && _isAllProjectsOpen)
                {
                    CloseProject();
                }

                WireProjectChoices();
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    /// <summary>Whether the All projects tab fills the main area.</summary>
    public bool IsAllProjectsOpen => _isAllProjectsOpen && _allProjectsView is not null;

    /// <summary>Whether the project view, a project's tab or All projects, fills the main area.</summary>
    public bool IsProjectViewShown => IsProjectOpen || IsAllProjectsOpen;

    /// <summary>The strip's tabs: All projects first when it exists, then each project that reads and is open.</summary>
    public IReadOnlyList<ProjectTabViewModel> ProjectTabs
    {
        get => _projectTabs;
        private set
        {
            if (Set(ref _projectTabs, value))
            {
                Raise(nameof(OpenProjectTab));
            }
        }
    }

    /// <summary>The strip's selection: the open tab. Selecting another opens it.</summary>
    public ProjectTabViewModel? OpenProjectTab
    {
        get => _projectTabs.FirstOrDefault(tab => tab.IsOpen);
        set
        {
            if (value is { IsOpen: false, OpenCommand: { } open } && open.CanExecute(null))
            {
                open.Execute(null);
            }
        }
    }

    /// <summary>Opens the All projects tab (Ctrl+0); off while 1.5b's kanban is not there.</summary>
    public ReasonedCommand OpenAllProjectsCommand => _openAllProjects ??= new ReasonedCommand(_ => OpenAllProjects(),
        _ => _allProjectsView is null ? "There is no All projects view" : IsAllProjectsOpen ? "All projects is open" : null);

    /// <summary>The toolbar's one Board / Projects toggle, and Ctrl+B.</summary>
    public ReasonedCommand ToggleViewCommand => _toggleView ??= new ReasonedCommand(_ => ToggleView(),
        _ => IsProjectViewShown || ToggleTarget() is not null
            ? null
            : "No project can be opened. Projects are read from the projects folder set in Tools › Options.");

    /// <summary>The toggle's words: Projects on the board, Board in the view.</summary>
    public string ToggleViewText => ViewToggle.Text(IsProjectViewShown);

    /// <summary>The toggle's glyph.</summary>
    public string ToggleViewGlyph => ViewToggle.Glyph(IsProjectViewShown);

    /// <summary>The toggle's tooltip.</summary>
    public string ToggleViewToolTip => ViewToggle.ToolTip(IsProjectViewShown);

    /// <summary>New on the toolbar and Ctrl+N: a workstream, made in the open project when a project's tab is shown.</summary>
    public ReasonedCommand NewInContextCommand => _newInContext ??= new ReasonedCommand(_ =>
        {
            _newOpening = IsProjectOpen ? _openProjectName : null;
            NewWorkstream.OpenCommand.Execute(null);
        },
        _ => NewWorkstream.OpenCommand.CanExecute(null) ? null : "New workstream is open");

    /// <summary>New's words on the toolbar.</summary>
    public string NewInContextToolTip => IsProjectOpen
        ? $"New workstream in {ProjectView?.Title ?? "this project"}… (Ctrl+N)"
        : "New workstream (Ctrl+N)";

    /// <summary>Start the project, on the toolbar and the Project menu.</summary>
    public ReasonedCommand ProjectStartCommand => _projectStart ??= ForOpenProject(card => card.StartCommand,
        card => $"Nothing of {card.Title} is stopped or waiting to start");

    /// <summary>Stop the project…, on the toolbar and the Project menu.</summary>
    public ReasonedCommand ProjectStopCommand => _projectStop ??= ForOpenProject(card => card.StopCommand,
        card => $"Nothing of {card.Title} is running, no duty of it is live and its cards hold no lock");

    /// <summary>Close project…, on the toolbar's More and the Project menu.</summary>
    public ReasonedCommand ProjectCloseCommand => _projectClose ??= ForOpenProject(card => card.CloseCommand,
        card => $"{card.Title} cannot be closed just now");

    /// <summary>Archive project…, on the toolbar's More and the Project menu.</summary>
    public ReasonedCommand ProjectArchiveCommand => _projectArchive ??= ForOpenProject(card => card.ArchiveCommand,
        card => $"{card.Title} cannot be archived just now");

    /// <summary>Project properties, on the toolbar's More and the Project menu.</summary>
    public ReasonedCommand ProjectPropertiesCommand => _projectProperties ??= ForOpenProject(card => card.PropertiesCommand,
        card => $"{card.Title} has no properties to show");

    /// <summary>Focus lead session, on the Project menu.</summary>
    public ReasonedCommand ProjectFocusLeadCommand => _projectFocusLead ??= ForOpenProject(card => card.FocusLeadCommand,
        card => card.FocusLeadToolTip);

    /// <summary>New workstream in this project…, on the Project menu.</summary>
    public ReasonedCommand ProjectNewWorkstreamCommand => _projectNew ??= new ReasonedCommand(_ => NewInContextCommand.Execute(null),
        _ => ViewToggle.WhyNoProject(IsProjectViewShown, IsAllProjectsOpen)
             ?? (NewWorkstream.OpenCommand.CanExecute(null) ? null : "New workstream is open"));

    /// <summary>Fold all, on the toolbar in a project: every piece of work and heading.</summary>
    public ReasonedCommand ProjectFoldAllCommand => _projectFoldAll ??= new ReasonedCommand(_ => ProjectView?.CollapseAllCommand.Execute(null),
        _ => ViewToggle.WhyNoProject(IsProjectViewShown, IsAllProjectsOpen));

    /// <summary>Unfold all, on the toolbar in a project.</summary>
    public ReasonedCommand ProjectUnfoldAllCommand => _projectUnfoldAll ??= new ReasonedCommand(_ => ProjectView?.ExpandAllCommand.Execute(null),
        _ => ViewToggle.WhyNoProject(IsProjectViewShown, IsAllProjectsOpen));


    /// <summary>The open project's card, or null on the board and on All projects.</summary>
    private ProjectCardViewModel? OpenProjectCard => IsProjectOpen
        ? Board.ProjectCards.FirstOrDefault(card => string.Equals(card.Name, _openProjectName, StringComparison.OrdinalIgnoreCase))
        : null;

    /// <summary>A command that runs the open project's own command, and says why when there is no project or it cannot.</summary>
    private ReasonedCommand ForOpenProject(Func<ProjectCardViewModel, ICommand?> command, Func<ProjectCardViewModel, string> why) =>
        new(_ =>
            {
                if (OpenProjectCard is { } card && command(card) is { } run && run.CanExecute(null))
                {
                    run.Execute(null);
                }
            },
            _ => ViewToggle.WhyNoProject(IsProjectViewShown, IsAllProjectsOpen)
                 ?? (OpenProjectCard is not { } card ? "The project is not on the board"
                     : command(card) is { } run && run.CanExecute(null) ? null
                     : why(card)));

    private ViewTarget? ToggleTarget() =>
        ViewToggle.Target(TabNames().Where(name => !IsProjectClosed(name)).ToList(), _lastOpenedProject, _lastWasAllProjects,
            _allProjectsView is not null);

    private void ToggleView()
    {
        if (IsProjectViewShown)
        {
            CloseProject();

            return;
        }

        switch (ToggleTarget())
        {
            case { AllProjects: true }:
                OpenAllProjects();
                break;
            case { Project: { } project }:
                OpenProject(project);
                break;
        }
    }

    /// <summary>Shows 1.5b's kanban in the All projects tab.</summary>
    public void OpenAllProjects()
    {
        if (_allProjectsView is null)
        {
            return;
        }

        if (_openProjectName is not null)
        {
            _openProjectName = null;
            ProjectView = null;
        }

        _isAllProjectsOpen = true;
        _lastWasAllProjects = true;
        _viewHost.Opened();
        WireProjectChoices();
        ContextChanged();
    }

    /// <summary>Takes the All projects tab off the main area, for a project's tab or the board.</summary>
    private void LeaveAllProjects(bool forProject)
    {
        _isAllProjectsOpen = false;

        if (forProject)
        {
            _lastWasAllProjects = false;
        }
    }

    /// <summary>Everything bound to where you are, told it changed.</summary>
    private void ContextChanged()
    {
        Raise(nameof(IsAllProjectsOpen));
        Raise(nameof(IsProjectViewShown));
        Raise(nameof(IsProjectOpen));
        Raise(nameof(IsBoardShown));
        Raise(nameof(ToggleViewText));
        Raise(nameof(ToggleViewGlyph));
        Raise(nameof(ToggleViewToolTip));
        Raise(nameof(NewInContextToolTip));
        Raise(nameof(OpenProjectTab));
        CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>The strip's tabs from the project tabs just built, with All projects first when it exists.</summary>
    private void SetProjectTabs(IReadOnlyList<ProjectTabViewModel> projects) =>
        ProjectTabs = _allProjectsView is null
            ? projects
            : [ProjectTabViewModel.AllProjects(IsAllProjectsOpen, OpenAllProjectsCommand), .. projects];

    /// <summary>The project a New made from a project's tab goes into, taken once by the card it made.</summary>
    private string? TakeNewIntoProject()
    {
        var project = _newIntoProject;
        _newIntoProject = null;

        return project;
    }

    /// <summary>Remembers, as New workstream opens, whether it was opened from a project's tab.</summary>
    private void OnNewWorkstreamChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NewWorkstreamViewModel.IsOpen) && NewWorkstream.IsOpen)
        {
            _newIntoProject = _newOpening;
            _newOpening = null;
        }
    }
}
