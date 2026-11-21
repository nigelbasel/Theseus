using System.Windows.Input;
using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// One box of the project view's tree (1.1d AC 24 to 28): a piece of work, folded to a strip of its stages or open into
/// its chain and actions, with the pieces it hands work to in a branch under it. What it shows comes from the model
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

    /// <summary>Whether it has a branch under it.</summary>
    public bool HasChildren => Children.Count > 0;

    /// <summary>Whether it hands work to more than one piece at once.</summary>
    public bool IsParallel => Node.IsParallel;

    /// <summary>
    /// How far in its branch starts: further when parallel, so the brace and its label sit left of the hand-over line
    /// (1.1e AC 6d).
    /// </summary>
    public System.Windows.GridLength BranchIndent =>
        new(IsParallel ? Views.ConnectorGeometry.ParallelBranchIndent : Views.ConnectorGeometry.BranchIndent);

    /// <summary>How far in from its left edge its hand-overs drop: right of the parallel brace when it has one (1.1e AC 6d).</summary>
    public double HandOverInset => IsParallel ? Views.ConnectorGeometry.ParallelHandOverInset : Views.ConnectorGeometry.HandOverInset;

    /// <summary>"7 in parallel", written beside the brace.</summary>
    public string ParallelText => $"{Node.ChildCount} in parallel";

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
    /// The marks it carries in the view grouped by objectives, where no connector is drawn (1.1d AC 47): "← handed on
    /// from …", "→ handed on to …", "┈ worked on after …" and "┈ then worked on …".
    /// </summary>
    public IReadOnlyList<string> CrossMarks { get; init; } = [];

    /// <summary>Add to project…, offered only on a piece no project file lists (1.1e AC 6); null otherwise.</summary>
    public PieceActionViewModel? AddToProject { get; init; }

    /// <summary>Its spec as read, when it waits at its gate, for Open spec and Accept spec.</summary>
    public SpecAtGate? Spec { get; init; }

    /// <summary>Every box at and under this one, depth first.</summary>
    public IEnumerable<PieceNodeViewModel> SelfAndDescendants => Children.SelectMany(child => child.SelfAndDescendants).Prepend(this);
}
