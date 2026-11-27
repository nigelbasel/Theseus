namespace Theseus.Core.Settings;

/// <summary>Everything the user can configure.</summary>
/// <param name="PlanDirectory">Where the Claude Code sessions write their plan files.</param>
/// <param name="TerminalWindowName">The named Windows Terminal window tabs are launched into.</param>
/// <param name="TerminalExecutablePath">
/// A terminal chosen by hand. Empty means resolve it from whatever is running, which is almost
/// always right and is the only way to be sure a named window can be joined.
/// </param>
/// <param name="ClaudeExecutablePath">
/// Claude Code chosen by hand. Empty means find it in the usual place or on the path.
/// </param>
/// <param name="JiraBaseUrl">
/// The Jira instance issue keys link to, for example <c>https://example.atlassian.net</c>. Empty
/// means keys are still shown on cards but are not links.
/// </param>
/// <param name="SessionPreamble">
/// Standing instructions put in front of every workstream's own opening prompt. Composed at launch,
/// so changing it changes every future session without rewriting any plan file.
/// </param>
/// <param name="StopPrompt">
/// What a session is told when it is asked to stop. Use {handoff} for the file it should write.
/// </param>
/// <param name="ContinuePrompt">
/// What a workstream that has run before is told when it opens again, instead of its original
/// prompt. Use {handoff} for the path to its handover notes.
/// </param>
/// <param name="SessionsPanelCollapsed">
/// Whether the live sessions panel is folded away. Named for the collapsed state rather than the
/// open one so that a settings file written before this existed reads as open, which is the default.
/// </param>
/// <param name="Lanes">
/// The board's columns, in order. The last is where finished work goes and is the one lane a
/// whole-board Start leaves alone, so the order carries meaning and is not merely cosmetic.
/// </param>
/// <param name="Theme">Which theme to paint in.</param>
public sealed record TheseusSettings(
    string PlanDirectory,
    string TerminalWindowName,
    string TerminalExecutablePath,
    string ClaudeExecutablePath,
    string JiraBaseUrl,
    string SessionPreamble,
    string ContinuePrompt,
    string StopPrompt,
    bool SessionsPanelCollapsed,
    IReadOnlyList<string> Lanes,
    AppTheme Theme)
{
    /// <summary>
    /// Whether the board was last left showing what has been archived. Remembered so that going
    /// through what was put away survives a restart, the way the rest of the board's arrangement does.
    /// </summary>
    public bool ShowArchived { get; init; }

    /// <summary>Which cards the board shows: all, only project cards, or only workstreams (Nigel, 2026-11-23).</summary>
    public BoardShows BoardShows { get; init; }

    /// <summary>
    /// How Start and Restart bring back a workstream that has run before, when nothing more specific
    /// is asked for. Resume by default, because that is what every earlier build did: changing what a
    /// button does underneath somebody is worse than leaving the cheaper choice one menu away.
    /// </summary>
    public Launching.StartMode DefaultStartMode { get; init; } = Launching.StartMode.Resume;

    /// <summary>
    /// What opens every session that starts without its conversation - a first run or a fresh
    /// start - saying which workstream it is and where its notes go. Empty means the
    /// built-in text, so a board that never changed it picks up any improvement to that text.
    /// </summary>
    public string IdentityPrompt { get; init; } = string.Empty;

    /// <summary>
    /// The rows of the board: the kinds of work, as opposed to the lanes, which are how far along it
    /// is. Empty on a board configured before the grid existed, which is the signal to derive both
    /// axes from the single row of lanes it had.
    /// </summary>
    public IReadOnlyList<string> Areas { get; init; } = [];

    /// <summary>The figures behind the cores-held-back warning in the status bar (parked-cores, 2026-11-19).</summary>
    public CoreClampSettings CoreClamp { get; init; } = new();

    /// <summary>The limits for duties and the overnight queue.</summary>
    public DutySettings Duties { get; init; } = new();

    /// <summary>
    /// The folder a spawned workstream's brief and report go in, named relative to the working
    /// directory of the workstream that spawned it, never as an absolute path (Nigel, 2026-10-21).
    /// </summary>
    public string BriefFolderName { get; init; } = Links.BriefFolder.DefaultName;

    /// <summary>
    /// The clone of the private holdout scenarios. The holdout guard reads it from this file (it keeps no path of its
    /// own, P19), and a verifier card has it in reach to read the scenarios and write its report. Empty: no holdout, and
    /// the guard allows everything
    /// (verifier-card AC 4 and 7, Nigel 2026-11-19).
    /// </summary>
    public string HoldoutFolder { get; init; } = string.Empty;

    /// <summary>The holdout's remote address, such as host/owner/repo, which the guard also refuses to non-verifiers.</summary>
    public string HoldoutRemote { get; init; } = string.Empty;

    /// <summary>The clear-up of verifier cards under way or last made, so a restart neither loses its archives nor Undo.</summary>
    public Theseus.Core.Projects.VerifierClearUpRecord? VerifierClearUp { get; init; }

    /// <summary>The guards' setup findings the board's notice was last given for, so the same ones are said once.</summary>
    public string? GuardNoticeFor { get; init; }

    /// <summary>The source-control hosts auto mode trusts for pushes, entered once in Options, Guards; the owner's own.</summary>
    public IReadOnlyList<string> AutoModeSourceControlHosts { get; init; } = [];

    /// <summary>The domains auto mode trusts by name, entered once in Options, Guards.</summary>
    public IReadOnlyList<string> AutoModeInternalDomains { get; init; } = [];

    /// <summary>Words that mark a host, namespace or resource as production to auto mode, entered once in Options, Guards.</summary>
    public IReadOnlyList<string> AutoModeProductionPatterns { get; init; } = ["prod", "production", "live"];

    /// <summary>Which rows were folded away, so a board left tidy opens tidy.</summary>
    public IReadOnlyList<string> CollapsedAreas { get; init; } = [];

    /// <summary>
    /// The folder of project homes: each subfolder holding a <c>project.md</c> is a project, read as plain files (1.1b
    /// AC 1). Empty by default, so a board with none set behaves exactly as before (AC 13).
    /// </summary>
    public string ProjectsFolder { get; init; } = string.Empty;

    /// <summary>
    /// Whether the owner finished or skipped the first-run setup, so it does not open by itself again; Tools › Set up Theseus…
    /// still opens it (first-run-setup, Nigel 2026-11-25).
    /// </summary>
    public bool FirstRunSetupDone { get; init; }

    /// <summary>Where workstreams' worktrees go, <c>&lt;folder&gt;\&lt;slug&gt;\&lt;repo&gt;</c> (1.1b AC 11). Empty by default.</summary>
    public string WorktreesFolder { get; init; } = string.Empty;

    /// <summary>
    /// Where the primary clones are, by the layout rule: a repository's remote gives its clone's place in it,
    /// <c>&lt;folder&gt;\&lt;host&gt;\&lt;owner&gt;\&lt;repo&gt;</c> (worktree-per-repo AC 18). Empty by default, and with it
    /// or the worktrees folder empty a start makes no worktrees (AC 1).
    /// </summary>
    public string ClonesFolder { get; init; } = string.Empty;

    /// <summary>
    /// The base folders Theseus keeps trusted in Claude Code, each as one plain-folder record that covers every folder under
    /// it (objective 1.3f, Nigel 2026-11-27: "We want to have the base directories, claude-hq, and work trusted, so that all
    /// subdirectories are also trusted"). Choosing them is the consent; there is no switch. Null until first set, when they
    /// default to the projects folder's root and the worktrees folder; empty after Undo, so nothing is trusted again.
    /// </summary>
    public IReadOnlyList<string>? BaseFolders { get; init; }

    /// <summary>
    /// Where each project's card was dragged to, by project name. A project not listed sits in the first column and row
    /// (project-view AC 12).
    /// </summary>
    public IReadOnlyDictionary<string, ProjectCardPlace> ProjectCardPlaces { get; init; } =
        new Dictionary<string, ProjectCardPlace>();

    /// <summary>
    /// Each project's own colour, as <c>#rrggbb</c>, by project name (1.1d AC 40): allocated as a workstream's is when a
    /// project first shows, and changed only in its Project properties.
    /// </summary>
    public IReadOnlyDictionary<string, string> ProjectColours { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Each project card's tags, by project name (card-search-and-tags item 9): a project card has no sidecar, so its
    /// tags are kept here, beside its place and colour. Per machine, never in a repo.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ProjectTags { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>
    /// Each tag's colour, as <c>#rrggbb</c>, by tag (item 11): chosen from the tag palette on first use and changeable in
    /// Options, Tags.
    /// </summary>
    public IReadOnlyDictionary<string, string> TagColours { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Which one-off tidies of the grid this board has had. Zero for a board from before any; see
    /// <see cref="Domain.BoardTidy"/>.
    /// </summary>
    public int BoardRevision { get; init; }

    /// <summary>
    /// Whether the update check also offers pre-releases: the builds CI publishes from a project's primary branch, so
    /// the owner can try them live before merging into main (Nigel, 2026-11-20). Off reads releases from main only.
    /// </summary>
    public bool IncludePreReleases { get; init; }

    /// <summary>Whether the toolbar was hidden from View. Named for hidden so an older file reads as shown.</summary>
    public bool ToolbarHidden { get; init; }

    /// <summary>Whether the status bar was hidden from View, likewise.</summary>
    public bool StatusBarHidden { get; init; }

    /// <summary>What the board's columns show: stages, or what each session is doing.</summary>
    public BoardColumnsBy ColumnsBy { get; init; }

    /// <summary>What the board's rows show: areas, projects, chains, or one row.</summary>
    public BoardRowsBy RowsBy { get; init; }

    /// <summary>
    /// What is folded in the views other than stages and areas, keyed "columns:SessionState",
    /// "rows:Project" and so on, so each view opens the way it was left.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ViewFolds { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>
    /// How wide each column was left, as a share relative to the others (1 is the even share), keyed
    /// by view and column such as "Stage:In progress". A column not listed takes the even share.
    /// </summary>
    public IReadOnlyDictionary<string, double> ColumnWeights { get; init; } = new Dictionary<string, double>();

    /// <summary>
    /// How tall each row was left in pixels, keyed by view and row such as "Area:Dev". A row not
    /// listed fits its cards.
    /// </summary>
    public IReadOnlyDictionary<string, double> RowHeights { get; init; } = new Dictionary<string, double>();

    /// <summary>
    /// How wide each resizable pane was left in pixels, keyed by view and pane such as "ProjectView:Pane" (1.1d AC 35).
    /// A pane not listed takes its default width.
    /// </summary>
    public IReadOnlyDictionary<string, double> PaneSizes { get; init; } = new Dictionary<string, double>();

    /// <summary>
    /// How each project's view groups its pieces of work, by project name (1.1d AC 45). A project not listed shows the
    /// flow of hand-overs.
    /// </summary>
    public IReadOnlyDictionary<string, ProjectViewGrouping> ProjectGrouping { get; init; } =
        new Dictionary<string, ProjectViewGrouping>();

    /// <summary>Whether the projects kanban also shows closed and archived projects (projects-kanban spec AC 4).</summary>
    public bool KanbanShowsClosed { get; init; }

    /// <summary>
    /// Each stopped project's run state, by project name (1.1e AC 8; Nigel, 2026-11-21: "when I restart the project, it
    /// would go back to the previous state"): the keys of its cards that had a session when it was stopped. A project not
    /// listed is not stopped. Its Start starts exactly these and removes the entry.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ProjectRunStates { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>
    /// The projects the owner closed or archived, by project name (archive-a-project, 2026-11-26): each with what its
    /// close changed, so Reopen undoes exactly that. Kept here, never in project.md, whose records are never moved.
    /// </summary>
    public IReadOnlyDictionary<string, Projects.ProjectClosure> ClosedProjects { get; init; } =
        new Dictionary<string, Projects.ProjectClosure>();

    /// <summary>
    /// The projects the owner stopped with Stop the project, by project name (project-view-as-tabs, 2026-11-26): what the
    /// stop paused, so Start resumes exactly that, and the cards whose locks are still to release once they have stopped.
    /// </summary>
    public IReadOnlyDictionary<string, Projects.ProjectStopping> StoppedProjects { get; init; } =
        new Dictionary<string, Projects.ProjectStopping>();

    /// <summary>Each project's lead card, by project name, as chosen in Project properties (Nigel, 2026-11-22).</summary>
    public IReadOnlyDictionary<string, string> ProjectLeads { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// The cards the owner stopped by hand while their project ran (1.1e AC 10, 12): the project does not start them again
    /// until they are started by hand. A start of the card removes it.
    /// </summary>
    public IReadOnlyList<string> CardsStoppedByOwner { get; init; } = [];

    /// <summary>
    /// The projects whose owner granted branch rules, by project folder name, each pinned to the primary branch it covers
    /// (1.1e AC 12b): their cards may branch off it, push and merge back without asking. Kept here, never read from
    /// project.md.
    /// </summary>
    public IReadOnlyDictionary<string, ProjectBranchGrant> ProjectBranchRules { get; init; } = new Dictionary<string, ProjectBranchGrant>();

    /// <summary>
    /// The cards given branch rules at their last start, so a resume after the grant is withdrawn is told so (review of
    /// 1.1e PR 2c, finding 3).
    /// </summary>
    public IReadOnlyList<string> CardsToldBranchRules { get; init; } = [];

    /// <summary>
    /// The projects the owner set unattended in Project properties, by folder name (2.4 W2): their cards are refused the
    /// question tool and told to park, are ended once they have reported back, and are resumed after a transport error.
    /// Off for every project until he sets it; kept here, never read from project.md, which sessions write.
    /// </summary>
    public IReadOnlyList<string> UnattendedProjects { get; init; } = [];

    /// <summary>
    /// The folders in the projects folder the owner said are not projects, by name (1.1e AC 20): their cards are not shown;
    /// Options lists them and shows them again.
    /// </summary>
    public IReadOnlyList<string> HiddenProjectFolders { get; init; } = [];

    /// <summary>
    /// The holdout folder the owner chose for a project, by project folder name (1.1e AC 6e): first in the order a
    /// project's holdout is found; Reset in Options removes it.
    /// </summary>
    public IReadOnlyDictionary<string, string> ProjectHoldoutFolders { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// The projects whose updater has run while their file does not read, by project folder name (review of 1.1e PR 4):
    /// the fingerprint of the file when it started, or empty once it has been asked to stop. Its card says "Still needs
    /// updating" while the name is here; the name leaves once the file reads and the updater has stopped.
    /// </summary>
    public IReadOnlyDictionary<string, string> ProjectUpdaterRuns { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// The terminal window an area's sessions start in, where it is not named after the area itself:
    /// an area renamed by <see cref="Domain.BoardTidy"/> keeps its old window, so its new sessions
    /// join the tabs already open rather than starting a window of their own.
    /// </summary>
    public IReadOnlyDictionary<string, string> AreaWindows { get; init; } = new Dictionary<string, string>();

    /// <summary>Which columns were folded away, by stage name, for the same reason.</summary>
    public IReadOnlyList<string> CollapsedStages { get; init; } = [];

    /// <summary>
    /// Which cards were folded, by key. A card archived or hidden while folded keeps its place here,
    /// so it comes back folded.
    /// </summary>
    public IReadOnlyList<string> CollapsedCards { get; init; } = [];

    /// <summary>Which project cards were folded, by project name, as cards are.</summary>
    public IReadOnlyList<string> CollapsedProjects { get; init; } = [];

    /// <summary>Whether the board shows only what is active: cards with a session or waiting on the owner (Nigel, 2026-11-24).</summary>
    public bool ShowActiveOnly { get; init; }

    /// <summary>The Chains panel's filters, as last chosen; Hide completed on by default (Nigel, 2026-11-25).</summary>
    public Links.ChainFilters ChainsFilters { get; init; } = new();

    /// <summary>
    /// The Chains rows the owner folded (false) or unfolded (true) by hand, by card key, or <c>project:&lt;name&gt;</c> for a
    /// project's header. A row not here takes its default: folded when everything below it is finished (Nigel, 2026-11-25).
    /// </summary>
    public IReadOnlyDictionary<string, bool> ChainsFolds { get; init; } = new Dictionary<string, bool>();

    /// <summary>
    /// The locks panel's folds the owner chose, by <c>lock:&lt;name&gt;</c>, <c>queue:&lt;name&gt;</c> or <c>section:&lt;name&gt;</c>.
    /// A fold not here takes its default: a lock folded to one line, its queue open (Nigel, 2026-11-26).
    /// </summary>
    public IReadOnlyDictionary<string, bool> LocksFolds { get; init; } = new Dictionary<string, bool>();

    /// <summary>
    /// Where the board window was when it last closed, so it opens where it was left. Checked against
    /// the screens actually attached before it is used: a position saved while docked is a position
    /// on a monitor that may not be there any more.
    /// </summary>
    public Layout.WindowPlacement? MainWindow { get; init; }

    /// <summary>Where the spec window was last left, so the next one opens there (1.1e AC 24a).</summary>
    public Layout.WindowPlacement? SpecWindow { get; init; }


    /// <summary>The limits headless runs work inside, unless their project has its own (Nigel, 2026-11-24).</summary>
    public Budgets.BudgetSettings Budgets { get; init; } = Theseus.Core.Budgets.BudgetSettings.Default;

    /// <summary>A project's own limits for its headless runs, by project name, replacing <see cref="Budgets"/> whole.</summary>
    public IReadOnlyDictionary<string, Budgets.BudgetSettings> ProjectBudgets { get; init; } =
        new Dictionary<string, Budgets.BudgetSettings>();

    /// <summary>The thresholds of the circuit breakers on every card (Nigel, 2026-11-24).</summary>
    public Breakers.BreakerSettings Breakers { get; init; } = Theseus.Core.Breakers.BreakerSettings.Default;

    /// <summary>How many headless runs may go at once on this machine; the rest queue (Nigel, 2026-11-24).</summary>
    public int MaxHeadlessRuns { get; init; } = 3;

    /// <summary>
    /// The card whose sessions may rule on resources the lock table does not know, with rule_resource. None by
    /// default, so an unknown resource stays exclusive of everything until Nigel names one.
    /// </summary>
    public string? LockArbiterCard { get; init; }

    /// <summary>The run mode a project's cards take when a card has none of its own, by project name.</summary>
    public IReadOnlyDictionary<string, Running.RunMode> ProjectRunModes { get; init; } =
        new Dictionary<string, Running.RunMode>();

    /// <summary>The budget limits in force for a project's cards: its own, or the app's.</summary>
    public Budgets.BudgetSettings BudgetsFor(string? project) =>
        project is not null && ProjectBudgets.TryGetValue(project, out var own) ? own : Budgets;

    /// <summary>Where workstreams are kept until the user points Theseus somewhere of their own.</summary>
    public static string DefaultPlanDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Theseus", "workstreams");

    /// <summary>The settings a first run starts from.</summary>
    public static TheseusSettings Default { get; } = new(
        DefaultPlanDirectory,
        "theseus",
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        false,
        Domain.BoardLanes.Default,
        AppTheme.System);
}
