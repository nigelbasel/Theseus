using System.Windows.Input;
using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// One box of the project view (1.1d AC 24 to 28): a piece of work, folded to a strip of its stages or open into its
/// chain and actions; the pieces it hands work to are its children, though each box is drawn under its own state. What it shows comes from the model
/// (<see cref="PieceOfWork"/>, <see cref="PieceNode"/>); nothing here works state out again.
/// </summary>
public sealed class PieceNodeViewModel : ObservableObject, IFoldable
{
    /// <summary>A folded box's width.</summary>
    public const double FoldedWidth = 290;

    /// <summary>An open box's width.</summary>
    public const double OpenWidth = 640;

    /// <summary>The stripe and dot of a piece whose card is not on the board.</summary>
    public const string NoCardColour = "#8a9490";

    private bool _isOpen;
    private bool _isSelected;
    private bool _isShown = true;
    private bool _isHighlighted;

    internal PieceNodeViewModel(PieceNode node, IReadOnlyList<PieceNodeViewModel> children,
        IReadOnlyList<StageNodeViewModel> stages, IReadOnlyList<PieceActionViewModel> actions, bool isOpen,
        Action<PieceNodeViewModel> toggle, Action<PieceNodeViewModel> select)
    {
        Node = node;
        Status = node.Piece.Status;
        Children = children;
        Stages = stages;
        Actions = actions;
        _isOpen = isOpen;
        ToggleCommand = new RelayCommand(_ => toggle(this));
        SelectCommand = new RelayCommand(_ => select(this));

        foreach (var stage in stages)
        {
            stage.Piece = this;
        }
    }

    /// <summary>Its place in the tree.</summary>
    public PieceNode Node { get; }

    /// <summary>The piece of work.</summary>
    public PieceOfWork Piece => Node.Piece;

    /// <summary>Its name.</summary>
    public string Name => Piece.Name;

    /// <summary>The name of the workstream that works it.</summary>
    public string Workstream => Piece.Workstream;

    /// <summary>Its workstream's colour, or grey when its card is not on the board.</summary>
    public string ColourHex => Piece.Card?.Colour ?? NoCardColour;

    /// <summary>"feature", "bug" or "freeform".</summary>
    public string KindWord => Piece.KindWord;

    /// <summary>"objective 1.1", or "not in project.md".</summary>
    public string ObjectiveLabel => Piece.ObjectiveLabel;

    /// <summary>"project branch rules" while its card's project has them, else empty (1.1e AC 12b).</summary>
    public string BranchRulesMark => Piece.Card?.BranchRulesSince is null ? string.Empty : Theseus.Core.Projects.ProjectBranchRules.Mark;

    /// <summary>The mark's tooltip, with the grant's date.</summary>
    public string BranchRulesToolTip => Piece.Card?.BranchRulesSince is { } since
        ? $"Granted by you on {since.ToLocalTime():d MMM yyyy}: its card may branch off the primary branch, push, and merge back without asking"
        : string.Empty;

    /// <summary>The meta line in full, for its tooltip when it is trimmed.</summary>
    public string MetaText => $"{Workstream} · {KindWord} · {ObjectiveLabel}";

    /// <summary>Where it is (AC 7).</summary>
    public PieceStatus Status { get; }

    /// <summary>Its status's state, which colours its status line.</summary>
    public StageNodeState State => Status.State;

    /// <summary>The status line: mark and sentence.</summary>
    public string StatusText => $"{Status.Mark} {Status.Text}";

    /// <summary>Its report-back mark, or empty (AC 14).</summary>
    public string ReportBackText => Node.ReportBack?.Text ?? string.Empty;

    /// <summary>Whether it has a report-back mark.</summary>
    public bool HasReportBack => Node.ReportBack is not null;

    /// <summary>Whether its report was lost, shown in the Stuck colour.</summary>
    public bool IsReportLost => Node.ReportBack?.IsLost == true;

    /// <summary>The mark's tooltip, such as "Reported back to buildyourmarket-mcp-server-spec".</summary>
    public string ReportBackToolTip =>
        ReportBackText.Length == 0 ? string.Empty : char.ToUpperInvariant(ReportBackText[2]) + ReportBackText[3..];

    /// <summary>Its stages, in pipeline order.</summary>
    public IReadOnlyList<StageNodeViewModel> Stages { get; }

    /// <summary>The open box's actions, in the order AC 27 lists them, each only when it applies.</summary>
    public IReadOnlyList<PieceActionViewModel> Actions { get; }

    /// <summary>The pieces it hands work to, in file order.</summary>
    public IReadOnlyList<PieceNodeViewModel> Children { get; }

    /// <summary>
    /// "● running", "○ idle" or "◆ waiting" while its card's session works this piece; empty otherwise (item 8).
    /// </summary>
    public string SessionMark => Piece.IsCurrentPiece && Piece.Card is { } card && FocusAction is not null
        ? Theseus.Core.Domain.SessionMark.Of(card.Lifecycle)
        : string.Empty;

