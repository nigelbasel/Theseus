using System.Windows.Input;
using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// An objective as a heading of the view grouped by objectives (1.1d AC 46, 47, 49; Nigel's decision 5, 2026-11-21): its
/// number and title, how many criteria are ticked, the state counts of its pieces of work, and those pieces as the same
/// boxes the Flow shows. It folds like a branch; selecting it puts the objective, its criteria and Edit objective… in
/// Properties. The pieces no project file lists have a heading of their own, with no objective.
/// </summary>
public sealed class ObjectiveHeadingViewModel : ObservableObject, IFoldable
{
    /// <summary>The heading of the pieces no project file lists.</summary>
    public const string UnlistedTitle = "Not in project.md";

    /// <summary>Edit objective…'s tooltip, on the heading's menu and in Properties.</summary>
    public const string EditObjectiveToolTip = "Change this objective's title in project.md";

    private bool _isOpen;
    private bool _isSelected;

    internal ObjectiveHeadingViewModel(ObjectiveGroup group, string? phaseTitle, IReadOnlyList<PieceNodeViewModel> pieces,
        bool isOpen, Action<IFoldable> toggle, Action<object> select, ProjectViewEdits? edits)
    {
        Objective = group.Objective;
        PhaseTitle = phaseTitle;
        Pieces = pieces;
        Summary = ProjectSummary.For(group.Pieces);
        Rollup = Objective is { } rolled ? Theseus.Core.Projects.Rollup.Of(rolled, group.Pieces) : null;
        _isOpen = isOpen;
        ToggleCommand = new RelayCommand(_ => toggle(this));
        SelectCommand = new RelayCommand(_ => select(this));
        Criteria = Objective is { } objective ? CriterionViewModel.Of(objective, edits) : [];
        EditObjectiveCommand = Objective is { } edited && edits is not null
            ? new RelayCommand(_ => edits.EditObjective(edited))
            : null;

        var menu = new List<PieceActionViewModel>
        {
            new("Properties", () => select(this), toolTip: "Show this objective's details", isDefault: true),
        };

        if (EditObjectiveCommand is { } edit)
        {
            menu.Add(new PieceActionViewModel("Edit objective…", () => edit.Execute(null), toolTip: EditObjectiveToolTip));
        }

        MenuItems = menu;
    }

    /// <summary>The objective, or null for the pieces no project file lists.</summary>
    public ProjectObjective? Objective { get; }

    /// <summary>Its number, such as "1.1", or null.</summary>
    public string? Number => Objective?.Number;

    /// <summary>"1.1 Checks shown on cards", or "Not in project.md".</summary>
    public string Title => Objective is { } objective ? $"{objective.Number} {objective.Title}" : UnlistedTitle;

    /// <summary>"Phase 1: Evidence", the phase it is under, or null.</summary>
    public string? PhaseTitle { get; }

    /// <summary>"0/6": ticked of criteria; empty with no objective.</summary>
    public string CriteriaText => Objective is { } objective ? $"{objective.TickedCount}/{objective.Criteria.Count}" : string.Empty;

    /// <summary>"0 of 6 criteria ticked", or what the heading of unlisted pieces holds.</summary>
    public string CriteriaToolTip => Objective is { } objective
        ? $"{objective.TickedCount} of {objective.Criteria.Count} criteria ticked"
        : "Linked into the project by a hand-over, but listed in no project file";

    /// <summary>Its pieces of work, counted as the summary counts them.</summary>
    public ProjectSummary Summary { get; }

    /// <summary>The summary's chips for its pieces, zeros left out (AC 46).</summary>
    public IReadOnlyList<StateCountViewModel> Counts => StateCountViewModel.For(Summary, zeros: false);

    /// <summary>Its pieces of work, in file order, as the Flow's boxes.</summary>
    public IReadOnlyList<PieceNodeViewModel> Pieces { get; }

    /// <summary>Its rolled-up mark, shown even when folded (Nigel, 2026-11-22); null for the heading of unlisted pieces.</summary>
    public Rollup? Rollup { get; }

    /// <summary>"◆ Waiting for you · Theseus 1.1g", or empty.</summary>
    public string RollupText => Rollup is { } rollup ? rollup.Text + (rollup.Who.Length > 0 ? $" · {rollup.Who}" : string.Empty) : string.Empty;

    /// <summary>Whether nothing works on it yet, when it says "nothing yet".</summary>
    public bool IsEmpty => Pieces.Count == 0;

    /// <summary>Whether its branch of boxes shows: open, with pieces.</summary>
    public bool ShowsPieces => _isOpen && Pieces.Count > 0;

    /// <summary>Its criteria, ticked in the project file, for Properties.</summary>
    public IReadOnlyList<CriterionViewModel> Criteria { get; }

    /// <summary>Edit objective…, or null when the view cannot edit or there is no objective.</summary>
    public ICommand? EditObjectiveCommand { get; }

    /// <summary>Its right-click menu: Properties (the default), then Edit objective….</summary>
    public IReadOnlyList<PieceActionViewModel> MenuItems { get; }

    /// <inheritdoc />
    public string FoldName => Objective is { } objective ? PieceFolds.ObjectiveEntry(objective.Title) : PieceFolds.UnlistedEntry;

    /// <summary>Objective headings start open.</summary>
    public bool StartsOpen => true;

    /// <summary>Whether its pieces show; open to start.</summary>
    public bool IsOpen
    {
        get => _isOpen;
        internal set
        {
            if (Set(ref _isOpen, value))
            {
                Raise(nameof(Chevron));
                Raise(nameof(ChevronToolTip));
                Raise(nameof(ShowsPieces));
            }
        }
    }

    bool IFoldable.IsOpen
    {
        get => IsOpen;
        set => IsOpen = value;
    }

    /// <summary>▸ folded, ▾ open.</summary>
    public string Chevron => _isOpen ? "▾" : "▸";

    /// <summary>What the chevron does.</summary>
    public string ChevronToolTip => _isOpen ? "Fold this objective" : "Show this objective's pieces of work";

    /// <summary>Whether it is what the Properties pane shows.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        internal set => Set(ref _isSelected, value);
    }

    /// <summary>Folds or opens it, and remembers which.</summary>
    public ICommand ToggleCommand { get; }

    /// <summary>Selects it, to show it in Properties.</summary>
    public ICommand SelectCommand { get; }
}
