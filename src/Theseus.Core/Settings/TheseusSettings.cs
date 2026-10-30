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

    /// <summary>
    /// The folder a spawned workstream's brief and report go in, named relative to the working
    /// directory of the workstream that spawned it, never as an absolute path (Nigel, 2026-10-21).
    /// </summary>
    public string BriefFolderName { get; init; } = Links.BriefFolder.DefaultName;

    /// <summary>Which rows were folded away, so a board left tidy opens tidy.</summary>
    public IReadOnlyList<string> CollapsedAreas { get; init; } = [];

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
