using System.Globalization;
using System.Windows.Input;
using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// A phase of project.md as a heading of the view grouped by objectives (1.1d AC 46, 49; Nigel's decision 5,
/// 2026-11-21): "Phase n: name", its exit gate and how many objectives are done, then its objectives. It folds like a
/// branch, and a done phase starts folded. Its menu has Add objective…, adding at the end of this phase.
/// </summary>
public sealed class PhaseHeadingViewModel : ObservableObject, IFoldable
{
    /// <summary>Add objective…'s tooltip, on the heading's menu and in Properties.</summary>
    public const string AddObjectiveToolTip = "Add an objective at the end of this phase in project.md";

    private bool _isOpen;
    private bool _isShown = true;
    private bool _isSelected;

    internal PhaseHeadingViewModel(PhaseGroup group, IReadOnlyList<ObjectiveHeadingViewModel> objectives, bool isOpen,
        Action<IFoldable> toggle, Action<object> select, ProjectViewEdits? edits)
    {
        Group = group;
        Objectives = objectives;
        Rollup = Theseus.Core.Projects.Rollup.Of([.. objectives.Select(objective => objective.Rollup).OfType<Rollup>()]);
        _isOpen = isOpen;
        ToggleCommand = new RelayCommand(_ => toggle(this));
        SelectCommand = new RelayCommand(_ => select(this));
        AddObjective = edits?.AddObjective is { } add
            ? new PieceActionViewModel("Add objective…", () => add(group.Phase.Number), toolTip: AddObjectiveToolTip)
            : null;

        var menu = new List<PieceActionViewModel>
        {
            new("Properties", () => select(this), toolTip: "Show this phase's details", isDefault: true),
        };

        if (AddObjective is { } action)
        {
            menu.Add(action);
        }

        MenuItems = menu;
    }

    /// <summary>The phase and its objectives.</summary>
    public PhaseGroup Group { get; }

    /// <summary>
    /// The criteria's asks of this phase, held here while it has not started, each with Respond… (git-22 for Nigel,
    /// 2026-11-24: "the band shows what blocks the current phase").
    /// </summary>
    public IReadOnlyList<WaitItemViewModel> Held { get; init; } = [];

    /// <summary>"When this phase starts, 12 things wait: 8 for you and 4 on others.", or empty.</summary>
    public string HeldText
    {
        get
        {
            var others = Held.Count(line => line.Item.Who is not null);
            var yours = Held.Count - others;

            return Held.Count == 0
                ? string.Empty
                : $"When this phase starts, {Held.Count} {(Held.Count == 1 ? "thing waits" : "things wait")}: {yours} for you and {others} on others.";
        }
    }

    /// <summary>Whether anything is held here.</summary>
    public bool HasHeld => Held.Count > 0;

    /// <summary>Respond… on the held line: the waits checklist with these ticked.</summary>
    public System.Windows.Input.ICommand? RespondCommand { get; init; }

    /// <summary>Its number, from 0.</summary>
    public int Number => Group.Phase.Number;

    /// <summary>"Phase 1: Evidence".</summary>
    public string Title => $"Phase {Number.ToString(CultureInfo.InvariantCulture)}: {Group.Phase.Name}";

    /// <summary>"Exit gate: …", or empty when the file gives none.</summary>
    public string ExitGate => Group.Phase.ExitGate.Length == 0 ? string.Empty : $"Exit gate: {Group.Phase.ExitGate}";

    /// <summary>Whether it has an exit gate line.</summary>
    public bool HasExitGate => ExitGate.Length > 0;

    /// <summary>"1 of 2 objectives met", or "no objectives yet" (walkthrough row 19: "met", as the objectives say).</summary>
    public string DoneText => Group.Objectives.Count == 0
        ? "no objectives yet"
        : $"{Group.ObjectivesDone} of {Group.Objectives.Count} objectives met";

    /// <summary>Whether every objective of it is done.</summary>
    public bool IsDone => Group.IsDone;

    /// <summary>Its objectives, in file order.</summary>
    public IReadOnlyList<ObjectiveHeadingViewModel> Objectives { get; }

    /// <summary>Its objectives rolled up the same way, shown even when folded (Nigel, 2026-11-22).</summary>
    public Rollup Rollup { get; }

    /// <summary>"● In progress · Theseus 1.1e", for its heading.</summary>
    public string RollupText => Rollup.Text + (Rollup.Who.Length > 0 ? $" · {Rollup.Who}" : string.Empty);

    /// <summary>Add objective…, or null when the view cannot edit.</summary>
    public PieceActionViewModel? AddObjective { get; }

    /// <summary>Its right-click menu: Properties (the default), then Add objective….</summary>
    public IReadOnlyList<PieceActionViewModel> MenuItems { get; }

    /// <summary>Its right-click menu as drawn: each item with the access key the menu gave it (W26, C9).</summary>
    public IReadOnlyList<MenuEntryViewModel> MenuEntries => MenuAccessKeys.For(MenuItems);

    /// <inheritdoc />
    public string FoldName => PieceFolds.PhaseEntry(Group.Phase.Name);

    /// <summary>A phase starts open unless it is done (AC 46).</summary>
    public bool StartsOpen => !IsDone;

    /// <summary>Whether its objectives show; open to start unless the phase is done.</summary>
    public bool IsOpen
    {
        get => _isOpen;
        internal set
        {
            if (Set(ref _isOpen, value))
            {
                Raise(nameof(Chevron));
                Raise(nameof(ChevronToolTip));
            }
        }
    }

    bool IFoldable.IsOpen
    {
        get => IsOpen;
        set => IsOpen = value;
    }

    /// <summary>The fold chevron, in the icon font (W25).</summary>
    public string Chevron => FoldGlyph.Of(_isOpen);

    /// <summary>What the chevron does.</summary>
    public string ChevronToolTip => _isOpen ? "Fold this phase" : "Show this phase's objectives";

    /// <summary>
    /// Whether it is shown: always without a filter; with one, only while it holds a piece the filter shows, as a round
    /// does in Rounds and work (Nigel, 2026-11-24: a chip changed nothing in Objectives only).
    /// </summary>
    public bool IsShown
    {
        get => _isShown;
        internal set => Set(ref _isShown, value);
    }

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
