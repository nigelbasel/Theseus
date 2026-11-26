using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// What the project view's buttons run (1.1d AC 27): the shell's existing Open spec, Accept spec, Independent check,
/// Focus, Start and Stop, each for one card by its key, and opening a records folder or file.
/// </summary>
/// <param name="OpenSpec">Opens a spec in its window.</param>
/// <param name="AcceptSpec">Accepts a spec, after a confirmation naming the piece of work and the commit.</param>
/// <param name="VerifyWithHoldouts">Creates a verifier card for a card's current piece of work.</param>
/// <param name="Focus">Brings a card's terminal forward.</param>
/// <param name="Start">Starts a card.</param>
/// <param name="Stop">Asks a card's session to write its handover and stop.</param>
/// <param name="OpenPath">Opens a records folder or a record in its editor.</param>
/// <param name="Notice">Says something in the status bar, such as that Properties is hidden (1.1d AC 52); null says nothing.</param>
/// <param name="WhyNot">
/// Why a button cannot run for a card now, by card key and action, or null when it can: it is then disabled with this as
/// its tooltip (1.1e AC 6a). Null asks nothing.
/// </param>
/// <param name="WayOn">The way on from a disabled button, offered beside it as a link (1.1e AC 6a), or null.</param>
/// <param name="AddToProject">
/// Add to project… for a piece no project file lists, by project and card key, with that project chosen (1.1e AC 6);
/// null offers none.
/// </param>
public sealed record ProjectViewActions(
    Action<SpecAtGate> OpenSpec,
    Action<SpecAtGate> AcceptSpec,
    Action<string> VerifyWithHoldouts,
    Action<string> Focus,
    Action<string> Start,
    Action<string> Stop,
    Action<string> OpenPath,
    Action<string>? Notice = null,
    Action<string, string>? AddToProject = null,
    Func<string, StageAction, string?>? WhyNot = null,
    Func<string, StageAction, PieceActionViewModel?>? WayOn = null)
{
    /// <summary>
    /// Whether a card's project is running, by card key, and if so who starts it and when; null where the owner is the one
    /// to start it (project-view-usability item 5). Its Start then becomes Start anyway…, and its box says who starts it.
    /// </summary>
    public Func<string, ProjectRunStart?>? WhoStarts { get; init; }

    /// <summary>
    /// Whether the owner has opened a later report, by link id and the report's time; null treats none as opened.
    /// Opening it clears its box's line (Nigel, 2026-11-23).
    /// </summary>
    public Func<string, DateTimeOffset, bool>? ReportOpened { get; init; }

    /// <summary>Opens a later report and remembers it, by link id, report time and document; null opens the file only.</summary>
    public Action<string, DateTimeOffset, string>? OpenReport { get; init; }

    /// <summary>
    /// Opens a piece's Records window, a stage's document chosen when one is named (Nigel, 2026-11-22);
    /// when null, Open records opens the folder as before.
    /// </summary>
    public Action<PieceOfWork, string?>? ShowRecords { get; init; }

    /// <summary>Opens a card's links and proposals on the board, where a report is confirmed or a proposal decided; null in tests.</summary>
    public Action<string>? OpenLinks { get; init; }

    /// <summary>
    /// A piece's card's own board actions, for its box and Properties: Archive, Remove from project or Unlink from its
    /// parent, Properties (Nigel, 2026-11-22; item 3 and walkthrough row 32, 2026-11-23); null in tests.
    /// </summary>
    public Func<PieceOfWork, IReadOnlyList<PieceActionViewModel>>? CardActions { get; init; }

    /// <summary>Told each jump within the view (a link followed), with what is now selected, for Back and Forward (1.1f I46).</summary>
    public Action<string?>? Jumped { get; init; }

    /// <summary>◀ Back, from the shell's history; null in tests.</summary>
    public System.Windows.Input.ICommand? GoBack { get; init; }

    /// <summary>Forward ▶, from the shell's history; null in tests.</summary>
    public System.Windows.Input.ICommand? GoForward { get; init; }

    /// <summary>
    /// Tells a card's session something with its next turn, by key: Ask the lead in the waits checklist (project-view-scale,
    /// 2026-11-24). Null where the view cannot.
    /// </summary>
    public Action<string, string>? TellCard { get; init; }

    /// <summary>Opens the proposal window for a proposal by its id (sessions-run-the-project 0.2); null in tests.</summary>
    public Action<string>? ReviewProposal { get; init; }

    /// <summary>Asks the owner to allow or refuse a start request by its id (sessions-run-the-project 13); null in tests.</summary>
    public Action<string>? AnswerStartRequest { get; init; }

    /// <summary>Starts a verifier card Theseus made, as Start again does; null starts it as a card's Start.</summary>
    public Action<string>? StartVerifier { get; init; }

    /// <summary>Clear up verifiers… for a project: stops, archives and rejects the verifier cards that should not be kept.</summary>
    public Action<string>? ClearVerifiers { get; init; }

    /// <summary>Resets a card's tripped breaker, by key (Nigel, 2026-11-24).</summary>
    public Action<string>? ResetBreaker { get; init; }

    /// <summary>Resets a card's tripped breaker and starts it, by key: the line's button for a card Theseus made.</summary>
    public Action<string>? ResetBreakerAndStart { get; init; }

    /// <summary>Opens Options, where the headless limits are raised.</summary>
    public Action? OpenOptions { get; init; }

    /// <summary>Undo of the last Clear up verifiers… of a project.</summary>
    public Action<string>? UndoClearVerifiers { get; init; }

    /// <summary>✕ Dismiss on a clear-up's notice: it can no longer be undone.</summary>
    public Action<string>? DismissClearUp { get; init; }

    /// <summary>The pending proposals a card raised, as the board's Chains panel shows them (1.1e item 30); null in tests.</summary>
    public Func<string, IReadOnlyList<ProposalRowViewModel>>? ProposalsOf { get; init; }
}
