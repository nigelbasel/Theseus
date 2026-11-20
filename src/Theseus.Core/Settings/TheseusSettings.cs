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

    /// <summary>Which rows were folded away, so a board left tidy opens tidy.</summary>
    public IReadOnlyList<string> CollapsedAreas { get; init; } = [];

    /// <summary>
    /// The folder of project homes: each subfolder holding a <c>project.md</c> is a project, read as plain files (1.1b
    /// AC 1). Empty by default, so a board with none set behaves exactly as before (AC 13).
    /// </summary>
    public string ProjectsFolder { get; init; } = string.Empty;

    /// <summary>Where workstreams' worktrees go, <c>&lt;folder&gt;\&lt;slug&gt;\&lt;repo&gt;</c> (1.1b AC 11). Empty by default.</summary>
    public string WorktreesFolder { get; init; } = string.Empty;

    /// <summary>
    /// Where each project's card was dragged to, by project name. A project not listed sits in the first column and row
    /// (project-view AC 12).
    /// </summary>
    public IReadOnlyDictionary<string, ProjectCardPlace> ProjectCardPlaces { get; init; } =
        new Dictionary<string, ProjectCardPlace>();

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

    /// <summary>
    /// Where the board window was when it last closed, so it opens where it was left. Checked against
    /// the screens actually attached before it is used: a position saved while docked is a position
    /// on a monitor that may not be there any more.
    /// </summary>
    public Layout.WindowPlacement? MainWindow { get; init; }


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