    /// <summary>Whether the mark and ⌖ Focus show on its title bar.</summary>
    public bool HasSessionMark => SessionMark.Length > 0;

    /// <summary>Its card's Focus, on the title bar while its session runs: its tab in one click (item 8); null otherwise.</summary>
    public PieceActionViewModel? FocusAction =>
        Piece.IsCurrentPiece && Piece.Card is { HasSession: true }
            ? Actions.FirstOrDefault(action => action.Label.StartsWith("⧉ Focus", StringComparison.Ordinal) && action.IsEnabled)
            : null;

    /// <summary>"Reported again after you confirmed: read it", when its session reported after the confirmation; null otherwise.</summary>
    public string? ReportedAgain => LaterReport is { } report
        ? $"Reported again after you confirmed{(report.Verdict is { } verdict ? $" (verdict {verdict})" : string.Empty)}: read it"
        : null;

    /// <summary>Whether that line shows.</summary>
    public bool HasReportedAgain => ReportedAgain is not null;

    /// <summary>The later report while the owner has not opened it; null once he has (Nigel, 2026-11-23). Set by the view.</summary>
    public Theseus.Core.Links.LinkReport? LaterReport { get; init; }

    /// <summary>Opens the later report; null with none. Set by the view.</summary>
    public System.Windows.Input.ICommand? OpenLaterReport { get; init; }

    /// <summary>Whether it is open into its stages; folded by default.</summary>
    public bool IsOpen
    {
        get => _isOpen;
        internal set
        {
            if (Set(ref _isOpen, value))
            {
                Raise(nameof(IsFolded));
                Raise(nameof(Chevron));
                Raise(nameof(ChevronToolTip));
                Raise(nameof(BoxWidth));
            }
        }
    }

    bool IFoldable.IsOpen
    {
        get => IsOpen;
        set => IsOpen = value;
    }

    /// <inheritdoc />
    public string FoldName => Name;

    /// <summary>A box starts folded, the mockup's first view (AC 33).</summary>
    public bool StartsOpen => false;

    /// <summary>Whether it shows the strip rather than the chain.</summary>
    public bool IsFolded => !_isOpen;

    /// <summary>▸ folded, ▾ open.</summary>
    public string Chevron => _isOpen ? "▾" : "▸";

    /// <summary>What the chevron does.</summary>
    public string ChevronToolTip => _isOpen ? "Fold this piece of work" : "Open into its stages";

    /// <summary>The widest it is: 290 px folded, 640 px open; narrower when the flow leaves less (AC 31, 54).</summary>
    public double BoxWidth => _isOpen ? OpenWidth : FoldedWidth;

