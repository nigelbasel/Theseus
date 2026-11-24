using System.Globalization;
using System.Windows.Input;
using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// One round of the project view's main area (1.1f C10 to C14): its heading, the change banner before it when the
/// requirements changed, "Set out to meet:" its objectives, and its pieces as a tree, a run of finished ones folded.
/// </summary>
public sealed class RoundViewModel : ObservableObject, IFoldable
{
    private static readonly StageNodeState[] Urgency =
    [
        StageNodeState.Busy, StageNodeState.WaitingOnYou, StageNodeState.Stuck, StageNodeState.HoldoutsNotRun,
        StageNodeState.NotInProject, StageNodeState.ToDo, StageNodeState.HandedOn, StageNodeState.Answered,
    ];

    private bool _isMeetOpen;
    private bool _isOpen;
    private bool _isShown = true;

    /// <summary>
    /// Whether a round is open: its own entry only, else open when it is the latest (C13). Collapse all and Expand all
    /// write the <c>*</c> entry for boxes and headings; a round does not follow it, so the latest round is never hidden by a
    /// Collapse all made before it existed.
    /// </summary>
    internal static bool IsOpenIn(IReadOnlyList<string> folds, int number, bool isLatest) =>
        folds.LastOrDefault(entry => entry.StartsWith($"round:{number}=", StringComparison.Ordinal)) is { } own
            ? own.EndsWith("=open", StringComparison.Ordinal)
            : isLatest;

    internal RoundViewModel(ProjectRound round, ProjectRound? next, bool isLatest, IReadOnlyList<RoundObjectiveViewModel> objectives,
        IReadOnlyList<object> items, IReadOnlyList<PieceNodeViewModel> boxes, bool isOpen, Action<RoundViewModel> toggle)
    {
        Round = round;
        IsLatest = isLatest;
        Objectives = objectives;
        Items = items;
        Boxes = boxes;
        _isOpen = isOpen;
        Dates = $"{Day(round.Began)} – {(isLatest ? "now" : Day(next?.Began))}";
        State = Urgency.FirstOrDefault(state => boxes.Any(box => box.State == state), StageNodeState.Done);
        ToggleCommand = new RelayCommand(_ => toggle(this));
    }

    /// <summary>The round.</summary>
    public ProjectRound Round { get; }

    /// <summary>Whether it is the latest round, which opens; earlier ones open folded (C13).</summary>
    public bool IsLatest { get; }

    /// <summary>"Round 2: GUI parity", or "Round 1" for a file with no Rounds table.</summary>
    public string Title => Round.IsImplicit ? "Round 1" : $"Round {Round.Number}: {Round.Name}";

    /// <summary>"18 Nov – 21 Nov", the last "– now".</summary>
    public string Dates { get; }

    /// <summary>The state its chip shows: the most urgent of its pieces'.</summary>
    public StageNodeState State { get; }

    /// <summary>The chip's mark and word.</summary>
    public string StateText => $"{StageNodeStates.Mark(State)} {StageNodeStates.Word(State)}";

    /// <summary>Whether the filter leaves any of its pieces shown.</summary>
    public bool IsShown
    {
        get => _isShown;
        internal set => Set(ref _isShown, value);
    }

    /// <summary>The one line saying what the round did (C10).</summary>
    public string Line { get; init; } = string.Empty;

    /// <summary>The banner before it, or null for the first round (C11).</summary>
    public string? Banner => Round.Number <= 1 || Round.IsImplicit
        ? null
        : $"⟲ The requirements changed, {Day(Round.Began, withYear: true)}, by {Round.By}. {Round.Why}";

    /// <summary>The banner's second line: the objectives the round added or reopened.</summary>
    public string BannerObjectives => Objectives.Count == 0
        ? string.Empty
        : $"Objectives added or reopened: {string.Join(", ", Objectives.Select(objective => objective.Objective.Number))}.";

    /// <summary>Whether a banner shows.</summary>
    public bool HasBanner => Banner is not null;

    /// <summary>The objectives it set out to meet, as chips (C12).</summary>
    public IReadOnlyList<RoundObjectiveViewModel> Objectives { get; }

    /// <summary>Its top-level items: a box (with its tree under it) or a run of finished boxes folded to one line (D22).</summary>
    public IReadOnlyList<object> Items { get; }

    /// <summary>Every box in it, depth first.</summary>
    public IReadOnlyList<PieceNodeViewModel> Boxes { get; }

    /// <summary>Whether it is open.</summary>
    public bool IsOpen
    {
        get => _isOpen;
        internal set
        {
            if (Set(ref _isOpen, value))
            {
                Raise(nameof(Chevron));
            }
        }
    }

    bool IFoldable.IsOpen
    {
        get => IsOpen;
        set => IsOpen = value;
    }

    /// <inheritdoc />
    public string FoldName => $"round:{Round.Number}";

    /// <summary>The latest round starts open, earlier ones folded (C13).</summary>
    public bool StartsOpen => IsLatest;

    /// <summary>▸ folded, ▾ open.</summary>
    public string Chevron => _isOpen ? "▾" : "▸";

    /// <summary>Folds or opens it.</summary>
    public ICommand ToggleCommand { get; }

    /// <summary>
    /// "Set out to meet" folded to one line, "3 objectives · 11 of 13 criteria met", over the table of them: the free-flowing
    /// list of links and bars was "very busy" (walkthrough row 37). Empty with no objectives.
    /// </summary>
    public string MeetSummary => Objectives.Count == 0
        ? string.Empty
        : $"{Objectives.Count} {(Objectives.Count == 1 ? "objective" : "objectives")} · "
          + $"{Objectives.Sum(each => each.Objective.TickedCount)} of {Objectives.Sum(each => each.Objective.Criteria.Count)} criteria met";

    /// <summary>Whether the table of objectives is shown; folded to <see cref="MeetSummary"/> at first.</summary>
    public bool IsMeetOpen
    {
        get => _isMeetOpen;
        set
        {
            if (Set(ref _isMeetOpen, value))
            {
                Raise(nameof(MeetChevron));
            }
        }
    }

    /// <summary>▸ folded, ▾ open.</summary>
    public string MeetChevron => _isMeetOpen ? "▾" : "▸";

    /// <summary>Folds or opens the table.</summary>
    public ICommand MeetToggleCommand => new RelayCommand(_ => IsMeetOpen = !IsMeetOpen);

    private static string Day(DateOnly? date, bool withYear = false) =>
        date?.ToString(withYear ? "d MMM yyyy" : "d MMM", CultureInfo.InvariantCulture) ?? "?";
}