    /// <summary>Whether it, or one of its stages, is what the Properties pane shows.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        internal set => Set(ref _isSelected, value);
    }

    /// <summary>Folds or opens it, and remembers which.</summary>
    public ICommand ToggleCommand { get; }

    /// <summary>Selects the piece itself.</summary>
    public ICommand SelectCommand { get; }

    /// <summary>The Links lines of its Properties, each with the link's state in words.</summary>
    public IReadOnlyList<string> LinkLines { get; init; } = [];

    /// <summary>Its Evidence row: the last block's result, "no block", or "none written".</summary>
    public string Evidence { get; init; } = "none written";

    /// <summary>Its objective in the project file, or null for a piece no project file lists.</summary>
    public ProjectObjective? Objective { get; init; }

    /// <summary>
    /// The marks it carries in both lenses, since no line is drawn between boxes (1.1d AC 47): "← handed on
    /// from …", "→ handed on to …", "┈ worked on after …" and "┈ then worked on …".
    /// </summary>
    public IReadOnlyList<string> CrossMarks { get; init; } = [];

    /// <summary>Add to project…, offered only on a piece no project file lists (1.1e AC 6); null otherwise.</summary>
    public PieceActionViewModel? AddToProject { get; init; }

    /// <summary>Its spec as read, when it waits at its gate, for Open spec and Accept spec.</summary>
    public SpecAtGate? Spec { get; init; }

    /// <summary>Its kind, shown by its title bar (1.1f A1, A2).</summary>
    public PieceKind Kind => Piece.Kind;

    /// <summary>The title bar's icon and word, such as "⚒ Build".</summary>
    public string KindLabel => $"{PieceKinds.Icon(Kind)} {PieceKinds.Word(Kind)}";

    /// <summary>The kind word's hover: what the kind means and how it was worked out (A3).</summary>
    public string KindToolTip => $"{PieceKinds.Word(Kind)}: {PieceKinds.Means(Kind)} {PieceKinds.HowWorkedOut}";

    /// <summary>The theme brush its title bar is filled with.</summary>
    public string KindBrushKey => $"Kind{Kind}Brush";

    /// <summary>What the piece achieves, its box's title (A4).</summary>
    public string Title => Piece.Title;

    /// <summary>Who acts next, and when, in the same words as its "Waits for you" item (F31, G37).</summary>
    public string WhoActs => StartedBy ?? Status.WhoActs;

    /// <summary>
    /// "← The lead: fix the search bugs, 08:41": the latest work another card of the project gave its card (git-22,
    /// 2026-11-24); empty with none. Properties has all of it.
    /// </summary>
    public string HandInText => Piece.HandIn is { } handIn ? $"← {handIn.FromTitle}: {handIn.FirstLine}, {When(handIn.AtUtc)}" : string.Empty;

    /// <summary>"08:41" today, else "23 Nov 08:41", in local time.</summary>
    internal static string When(DateTimeOffset atUtc)
    {
        var local = atUtc.ToLocalTime();

        return local.Date == DateTime.Today
            ? local.ToString("HH:mm", System.Globalization.CultureInfo.CurrentCulture)
            : local.ToString("d MMM HH:mm", System.Globalization.CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// Who starts it while its project is running, and why it is not running yet; null where the owner starts it
    /// (project-view-usability item 5).
    /// </summary>
    public string? StartedBy { get; init; }

    /// <summary>The state's three answers, for the state line's hover.</summary>
    public string StatusToolTip => $"{Status.Word}: {Status.Meaning}\nWho acts next: {WhoActs}";

    /// <summary>"serves Objective 1.2", or "not in project.md" (H38).</summary>
    public string ServesText => Piece.ObjectiveNumber is { } number ? $"serves Objective {number}" : "not in project.md";

    /// <summary>The objective's title, for the serves link's hover.</summary>
    public string ServesToolTip => Piece.ObjectiveTitle is { } title ? $"Objective {Piece.ObjectiveNumber}: {title}" : "No project row lists it";

    /// <summary>Its gate diamond, or null when it has none (D21).</summary>
    public GateMarkViewModel? Gate => GateMarkViewModel.For(Piece);

    /// <summary>
    /// The folded strip: its stages with the gate diamond after the Spec stage (after Rule for a bug), so the diamond
    /// sits between Spec and Plan (D21).
    /// </summary>
    public IReadOnlyList<object> StripItems
    {
        get
        {
            var items = new List<object>(Stages);

            if (Gate is { } gate && Stages.ToList().FindIndex(stage => stage.Node is SpecStageNode) is var at and >= 0)
            {
                items.Insert(at + 1, gate);
            }

            return items;
        }
    }

    /// <summary>The folded box's one button: its status's action, when the box offers it (F31).</summary>
    public PieceActionViewModel? PrimaryAction => Status.Action switch
    {
        PieceAction.Focus or PieceAction.Answer => Actions.FirstOrDefault(action => action.Label.Contains("Focus", StringComparison.Ordinal)),
        PieceAction.Start => Actions.FirstOrDefault(action => action.Label.Contains("Start", StringComparison.Ordinal)
                                                              && action.Label != WhoStarts.StartAnywayLabel),
        PieceAction.Accept => Actions.FirstOrDefault(action => action.Label.StartsWith("Accept", StringComparison.Ordinal)),
        PieceAction.OpenRecords => Actions.FirstOrDefault(action => action.Label == "Open records"),
        PieceAction.OpenEvidence => Actions.FirstOrDefault(action => action.Label.StartsWith("Open records", StringComparison.Ordinal)),
        PieceAction.AddToObjective => AddToProject,
        _ => null,
    };

    /// <summary>Whether its folded box shows a button.</summary>
    public bool HasPrimaryAction => PrimaryAction is not null;

    /// <summary>
    /// The proposals its card raised that wait for the owner, drawn as dashed nodes under it with Approve and Reject
    /// (1.1e item 30, placed by 1.1f's "What this replaces"); only on the card's current piece.
    /// </summary>
    public IReadOnlyList<ProposalRowViewModel> Proposals { get; init; } = [];

    /// <summary>Whether it has a proposal waiting.</summary>
    public bool HasProposals => Proposals.Count > 0;

    /// <summary>Whether it is highlighted, as a box of the objective hovered or followed (I44, I45).</summary>
    public bool IsHighlighted
    {
        get => _isHighlighted;
        internal set => Set(ref _isHighlighted, value);
    }

    /// <summary>Follows its "serves Objective n" link (I42, I44); set by the view.</summary>
    public System.Windows.Input.ICommand? ServesCommand { get; internal set; }

    /// <summary>Whether the filter shows it: it matches, or a piece under it does (1.1e item 26).</summary>
    public bool IsShown
    {
        get => _isShown;
        internal set => Set(ref _isShown, value);
    }

    /// <summary>Every box at and under this one, depth first.</summary>
    public IEnumerable<PieceNodeViewModel> SelfAndDescendants => Children.SelectMany(child => child.SelfAndDescendants).Prepend(this);
}
