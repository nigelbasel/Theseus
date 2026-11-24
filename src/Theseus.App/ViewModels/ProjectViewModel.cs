using System.Globalization;
using System.Windows.Input;
using Theseus.Core.Links;
using Theseus.Core.Projects;
using Theseus.Core.Settings;

namespace Theseus.App.ViewModels;

/// <summary>
/// The whole-screen view of one project (1.1d AC 15 to 37; Nigel accepted the mockup, 2026-11-20): where the project is
/// in one sentence and six counts, then its pieces of work by round under a heading for each state, or under their
/// phases and objectives (AC 45 to 51), beside a Properties pane that steps aside in a narrow window (AC 52 to 54).
/// Every state it shows comes from the model in <c>Theseus.Core.Projects</c>; the view model only lays it out,
/// remembers what is folded and what is selected, and runs the shell's commands.
/// </summary>
public sealed class ProjectViewModel : ObservableObject
{
    /// <summary>Accept spec's tooltip, on the box and in Properties.</summary>
    internal const string AcceptSpecToolTip = "Pass this gate: asks first, naming the piece of work and the commit";

    /// <summary>What the status bar says once when the window becomes too narrow for the pane beside the flow (AC 52).</summary>
    public const string PaneHiddenNotice = "Properties hidden: the window is too narrow";

    private static readonly StageAction[] BoxActionOrder =
    [
        StageAction.OpenSpec, StageAction.AcceptSpec, StageAction.VerifyWithHoldouts, StageAction.Focus, StageAction.Start,
        StageAction.Stop, StageAction.OpenRecords,
    ];

    private static readonly StageNodeState[] NeedsAttention =
        [StageNodeState.Busy, StageNodeState.WaitingOnYou, StageNodeState.Stuck, StageNodeState.HoldoutsNotRun];

    private readonly ISettingsStore _settings;
    private readonly ProjectViewActions _actions;
    private readonly ProjectViewEdits? _edits;
    private HashSet<PieceNodeViewModel> _searchHits = [];
    private string? _highlightedObjective;
    private IReadOnlyList<string> _folds;
    private object? _selection;
    private PropertiesViewModel _properties = PropertiesViewModel.Empty;
    private ProjectViewGrouping _grouping;
    private bool _isPaneDocked = true;
    private bool _isOverlayOpen;
    private IReadOnlyList<ThenWorkedOnLink> _thenWorkedOn = [];
    private HashSet<StageNodeState> _filter = [];
    private IReadOnlySet<string> _waitingPieces = new HashSet<string>();
    private int _waitsWithNoBox;
    private string? _leadName;
    private string? _leadKey;

    private ProjectViewModel(ProjectFile file, ProjectSummary summary, ISettingsStore settings, ProjectViewActions actions,
        ProjectViewEdits? edits)
    {
        Name = file.Name;
        Title = file.Title;
        Summary = summary;
        _settings = settings;
        _actions = actions;
        _edits = edits;

        var stored = settings.Read();
        _folds = stored.ViewFolds.GetValueOrDefault(PieceFolds.KeyFor(file.Name)) ?? [];
        PaneWidth = stored.PaneSizes.TryGetValue(ProjectPaneWidth.SettingsKey, out var width) ? width : ProjectPaneWidth.Default;
        // The objectives lens is the main view; a remembered choice for the project still wins (Nigel, 2026-11-22).
        _grouping = stored.ProjectGrouping.GetValueOrDefault(file.Name, ProjectViewGrouping.Objectives);

        CollapseAllCommand = new RelayCommand(_ => SetAll(false));
        ExpandAllCommand = new RelayCommand(_ => SetAll(true));
        OpenWhatNeedsAttentionCommand = new RelayCommand(_ => OpenWhatNeedsAttention());
        ClearSelectionCommand = new RelayCommand(_ => Escape(), _ => _selection is not null || _filter.Count > 0);
        CloseOverlayCommand = new RelayCommand(_ => SetOverlay(false), _ => _isOverlayOpen);
        ShowAllWaitsCommand = new RelayCommand(_ => Respond([]), _ => HasMoreWaits);
        RespondToOthersCommand = new RelayCommand(_ => Respond(Overview.Others), _ => Overview.Others.Count > 0);
    }

    /// <summary>The project's name.</summary>
    public string Name { get; }

    /// <summary>Its title.</summary>
    public string Title { get; }

    /// <summary>Where the project is (AC 8): the only source of its counts and sentence.</summary>
    public ProjectSummary Summary { get; }

    /// <summary>The summary's sentence.</summary>
    public string Sentence => Overview.Sentence;

    /// <summary>The quiet line the last clear-up leaves under the heading while it can be undone, or null (Nigel, 2026-11-23).</summary>
    public string? ClearUpNotice => Overview.ClearUpNotice;

    /// <summary>Undo on the clear-up's notice.</summary>
    public ICommand? UndoClearUpCommand => _actions.UndoClearVerifiers is { } undo ? new RelayCommand(_ => undo(Name)) : null;

    /// <summary>✕ Dismiss on the clear-up's notice.</summary>
    public ICommand? DismissClearUpCommand => _actions.DismissClearUp is { } dismiss ? new RelayCommand(_ => dismiss(Name)) : null;

    /// <summary>The summary, phase strip, tallies and "Waits for you" (1.1f G33 to G37b).</summary>
    public ProjectOverview Overview { get; private set; } = null!;

    /// <summary>"Lead: Theseus · idle", the project's lead card and its state (Nigel, 2026-11-22); empty with no rows.</summary>
    public string LeadText { get; private set; } = string.Empty;

    /// <summary>Brings the lead's terminal forward; disabled with its reason when it has no session.</summary>
    public System.Windows.Input.ICommand? FocusLeadCommand { get; private set; }

    /// <summary>What Focus does, or why it cannot.</summary>
    public string FocusLeadToolTip { get; private set; } = string.Empty;

    /// <summary>The project toolbar's Focus lead icon's tooltip: what it does, with its key (G78).</summary>
    public string FocusLeadIconToolTip => "Focus lead session (Ctrl+L): "
        + (FocusLeadToolTip.Length == 0 ? "this project has no lead card" : char.ToLowerInvariant(FocusLeadToolTip[0]) + FocusLeadToolTip[1..]);

    /// <summary>Whether the header shows a lead.</summary>
    public bool HasLead => LeadText.Length > 0;

    /// <summary>◀ Back (I46); null in tests without a shell.</summary>
    public System.Windows.Input.ICommand? GoBackCommand => _actions.GoBack;

    /// <summary>Forward ▶ (I46).</summary>
    public System.Windows.Input.ICommand? GoForwardCommand => _actions.GoForward;

    /// <summary>
    /// Follows a link to an objective (I44): opens the rounds that hold its pieces, highlights its boxes and its chips, and
    /// selects its first box; with Objectives only on, selects its heading. A jump, so Back returns to where the owner was.
    /// </summary>
    public void GoToObjective(string number)
    {
        HighlightObjective(number);

        if (IsGroupedByObjectives && AllObjectives.FirstOrDefault(objective => objective.Objective?.Number == number) is { } heading)
        {
            // A filter that hides the heading gives way, or the selection would be nowhere on screen.
            if (!heading.IsShown)
            {
                _filter = [];
                ApplyFilter();
            }

            Select(heading);
        }
        else if (AllNodes.FirstOrDefault(node => node.Piece.ObjectiveNumber == number) is { } first)
        {
            Reveal(first);
            Select(first);
        }

        _actions.Jumped?.Invoke($"objective:{number}");
    }

    /// <summary>
    /// Where a token in a sentence leads (I42): "Objective 1.5" to that objective, "W4" to the first piece whose workstream
    /// carries that code; null when nothing in the view matches, so the token stays plain text.
    /// </summary>
    public (Action Follow, string ToolTip)? LinkTarget(string token)
    {
        ArgumentNullException.ThrowIfNull(token);

        if (token.StartsWith("Objective ", StringComparison.OrdinalIgnoreCase))
        {
            var number = token[(token.IndexOf(' ', StringComparison.Ordinal) + 1)..];

            return AllObjectives.Any(heading => heading.Objective?.Number == number)
                ? (() => GoToObjective(number), $"Go to objective {number}")
                : null;
        }

        var code = new System.Text.RegularExpressions.Regex($@"\b{System.Text.RegularExpressions.Regex.Escape(token)}\b");

        return AllNodes.Where(node => code.IsMatch(node.Workstream)).OrderByDescending(node => node.Piece.IsCurrentPiece).FirstOrDefault() is { } piece
            ? (() => GoToPiece(piece.Name), $"Go to {piece.Title}")
            : null;
    }

    /// <summary>Follows a link to a piece (I44): opens its round and its state's heading, selects it, and records the jump.</summary>
    public void GoToPiece(string name)
    {
        if (AllNodes.FirstOrDefault(node => node.Name == name) is { } node)
        {
            Reveal(node);
            Select(node);
            _actions.Jumped?.Invoke(name);
        }
    }

    /// <summary>Puts the view back at a place from the history: its lens and what was selected (I46). Not a jump.</summary>
    public void GoTo(string? lens, string? selection)
    {
        if (lens is not null && Enum.TryParse<ProjectViewGrouping>(lens, out var grouping))
        {
            if (grouping == ProjectViewGrouping.Objectives)
            {
                IsGroupedByObjectives = true;
            }
            else
            {
                IsGroupedByFlow = true;
            }
        }

        if (selection is null)
        {
            Select(null);
            HighlightObjective(null);
        }
        else if (selection.StartsWith("objective:", StringComparison.Ordinal))
        {
            var number = selection["objective:".Length..];
            HighlightObjective(number);

            if (IsGroupedByObjectives && AllObjectives.FirstOrDefault(objective => objective.Objective?.Number == number) is { } heading)
            {
                Select(heading);
            }
            else
            {
                Select(AllNodes.FirstOrDefault(node => node.Piece.ObjectiveNumber == number));
            }
        }
        else if (AllNodes.FirstOrDefault(node => node.Name == selection) is { } node)
        {
            Reveal(node);
            Select(node);
        }
    }

    /// <summary>
    /// Highlights the boxes the board's search matches (card-search-and-tags item 7), with the highlight a followed
    /// objective uses, and keeps them highlighted while objectives are hovered. Null clears it.
    /// </summary>
    public void ShowSearch(Func<PieceNodeViewModel, bool>? matches)
    {
        _searchHits = matches is null ? [] : [.. AllNodes.Where(matches)];
        HighlightObjective(_highlightedObjective);
    }

    /// <summary>
    /// Opens what hides the search's matching boxes, as following a link does: their round, state heading, objective and phase.
    /// Run when a project is opened with a search on, so the highlight is seen and not folded away (item 7).
    /// </summary>
    public void RevealSearch()
    {
        foreach (var node in _searchHits)
        {
            Reveal(node);

            foreach (var phase in Phases)
            {
                foreach (var heading in phase.Objectives.Where(heading => heading.Pieces.Contains(node)))
                {
                    if (!phase.IsOpen)
                    {
                        ToggleFold(phase);
                    }

                    if (!heading.IsOpen)
                    {
                        ToggleFold(heading);
                    }
                }
            }

            if (AllObjectives.FirstOrDefault(heading => heading.Pieces.Contains(node)) is { IsOpen: false } loose)
            {
                ToggleFold(loose);
            }
        }
    }

    /// <summary>The boxes the board's search matches, highlighted while it is on (item 7).</summary>
    public IReadOnlyCollection<PieceNodeViewModel> SearchHits => _searchHits;

    /// <summary>Hovering a box highlights its objective's chip; hovering a chip highlights its boxes (I45). Null clears.</summary>
    public void HighlightObjective(string? number)
    {
        _highlightedObjective = number;

        foreach (var node in AllNodes)
        {
            node.IsHighlighted = (number is not null && node.Piece.ObjectiveNumber == number) || _searchHits.Contains(node);
        }

        foreach (var chip in Rounds.SelectMany(round => round.Objectives))
        {
            chip.IsHighlighted = number is not null && chip.Objective.Number == number;
        }

        foreach (var round in Rounds)
        {
            round.RaiseHighlight();
        }
    }

    // Opens what hides a box: its round, the heading of its state, and its phase and objective.
    private void Reveal(PieceNodeViewModel node)
    {
        if (Rounds.FirstOrDefault(round => round.Boxes.Contains(node)) is { IsOpen: false } round)
        {
            ToggleFold(round);
        }

        if (StateGroups.FirstOrDefault(group => group.Boxes.Contains(node)) is { IsOpen: false } heading)
        {
            ToggleFold(heading);
        }

        foreach (var phase in Phases.Where(phase => phase.Objectives.Any(objective => objective.Pieces.Contains(node))))
        {
            if (!phase.IsOpen)
            {
                ToggleFold(phase);
            }

            foreach (var objective in phase.Objectives.Where(objective => objective.Pieces.Contains(node) && !objective.IsOpen))
            {
                ToggleFold(objective);
            }
        }

        foreach (var objective in Unlisted.Where(objective => objective.Pieces.Contains(node) && !objective.IsOpen))
        {
            ToggleFold(objective);
        }
    }

    /// <summary>"◆ Waits for you (4)" (G36).</summary>
    public string WaitsHeading => $"◆ Waits for you ({Waits.Count})";

    /// <summary>Everything that waits for the owner, each with its button (G36, G37a).</summary>
    public IReadOnlyList<WaitItemViewModel> Waits { get; private set; } = [];

    /// <summary>Whether anything waits; the block reads "Nothing waits for you." when not, still in place (G36).</summary>
    public bool HasWaits => Waits.Count > 0;

    /// <summary>
    /// How many lines the band shows before "and n more": the band sits above the page, so 68 lines pushed the work off
    /// the window and nothing scrolled (project-view-scale, Nigel 2026-11-24).
    /// </summary>
    public const int ShownWaitsLimit = 3;

    /// <summary>The lines the band shows: the first <see cref="ShownWaitsLimit"/>.</summary>
    public IReadOnlyList<WaitItemViewModel> ShownWaits => [.. Waits.Take(ShownWaitsLimit)];

    /// <summary>"and 44 more…" under the band's lines, or empty when it shows them all.</summary>
    public string MoreWaitsText => HasMoreWaits ? $"and {Waits.Count - ShownWaitsLimit} more…" : string.Empty;

    /// <summary>What "and n more…" is called to a screen reader: "Show all 47 lines of Waits for you".</summary>
    public string MoreWaitsName => $"Show all {Waits.Count} lines of Waits for you";

    /// <summary>Whether the band has more lines than it shows.</summary>
    public bool HasMoreWaits => Waits.Count > ShownWaitsLimit;

    /// <summary>"and n more…": every line in a dialog of its own, since lists of items go in dialogs (Nigel's rule).</summary>
    public ICommand ShowAllWaitsCommand { get; }

    /// <summary>Raised with the waits checklist to open; the view opens the dialog.</summary>
    public event EventHandler<WaitsChecklistViewModel>? AllWaitsRequested;

    /// <summary>The criteria's asks of someone other than the owner, each with Respond… (git-22 for Nigel, 2026-11-24).</summary>
    public IReadOnlyList<WaitItemViewModel> OtherWaits { get; private set; } = [];

    /// <summary>"Waits on others: 4, on the gateway team and the DPO.", or empty: said, not counted.</summary>
    public string OthersText => Overview.Others.Count == 0
        ? string.Empty
        : $"Waits on others: {Overview.Others.Count}, on {Names([.. Overview.Others.Select(wait => wait.Who!).Distinct(StringComparer.OrdinalIgnoreCase)])}.";

    /// <summary>Respond… on the others' line: the checklist with theirs ticked.</summary>
    public ICommand RespondToOthersCommand { get; }

    /// <summary>
    /// The waits that concern no box, shown at the top of the work while the "waiting on you" chip filters, so the chip's
    /// count is what the filter shows (git-22 for Nigel, 2026-11-24: the chip "filters to nothing in both lenses").
    /// </summary>
    public IReadOnlyList<WaitItemViewModel> NoPieceWaits => _filter.Contains(StageNodeState.WaitingOnYou)
        ? [.. Waits.Where(wait => !AllNodes.Any(node => Concerns(wait.Item, node)))]
        : [];

    /// <summary>Whether the group of waits with no piece of work shows.</summary>
    public bool HasNoPieceWaits => NoPieceWaits.Count > 0;

    /// <summary>The waits checklist with every wait, these ticked: what every Respond… opens.</summary>
    public WaitsChecklistViewModel Checklist(IEnumerable<WaitItem> ticked)
    {
        var rows = Waits.Select(line => (line, "For you"))
            .Concat(OtherWaits.Select(line => (line, $"On {line.Item.Who}")))
            .Concat(Phases.SelectMany(phase => phase.Held.Select(line =>
                (line, $"{(line.Item.Who is { } who ? $"On {who}" : "For you")}, when phase {phase.Group.Phase.Number} starts"))))
            .ToList();
        Action<string>? askLead = _leadKey is { } lead && _actions.TellCard is { } tell
            ? text =>
            {
                tell(lead, text);
                _actions.Focus(lead);
            }
            : null;

        return new WaitsChecklistViewModel(rows, ticked,
            new WaitsChecklistActions(Title, _edits?.EditFile, _edits?.AskText, _leadName, askLead, number =>
            {
                IsGroupedByObjectives = true;
                GoToObjective(number);
            }));
    }

    /// <summary>Opens the waits checklist with these ticked.</summary>
    public void Respond(IEnumerable<WaitItem> ticked) => AllWaitsRequested?.Invoke(this, Checklist(ticked));

    private static string Names(IReadOnlyList<string> names) =>
        names.Count <= 2 ? string.Join(" and ", names) : $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}";

    /// <summary>
    /// "Waits on the lead, Spec lead: 35 objectives have no workstream yet.", or empty: the lead plans an objective's
    /// work, so it is said under the band and not counted in it (project-view-scale, 2026-11-24).
    /// </summary>
    public string LeadWaitsText => Overview.ForTheLead.Count == 0
        ? string.Empty
        : $"Waits on the lead{(_leadName is null ? string.Empty : $", {_leadName}")}: {string.Join(" ", Overview.ForTheLead)}";

    /// <summary>"Theseus is doing this itself: Checking one build…", or empty: under the band, since it asks nothing.</summary>
    public string UnderwayText => Overview.Underway.Count == 0
        ? string.Empty
        : $"Theseus is doing this itself: {string.Join(" ", Overview.Underway)}";

    /// <summary>The phase strip (G34).</summary>
    public IReadOnlyList<PhaseCell> PhaseStrip => Overview.Phases;

    /// <summary>The value of the process, as tallies (G35).</summary>
    public IReadOnlyList<string> Tallies => Overview.Tallies;

    /// <summary>
    /// The states shown while a filter is on, from the legend row's chips (1.1e item 26, moved there by 1.1f D15); empty
    /// shows everything.
    /// </summary>
    public IReadOnlySet<StageNodeState> Filter => _filter;

    /// <summary>"Showing: busy, stuck (3 of 13)" while a filter is on; empty otherwise.</summary>
    public string FilterText => _filter.Count == 0
        ? string.Empty
        : $"Showing: {string.Join(", ", _filter.Select(state => StageNodeStates.Word(state).ToLowerInvariant()))} "
          + $"({AllNodes.Count(InFilter)} of {AllNodes.Count()})"
          + (_filter.Contains(StageNodeState.WaitingOnYou) && _waitsWithNoBox > 0
              ? $"; {_waitsWithNoBox} more {(_waitsWithNoBox == 1 ? "has" : "have")} no piece of work and {(_waitsWithNoBox == 1 ? "is" : "are")} listed at the top"
              : string.Empty);

    /// <summary>Whether a filter is on.</summary>
    public bool IsFiltered => _filter.Count > 0;

    /// <summary>The six count chips, zeros included (AC 19).</summary>
    public IReadOnlyList<StateCountViewModel> Counts { get; private set; } = [];

    /// <summary>
    /// The project's own colour, for the summary's mark (1.1d AC 40): its tab's, or null before it has one, when the mark
    /// takes the accent colour.
    /// </summary>
    public string? ColourHex => Tabs.FirstOrDefault(tab => string.Equals(tab.Name, Name, StringComparison.OrdinalIgnoreCase))?.ColourHex;

    /// <summary>The legend's states, in the order AC 22 gives.</summary>
    public static IReadOnlyList<LegendItemViewModel> Legend { get; } =
    [
        new(StageNodeState.Done), new(StageNodeState.WaitingOnYou), new(StageNodeState.Busy), new(StageNodeState.Stuck),
        new(StageNodeState.HoldoutsNotRun), new(StageNodeState.ToDo),
    ];

    /// <summary>The project's rounds, the main area's top level (1.1f C10).</summary>
    public IReadOnlyList<RoundViewModel> Rounds { get; private set; } = [];

    /// <summary>The rounds while the view shows rounds and work; none in the objectives lens.</summary>
    public IReadOnlyList<RoundViewModel> ShownRounds => IsGroupedByFlow ? Rounds : [];

    /// <summary>Every round's headings of its states (git-22, 2026-11-24).</summary>
    public IEnumerable<RoundStateGroupViewModel> StateGroups => Rounds.SelectMany(round => round.Items);

    /// <summary>The tree's roots, in file order (AC 23).</summary>
    public IReadOnlyList<PieceNodeViewModel> Roots { get; private set; } = [];

    /// <summary>Every box, depth first.</summary>
    public IEnumerable<PieceNodeViewModel> AllNodes => Roots.SelectMany(root => root.SelfAndDescendants);


    /// <summary>One tab per project that reads (AC 17).</summary>
    public IReadOnlyList<ProjectTabViewModel> Tabs { get; private set; } = [];

    /// <summary>
    /// The tab strip's selection (W41; B7, D6): the open project's tab. Selecting another opens its project, which builds
    /// a new view whose open tab is that one, so the selection follows the tab keys too.
    /// </summary>
    public ProjectTabViewModel? OpenTab
    {
        get => Tabs.FirstOrDefault(tab => tab.IsOpen);
        set
        {
            if (value is { IsOpen: false, OpenCommand: { } open } && open.CanExecute(null))
            {
                open.Execute(null);
            }
        }
    }

    /// <summary>The specs waiting on the owner, for the menu bar's Open spec and Accept spec (R16).</summary>
    public IReadOnlyList<SpecAtGate> WaitingSpecs { get; private set; } = [];

    /// <summary>How the boxes are grouped: the Flow of hand-overs, or under phases and objectives (AC 45).</summary>
    public ProjectViewGrouping Grouping => _grouping;

    /// <summary>Group by: Flow, on the toolbar and the View menu; unticking the chosen grouping keeps it.</summary>
    public bool IsGroupedByFlow
    {
        get => _grouping == ProjectViewGrouping.Flow;
        set => Choose(value, ProjectViewGrouping.Flow);
    }

    /// <summary>Group by: Objectives.</summary>
    public bool IsGroupedByObjectives
    {
        get => _grouping == ProjectViewGrouping.Objectives;
        set => Choose(value, ProjectViewGrouping.Objectives);
    }

    /// <summary>The roots the Flow draws: none while grouped by objectives, so no box is laid out twice.</summary>
    public IReadOnlyList<PieceNodeViewModel> FlowRoots => IsGroupedByFlow ? Roots : [];

    /// <summary>Every phase of project.md as a heading, in file order, each with its objectives (AC 46).</summary>
    public IReadOnlyList<PhaseHeadingViewModel> Phases { get; private set; } = [];

    /// <summary>The heading of the pieces no project file lists, after the phases: one, or none.</summary>
    public IReadOnlyList<ObjectiveHeadingViewModel> Unlisted { get; private set; } = [];

    /// <summary>The phases shown: none while grouped by the Flow.</summary>
    public IReadOnlyList<PhaseHeadingViewModel> ShownPhases => IsGroupedByObjectives ? Phases : [];

    /// <summary>The heading of unlisted pieces shown: none while grouped by the Flow.</summary>
    public IReadOnlyList<ObjectiveHeadingViewModel> ShownUnlisted => IsGroupedByObjectives ? Unlisted : [];

    /// <summary>Every objective heading, phase by phase, then the heading of unlisted pieces.</summary>
    public IEnumerable<ObjectiveHeadingViewModel> AllObjectives => Phases.SelectMany(phase => phase.Objectives).Concat(Unlisted);

    /// <summary>
    /// Whether the Properties pane is docked beside the flow; not while the view is narrower than both their minimums
    /// and the splitter (AC 52).
    /// </summary>
    public bool IsPaneDocked => _isPaneDocked;

    /// <summary>Whether Properties shows as an overlay on the right, over the flow, while the pane is not docked (AC 53).</summary>
    public bool IsOverlayOpen => _isOverlayOpen;

    /// <summary>The overlay's ×: closes it and leaves the selection.</summary>
    public ICommand CloseOverlayCommand { get; }

    /// <summary>
    /// What is selected: a <see cref="PieceNodeViewModel"/>, a <see cref="StageNodeViewModel"/>, a
    /// <see cref="PhaseHeadingViewModel"/>, an <see cref="ObjectiveHeadingViewModel"/>, or null.
    /// </summary>
    public object? Selection => _selection;

    /// <summary>What the Properties pane shows.</summary>
    public PropertiesViewModel Properties
    {
        get => _properties;
        private set => Set(ref _properties, value);
    }

    /// <summary>The Properties pane's width as last left (AC 35).</summary>
    public double PaneWidth { get; private set; }

    /// <summary>Folds every box (AC 21).</summary>
    public ICommand CollapseAllCommand { get; }

    /// <summary>Opens every box.</summary>
    public ICommand ExpandAllCommand { get; }

    /// <summary>Opens the boxes that are busy, waiting on you, stuck or waiting for their holdouts, and folds the rest.</summary>
    public ICommand OpenWhatNeedsAttentionCommand { get; }

    /// <summary>Esc: closes the Properties overlay when it is open, leaving the selection (AC 53); otherwise nothing selected (AC 16, 32).</summary>
    public ICommand ClearSelectionCommand { get; }

    /// <summary>Back to the board: ← Board, Alt+Left and View › Back to the board (AC 16).</summary>
    public ICommand? BackCommand { get; private set; }

    /// <summary>
    /// What the view draws, as text: a refresh rebuilds the view only when it changes (AC 36), so it covers every stage
    /// state and reason, status, mark, action, link, criterion and live session the view shows.
    /// </summary>
    public string Signature { get; private set; } = string.Empty;

    /// <summary>Builds the view of a readable project.</summary>
    /// <param name="reading">The project as read.</param>
    /// <param name="pieces">Its pieces of work, as <see cref="PiecesOfWork.Build"/> gave them.</param>
    /// <param name="links">The links read against the board.</param>
    /// <param name="tabs">One tab per project that reads.</param>
    /// <param name="settings">Where its folds and the pane's width are kept.</param>
    /// <param name="actions">What its buttons run.</param>
    /// <param name="back">Back to the board.</param>
    /// <param name="edits">The edits it offers on the project file, or null.</param>
    /// <param name="previous">The view it replaces, whose selection it keeps while the piece and stage still exist.</param>
    public static ProjectViewModel Build(ProjectFileReading reading, IReadOnlyList<PieceOfWork> pieces, LinkStatus links,
        IReadOnlyList<ProjectTabViewModel> tabs, ISettingsStore settings, ProjectViewActions actions, ICommand? back = null,
        ProjectViewEdits? edits = null, ProjectViewModel? previous = null, ProjectOverview? overview = null, string? lead = null)
    {
        ArgumentNullException.ThrowIfNull(reading);
        ArgumentNullException.ThrowIfNull(pieces);
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(tabs);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(actions);

        var file = reading.File ?? throw new ArgumentException("Only a readable project has a view.", nameof(reading));
        var tree = PieceTree.Build(pieces, links, file.Rounds);
        var marks = ObjectiveGrouping.Marks(tree);
        var view = new ProjectViewModel(file, ProjectSummary.For(pieces), settings, actions, edits) { BackCommand = back };
        view.Overview = overview ?? ProjectOverview.For(file, pieces, links.Links, [], 0);

        if ((lead ?? ProjectLead.KeyOf(file, new Dictionary<string, string>())) is { } leadKey)
        {
            var card = pieces.Select(piece => piece.Card).FirstOrDefault(each => string.Equals(each?.Key, leadKey, StringComparison.OrdinalIgnoreCase));
            var state = card switch
            {
                null => "not on the board",
                { IsWorking: true } => "busy",
                { Lifecycle: Theseus.Core.Domain.SessionLifecycle.NeedsAttention } => "asking you something",
                { HasSession: true } => "idle",
                _ => "no session",
            };
            view._leadName = card?.Title ?? leadKey;
            view._leadKey = leadKey;
            view.LeadText = $"Lead: {view._leadName} · {state}";
            view.FocusLeadCommand = new RelayCommand(_ => actions.Focus(leadKey), _ => card?.HasSession == true);
            view.FocusLeadToolTip = card?.HasSession == true
                ? $"Bring {card.Title}'s terminal forward"
                : $"{card?.Title ?? leadKey} has no session: start it first";
        }
        var specs = SpecsOf(reading, file.Name, pieces);
        var parents = tree.All.SelectMany(node => node.Children.Select(child => (Child: child.Piece, Parent: node.Piece)))
            .ToDictionary(pair => pair.Child, pair => pair.Parent);

        PieceNodeViewModel Node(PieceNode node) => view.NodeFor(node, [.. node.Children.Select(Node)], reading, file, links,
            tree, parents, specs.GetValueOrDefault(node.Piece.Name), marks.GetValueOrDefault(node.Piece) ?? []);

        view.Roots = [.. tree.Roots.Select(Node)];
        view.Tabs = tabs;
        view.WaitingSpecs = [.. pieces.Select(piece => specs.GetValueOrDefault(piece.Name)).OfType<SpecAtGate>()];

        var byPiece = view.AllNodes.ToDictionary(node => node.Piece);

        view.Rounds = [.. tree.Rounds.Select((round, index) => view.RoundFor(round, tree.Rounds.ElementAtOrDefault(index + 1)?.Round,
            index == tree.Rounds.Count - 1, file, byPiece))];

        view.Waits = [.. view.Overview.Waits.Select(item => item.Kind == WaitKind.CheckCannotRun
            ? view.CannotRunLine(item)
            : new WaitItemViewModel(item, view.WaitCommand(item, reading.Name)))];
        view.OtherWaits = [.. view.Overview.Others.Select(item => new WaitItemViewModel(item, view.WaitCommand(item, reading.Name)))];
        view._waitingPieces = view.AllNodes.Where(node => view.Overview.Waits.Any(wait => Concerns(wait, node)))
            .Select(node => node.Name).ToHashSet(StringComparer.Ordinal);
        view._waitsWithNoBox = view.Overview.Waits.Count(wait => !view.AllNodes.Any(node => Concerns(wait, node)));
        view.Counts = [.. StateCountViewModel.For(view.Summary, zeros: true, view.Overview.Waits.Count).Select(chip => chip with
        {
            Command = new RelayCommand(_ => view.ToggleFilter(chip.State,
                System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control))),
        })];
        view.Phases = [.. ObjectiveGrouping.Phases(file, pieces).Select(group => view.PhaseFor(group, byPiece, edits))];
        view.Unlisted = ObjectiveGrouping.Unlisted(pieces) is { } unlisted ? [view.ObjectiveFor(unlisted, null, byPiece, edits)] : [];
        view._thenWorkedOn = tree.ThenWorkedOn;

        view.Signature = view.SignatureOf();

        // The window is as wide as it was, so a rebuild keeps the pane docked or not, and says nothing again.
        view._isPaneDocked = previous?._isPaneDocked ?? true;

        if (previous is not null && string.Equals(previous.Name, view.Name, StringComparison.OrdinalIgnoreCase))
        {
            view.Select(view.Find(previous._selection), reveal: false);

            // A round's "Set out to meet" table stays as the owner left it across refreshes.
            foreach (var round in view.Rounds)
            {
                round.IsMeetOpen = previous.Rounds.FirstOrDefault(old => old.Round.Number == round.Round.Number)?.IsMeetOpen ?? false;
            }

            // The view is rebuilt whenever a session or a file changes, so the owner's filter stays on (review of PR 4, 1).
            if (previous._filter.Count > 0)
            {
                view._filter = [.. previous._filter];
                view.ApplyFilter();
            }
            view.SetOverlay(previous._isOverlayOpen && view._selection is not null);
        }

        return view;
    }

    /// <summary>
    /// Selects a box, a stage, a heading, or nothing, and shows it in Properties (AC 32, 49); while the pane is not
    /// docked, selecting something opens Properties as an overlay (AC 53).
    /// </summary>
    public void Select(object? item) => Select(item, reveal: true);

    // A rebuild restores the selection without revealing it: a selected piece that finished moves under the folded done
    // heading, and opening that by itself would be remembered (review of the state headings, 1).
    private void Select(object? item, bool reveal)
    {
        switch (_selection)
        {
            case PieceNodeViewModel oldPiece:
                oldPiece.IsSelected = false;
                break;
            case StageNodeViewModel oldStage:
                oldStage.IsSelected = false;
                oldStage.Piece.IsSelected = false;
                break;
            case PhaseHeadingViewModel oldPhase:
                oldPhase.IsSelected = false;
                break;
            case ObjectiveHeadingViewModel oldObjective:
                oldObjective.IsSelected = false;
                break;
        }

        _selection = item is PieceNodeViewModel or StageNodeViewModel or PhaseHeadingViewModel or ObjectiveHeadingViewModel ? item : null;

        // A box he selects is never hidden under a folded heading of its state.
        if (reveal && (item as PieceNodeViewModel ?? (item as StageNodeViewModel)?.Piece) is { } box
            && StateGroups.FirstOrDefault(group => !group.IsOpen && group.Boxes.Contains(box)) is { } folded)
        {
            ToggleFold(folded);
        }

        switch (_selection)
        {
            case PieceNodeViewModel piece:
                piece.IsSelected = true;
                Properties = PropertiesViewModel.ForPiece(piece, _actions, _edits, Overview.OwnerNames);
                break;
            case StageNodeViewModel stage:
                stage.IsSelected = true;
                stage.Piece.IsSelected = true;
                Properties = PropertiesViewModel.ForStage(stage, _actions, _edits, Overview.OwnerNames);
                break;
            case PhaseHeadingViewModel phase:
                phase.IsSelected = true;
                Properties = PropertiesViewModel.ForPhase(phase);
                break;
            case ObjectiveHeadingViewModel objective:
                objective.IsSelected = true;
                Properties = PropertiesViewModel.ForObjective(objective);
                break;
            default:
                Properties = PropertiesViewModel.Empty;
                break;
        }

        SetOverlay(_selection is not null && !_isPaneDocked);
        Raise(nameof(Selection));
        CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>
    /// Whether the view is wide enough for the pane beside the flow, as the view finds on each resize (AC 52, 53). On
    /// the change to too narrow the status bar says so once; on the change back the overlay closes and the pane docks.
    /// </summary>
    public void PaneFits(bool fits)
    {
        if (fits == _isPaneDocked)
        {
            return;
        }

        _isPaneDocked = fits;

        if (fits)
        {
            SetOverlay(false);
        }
        else
        {
            _actions.Notice?.Invoke(PaneHiddenNotice);
        }

        Raise(nameof(IsPaneDocked));
    }

    /// <summary>Folds or opens one box, and remembers it (AC 33).</summary>
    public void Toggle(PieceNodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);

        ToggleFold(node);
    }

    /// <summary>Saves the pane's width as the splitter left it, kept within its bounds by the view (AC 35).</summary>
    public void RememberPaneWidth(double width)
    {
        PaneWidth = width;

        var settings = _settings.Read();
        var sizes = new Dictionary<string, double>(settings.PaneSizes) { [ProjectPaneWidth.SettingsKey] = width };

        _settings.Write(settings with { PaneSizes = sizes });
    }

    /// <summary>A double-click on the splitter: the pane back to 320 px, and remembered.</summary>
    public double ResetPaneWidth()
    {
        RememberPaneWidth(ProjectPaneWidth.Default);

        return PaneWidth;
    }

    /// <summary>The label of Add to project… on a box no project file lists (1.1e AC 6).</summary>
    public const string AddToProjectLabel = "Add to project…";

    /// <summary>Open records, disabled with its reason when the piece has no records folder (AC 27).</summary>
    internal static PieceActionViewModel OpenRecords(PieceOfWork piece, ProjectViewActions actions) =>
        piece.RecordsFolder is { } folder
            ? new PieceActionViewModel("Open records",
                () => (actions.ShowRecords ?? ((_, _) => actions.OpenPath(folder)))(piece, null),
                toolTip: "Show its records: each stage's document, what it is for and where it stands")
            : new PieceActionViewModel("Open records", () => { }, false, "No records folder in the project yet");

    private PieceNodeViewModel NodeFor(PieceNode node, IReadOnlyList<PieceNodeViewModel> children, ProjectFileReading reading,
        ProjectFile file, LinkStatus links, PieceTree tree, IReadOnlyDictionary<PieceOfWork, PieceOfWork> parents, SpecAtGate? spec,
        IReadOnlyList<string> crossMarks)
    {
        var piece = node.Piece;
        var starts = VerifierStarts(piece);
        var stages = piece.Stages
            .Select((stage, index) => new StageNodeViewModel(stage, index, Select,
                [.. stage.Actions.Select(offer => Action(offer, piece, stage, spec)), .. stage.Stage == PipelineStage.Verify ? starts : []]))
            .ToList();

        // Each kind of action once, in AC 27's order; a stage's own record is opened from its menu or Properties.
        var boxActions = piece.Stages
            .SelectMany(stage => stage.Actions.Where(offer => offer.Action != StageAction.OpenRecord).Select(offer => (Offer: offer, Stage: stage)))
            .GroupBy(entry => entry.Offer.Action)
            .OrderBy(group => Array.IndexOf(BoxActionOrder, group.Key))
            .Select(group => Action(group.First().Offer, piece, group.First().Stage, spec))
            .ToList();
        // The way on, as a link, right after the button it unblocks (1.1e AC 6a, 6c).
        for (var index = boxActions.Count - 1; index >= 0; index--)
        {
            var offer = piece.Stages.SelectMany(stage => stage.Actions).FirstOrDefault(each => each.Label == boxActions[index].Label);

            if (!boxActions[index].IsEnabled && offer is not null && _actions.WayOn?.Invoke(piece.CardKey, offer.Action) is { } wayOn)
            {
                boxActions.Insert(index + 1, wayOn);
            }
        }

        boxActions.AddRange(starts);

        var addToProject = !piece.IsListed && _actions.AddToProject is { } add
            ? new PieceActionViewModel(AddToProjectLabel, () => add(reading.Name, piece.CardKey),
                toolTip: $"Add {piece.Workstream} to an objective of this project in project.md")
            : null;

        if (addToProject is not null)
        {
            boxActions.Add(addToProject);
        }

        boxActions.AddRange(_actions.CardActions?.Invoke(piece) ?? []);

        return new PieceNodeViewModel(node, children, stages, boxActions, PieceFolds.IsOpen(_folds, piece.Name), Toggle, Select)
        {
            LinkLines = LinkLines(node, links, tree, parents),
            Evidence = EvidenceOf(reading.Artefacts.GetValueOrDefault(piece.Name)),
            Objective = piece.ObjectiveNumber is { } number
                ? file.Objectives.FirstOrDefault(objective => objective.Number == number)
                : null,
            Spec = spec,
            CrossMarks = crossMarks,
            AddToProject = addToProject,
            Proposals = piece.IsCurrentPiece ? _actions.ProposalsOf?.Invoke(piece.CardKey) ?? [] : [],
            ServesCommand = piece.ObjectiveNumber is { } served ? new RelayCommand(_ => GoToObjective(served)) : null,
            LaterReport = piece.LaterReport is { } later && piece.LaterReportLink is { } link && _actions.ReportOpened?.Invoke(link, later.AtUtc) != true
                ? later
                : null,
            OpenLaterReport = piece.LaterReport is { } shown && piece.LaterReportLink is { } on
                ? new RelayCommand(_ =>
                    {
                        if (_actions.OpenReport is { } open)
                        {
                            open(on, shown.AtUtc, shown.Document);
                        }
                        else
                        {
                            _actions.OpenPath(shown.Document);
                        }
                    },
                    _ => System.IO.File.Exists(shown.Document))
                : null,
            // Only where starting it is the box's next step: a built piece waiting for its check keeps its own answer.
            StartedBy = piece.Status.Action == PieceAction.Start && boxActions.Any(action => action.Label == WhoStarts.StartAnywayLabel)
                ? _actions.WhoStarts?.Invoke(piece.CardKey)?.Line
                : null,
        };
    }

    // Headings start open and a done phase folded (AC 46); after that, as the owner left them (AC 50).
    private PhaseHeadingViewModel PhaseFor(PhaseGroup group, IReadOnlyDictionary<PieceOfWork, PieceNodeViewModel> byPiece,
        ProjectViewEdits? edits)
    {
        var title = $"Phase {group.Phase.Number.ToString(CultureInfo.InvariantCulture)}: {group.Phase.Name}";
        var objectives = group.Objectives.Select(objective => ObjectiveFor(objective, title, byPiece, edits)).ToList();

        IReadOnlyList<WaitItemViewModel> held = [.. Overview.Held.GetValueOrDefault(group.Phase.Number, [])
            .Select(item => new WaitItemViewModel(item, new RelayCommand(_ => Respond([item]))))];

        return new PhaseHeadingViewModel(group, objectives,
            PieceFolds.IsOpen(_folds, PieceFolds.PhaseEntry(group.Phase.Name), !group.IsDone), ToggleFold, Select, edits)
        {
            Held = held,
            RespondCommand = held.Count == 0 ? null : new RelayCommand(_ => Respond(held.Select(line => line.Item))),
        };
    }

    private ObjectiveHeadingViewModel ObjectiveFor(ObjectiveGroup group, string? phaseTitle,
        IReadOnlyDictionary<PieceOfWork, PieceNodeViewModel> byPiece, ProjectViewEdits? edits) =>
        new(group, phaseTitle, [.. group.Pieces.Select(piece => byPiece[piece])],
            PieceFolds.IsOpen(_folds, group.Objective is { } objective ? PieceFolds.ObjectiveEntry(objective.Title) : PieceFolds.UnlistedEntry,
                true), ToggleFold, Select, edits, Waits, Overview.OwnerNames)
        {
            RespondCommand = new RelayCommand(_ => Respond(CriterionWaitsFirst(Waits.Select(wait => wait.Item)
                .Where(ObjectiveWaits.Belongs(group.Objective?.Number, group.Pieces)).ToList()))),
        };

    // A banner's Respond… ticks its criteria's asks, which the checklist's actions apply to; a proposal or a report under the
    // same objective keeps its own button and is ticked only when nothing else is (review of the follow-on, nit 3).
    private static IReadOnlyList<WaitItem> CriterionWaitsFirst(IReadOnlyList<WaitItem> waits) =>
        waits.Any(wait => wait.Criteria.Count > 0) ? [.. waits.Where(wait => wait.Criteria.Count > 0)] : waits;

    private PieceActionViewModel Action(StageActionOffer offer, PieceOfWork piece, StageNode stage, SpecAtGate? spec)
    {
        var key = piece.CardKey;

        (Action Run, string? ToolTip) what = offer.Action switch
        {
            StageAction.OpenSpec => (() => Run(spec, _actions.OpenSpec), "Open the spec in its window; Accept spec… there accepts the version on screen"),
            StageAction.AcceptSpec => (() => Run(spec, _actions.AcceptSpec), AcceptSpecToolTip),
            StageAction.VerifyWithHoldouts => (() => _actions.VerifyWithHoldouts(key),
                $"Create a verifier card for {piece.Workstream}: it runs the holdout scenarios and reports only a verdict"),
            StageAction.Focus => (() => _actions.Focus(key), "Bring its terminal forward"),
            StageAction.Start => (() => _actions.Start(key), $"Start {piece.Workstream}"),
            StageAction.Stop => (() => _actions.Stop(key), $"Ask {piece.Workstream} to write its handover and stop"),
            StageAction.OpenRecord when _actions.ShowRecords is { } show => (() => show(piece, stage.Name),
                $"Show {stage.RecordName} in the Records window"),
            StageAction.OpenRecord => (() => _actions.OpenPath(stage.RecordFile!), stage.RecordFile),
            _ when _actions.ShowRecords is { } show => (() => show(piece, null),
                "Show its records: each stage's document, what it is for and where it stands"),
            _ => (() => _actions.OpenPath(piece.RecordsFolder!), piece.RecordsFolder),
        };

        // A button that cannot run is shown disabled with its own reason, never enabled to do nothing (1.1e AC 6a).
        var why = offer.IsEnabled && offer.Action != StageAction.OpenRecords && offer.Action != StageAction.OpenRecord
            ? _actions.WhyNot?.Invoke(key, offer.Action)
            : null;

        var isEnabled = offer.IsEnabled && why is null;
        var reason = offer.DisabledReason ?? why;

        // In a running project the project decides what runs: Start is the owner's override, asked about as before (item 5).
        if (offer.Action == StageAction.Start && _actions.WhoStarts?.Invoke(key) is { } run)
        {
            return new PieceActionViewModel(WhoStarts.StartAnywayLabel, what.Run, isEnabled,
                reason ?? $"{run.Line ?? "Its project is running and decides what runs."} Start it yourself instead: you are asked first.");
        }

        return new PieceActionViewModel(offer.Label, what.Run, isEnabled, reason ?? what.ToolTip)
        {
            Note = !isEnabled && offer.Action == StageAction.VerifyWithHoldouts ? reason : null,
        };
    }

    // A verifier card has no box of its own (AC 13). Theseus starts one as it makes it (Nigel, 2026-11-23), so its subject's
    // box and Verify stage offer it only when that start failed, with the card's own Start.
    private List<PieceActionViewModel> VerifierStarts(PieceOfWork piece) =>
    [
        .. piece.PendingVerifiers.Where(VerifierCards.NeedsAStart).Select(verifier =>
            new PieceActionViewModel(
                $"Start {verifier.Title} again", () => StartVerifier(verifier.Key), verifier.CanStart,
                verifier.CanStart
                    ? $"{VerifierCards.NotRunning(verifier)} Start it again: it runs the holdout scenarios against {piece.Workstream}"
                    : $"{verifier.Title} cannot be started from the board now")),
    ];

    // A verifier is started by the shell's own start for one, not the card's Start, which in a stopped project starts the
    // whole project (review of the auto-start).
    private void StartVerifier(string key) => (_actions.StartVerifier ?? _actions.Start)(key);

    private static void Run(SpecAtGate? spec, Action<SpecAtGate> action)
    {
        if (spec is not null)
        {
            action(spec);
        }
    }

    // The owner's own press opens the box, so it is remembered like any other toggle (AC 20, 33).
    private void Reveal(StageNodeViewModel stage)
    {
        if (!stage.Piece.IsOpen)
        {
            Toggle(stage.Piece);
        }

        Select(stage);
    }

    private IEnumerable<IFoldable> Headings =>
        Phases.Cast<IFoldable>().Concat(AllObjectives).Concat(Rounds).Concat(StateGroups);

    private RoundViewModel RoundFor(PieceRound round, ProjectRound? next, bool isLatest, ProjectFile file,
        IReadOnlyDictionary<PieceOfWork, PieceNodeViewModel> byPiece)
    {
        var boxes = round.All.Select(node => byPiece[node.Piece]).ToList();
        IReadOnlyList<RoundStateGroupViewModel> items =
        [
            .. StateCountViewModel.States
                .Where(state => boxes.Any(box => box.State == state))
                .Select(state => new RoundStateGroupViewModel(round.Round.Number, state, [.. boxes.Where(box => box.State == state)],
                    PieceFolds.IsOpen(_folds, RoundStateGroupViewModel.Entry(round.Round.Number, state), RoundStateGroupViewModel.OpensAtFirst(state)),
                    ToggleFold)),
        ];

        var objectives = file.Objectives.Where(objective => objective.Round == round.Round.Number)
            .Select(objective => new RoundObjectiveViewModel(objective, new RelayCommand(_ => GoToObjective(objective.Number))))
            .ToList();

        return new RoundViewModel(round.Round, next, isLatest, objectives, items, boxes,
            RoundViewModel.IsOpenIn(_folds, round.Round.Number, isLatest), ToggleFold)
        {
            Line = Overview.RoundLines.GetValueOrDefault(round.Round.Number) ?? string.Empty,
        };
    }

    /// <summary>
    /// Shows only the pieces in a state, or adds it with Ctrl; the same chip again, or Esc, shows everything (1.1e item 26).
    /// A shown piece keeps its round and its state's heading, or, in Objectives only, its phase and objective.
    /// </summary>
    public void ToggleFilter(StageNodeState state, bool add)
    {
        if (add)
        {
            if (!_filter.Remove(state))
            {
                _filter.Add(state);
            }
        }
        else
        {
            _filter = _filter.Count == 1 && _filter.Contains(state) ? [] : [state];
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        bool Shown(PieceNodeViewModel node) => _filter.Count == 0 || InFilter(node);

        foreach (var node in AllNodes)
        {
            node.IsShown = Shown(node);
        }

        // A heading counts and names only what the filter shows, and opens, unremembered, so the match is not behind it.
        foreach (var group in StateGroups)
        {
            group.ShowFiltered(opens: _filter.Count > 0);
        }

        foreach (var round in Rounds)
        {
            round.IsShown = round.Boxes.Any(box => box.IsShown);
        }

        // Objectives only the same way: a heading shows while it holds a match, and opens, unremembered, to show it.
        foreach (ObjectiveHeadingViewModel objective in AllObjectives)
        {
            objective.IsShown = _filter.Count == 0 || objective.Pieces.Any(node => node.IsShown)
                                || _filter.Contains(StageNodeState.WaitingOnYou) && objective.HasWaits;
            objective.IsOpen |= _filter.Count > 0 && objective.IsShown;
        }

        foreach (PhaseHeadingViewModel phase in Phases)
        {
            phase.IsShown = _filter.Count == 0 || phase.Objectives.Any(objective => objective.IsShown);
            phase.IsOpen |= _filter.Count > 0 && phase.IsShown;
        }

        Counts = [.. Counts.Select(chip => chip with { IsActive = _filter.Contains(chip.State) })];
        Raise(nameof(Counts));
        Raise(nameof(Filter));
        Raise(nameof(FilterText));
        Raise(nameof(IsFiltered));
        Raise(nameof(NoPieceWaits));
        Raise(nameof(HasNoPieceWaits));
    }

    // A wait concerns a box it names, or the current box of the card it concerns. One naming only an objective, such as
    // a criterion waiting outside Theseus, concerns no box: every done piece of that objective is not waiting on him.
    private static bool Concerns(WaitItem wait, PieceNodeViewModel node) =>
        wait.Pieces.Contains(node.Name, StringComparer.Ordinal)
        || (node.Piece.IsCurrentPiece && wait.CardKey is { } card && string.Equals(card, node.Piece.CardKey, StringComparison.OrdinalIgnoreCase));

    // "Waiting on you" counts the items of Waits for you, so it shows the boxes they concern as well as those in that state.
    private bool InFilter(PieceNodeViewModel node) =>
        _filter.Contains(node.State) || _filter.Contains(StageNodeState.WaitingOnYou) && _waitingPieces.Contains(node.Name);

    // Each kind of wait runs what its piece's own button runs, so the two never disagree (G37).
    // A check that cannot run offers its way on as its button, or none when there is nothing the owner can press.
    private WaitItemViewModel CannotRunLine(WaitItem item) =>
        item.CardKey is { } card && _actions.WayOn?.Invoke(card, StageAction.VerifyWithHoldouts) is { } wayOn
            ? new WaitItemViewModel(item, wayOn.Command, button: wayOn.Label, toolTip: wayOn.ToolTip)
            : new WaitItemViewModel(item, null);

    private System.Windows.Input.ICommand? WaitCommand(WaitItem item, string project)
    {
        var node = item.Pieces.Count == 0 ? null : AllNodes.FirstOrDefault(each => each.Name == item.Pieces[0]);

        return item.Kind switch
        {
            WaitKind.AcceptSpec or WaitKind.ConfirmAcceptance when node?.Spec is { } spec => new RelayCommand(_ => _actions.AcceptSpec(spec)),
            WaitKind.AcceptSpec when node?.PrimaryAction is { } action => action.Command,
            WaitKind.Answer when item.CardKey is { } card => new RelayCommand(_ => _actions.Focus(card)),
            WaitKind.Unblock when item.CardKey is { } card && node?.PrimaryAction is { } action => action.Command,
            WaitKind.StartVerifier when item.CardKey is { } verifier => new RelayCommand(_ => StartVerifier(verifier)),
            WaitKind.Decide or WaitKind.Outside => new RelayCommand(_ => Respond([item])),
            WaitKind.ClearVerifiers when _actions.ClearVerifiers is { } clear => new RelayCommand(_ => clear(project)),
            WaitKind.AddToObjective when item.CardKey is { } card && _actions.AddToProject is { } add => new RelayCommand(_ => add(project, card)),
            WaitKind.Proposal when item.ProposalId is { } proposal && _actions.ReviewProposal is { } review =>
                new RelayCommand(_ => review(proposal)),
            WaitKind.StartRequest when item.StartRequestId is { } request && _actions.AnswerStartRequest is { } answer =>
                new RelayCommand(_ => answer(request)),
            WaitKind.ConfirmReport or WaitKind.Proposal when item.CardKey is { } card && _actions.OpenLinks is { } open =>
                new RelayCommand(_ => open(card)),
            _ => null,
        };
    }

    private void ToggleFold(IFoldable fold)
    {
        fold.IsOpen = !fold.IsOpen;
        RememberFolds(PieceFolds.With(_folds, fold.FoldName, fold.IsOpen));
    }

    // Headings and boxes together, in either grouping, through the one * entry (AC 21, 48).
    private void SetAll(bool open)
    {
        foreach (var fold in AllNodes.Cast<IFoldable>().Concat(Headings).Where(fold => fold is not RoundViewModel))
        {
            fold.IsOpen = open;
        }

        // A round keeps its own fold, so the next open matches what is on screen (review of PR 3, finding 6).
        RememberFolds([.. PieceFolds.All(open), .. _folds.Where(entry => entry.StartsWith("round:", StringComparison.Ordinal))]);
    }

    // Grouped by objectives, a heading opens when a box under it does (AC 48). The Flow shows no headings, so there each
    // keeps its state: an entry for each heading not as it starts, since the * they may have followed is dropped (PR 4
    // review, finding 2).
    private void OpenWhatNeedsAttention()
    {
        foreach (var node in AllNodes)
        {
            node.IsOpen = NeedsAttention.Contains(node.State);
        }

        foreach (var round in Rounds)
        {
            round.IsOpen = round.IsLatest || round.Boxes.Any(box => NeedsAttention.Contains(box.State));
        }

        foreach (var group in StateGroups)
        {
            group.IsOpen = NeedsAttention.Contains(group.State);
        }


        if (!IsGroupedByObjectives)
        {
            var headings = Headings.Where(heading => heading.IsOpen != heading.StartsOpen).Select(heading => (heading.FoldName, heading.IsOpen));

            RememberFolds(PieceFolds.Each(AllNodes.Select(node => (node.Name, node.IsOpen)).Concat(headings)));

            return;
        }

        foreach (var objective in AllObjectives)
        {
            objective.IsOpen = objective.Pieces.Any(piece => piece.IsOpen);
        }

        foreach (var phase in Phases)
        {
            phase.IsOpen = phase.Objectives.Any(objective => objective.IsOpen);
        }

        RememberFolds(PieceFolds.Each(AllNodes.Cast<IFoldable>().Concat(Headings).Select(fold => (fold.FoldName, fold.IsOpen))));
    }

    private void Choose(bool chosen, ProjectViewGrouping grouping)
    {
        if (chosen && grouping != _grouping)
        {
            _grouping = grouping;

            var settings = _settings.Read();
            var groupings = new Dictionary<string, ProjectViewGrouping>(settings.ProjectGrouping) { [Name] = grouping };

            _settings.Write(settings with { ProjectGrouping = groupings });
            Signature = SignatureOf();

            // A heading is not drawn in the Flow, so Properties would show something no longer on screen (finding 4).
            if (grouping == ProjectViewGrouping.Flow && _selection is PhaseHeadingViewModel or ObjectiveHeadingViewModel)
            {
                Select(null);
            }
        }

        // Unticking the chosen grouping is not a choice of the other, so both are raised and it stays ticked.
        Raise(nameof(Grouping));
        Raise(nameof(IsGroupedByFlow));
        Raise(nameof(IsGroupedByObjectives));
        Raise(nameof(FlowRoots));
        Raise(nameof(ShownRounds));
        Raise(nameof(ShownPhases));
        Raise(nameof(ShownUnlisted));
    }

    private void Escape()
    {
        if (_isOverlayOpen)
        {
            SetOverlay(false);
        }
        else if (_selection is null && _filter.Count > 0)
        {
            _filter = [];
            ApplyFilter();
        }
        else
        {
            Select(null);
        }
    }

    private void SetOverlay(bool open)
    {
        if (_isOverlayOpen != open)
        {
            _isOverlayOpen = open;
            Raise(nameof(IsOverlayOpen));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void RememberFolds(IReadOnlyList<string> folds)
    {
        _folds = folds;

        var settings = _settings.Read();
        var views = new Dictionary<string, IReadOnlyList<string>>(settings.ViewFolds) { [PieceFolds.KeyFor(Name)] = folds };

        _settings.Write(settings with { ViewFolds = views });
    }

    private object? Find(object? older) => older switch
    {
        PieceNodeViewModel piece => AllNodes.FirstOrDefault(node => node.Name == piece.Name),
        StageNodeViewModel stage => AllNodes.FirstOrDefault(node => node.Name == stage.Piece.Name)?.Stages
            .FirstOrDefault(each => each.Name == stage.Name),
        PhaseHeadingViewModel phase => Phases.FirstOrDefault(each => each.Number == phase.Number),
        ObjectiveHeadingViewModel objective => AllObjectives.FirstOrDefault(each => each.Number == objective.Number),
        _ => null,
    };

    private static Dictionary<string, SpecAtGate> SpecsOf(ProjectFileReading reading, string project, IReadOnlyList<PieceOfWork> pieces)
    {
        var specs = new Dictionary<string, SpecAtGate>(StringComparer.Ordinal);

        foreach (var piece in pieces.Where(piece => piece.Stages.OfType<SpecStageNode>().Any(stage => stage.AwaitsAcceptance)))
        {
            if (reading.Artefacts.GetValueOrDefault(piece.Name) is { } artefacts
                && SpecAtGate.For(project, piece.Name, piece.Pipeline, artefacts) is { } spec)
            {
                specs[piece.Name] = spec;
            }
        }

        return specs;
    }

    private static string EvidenceOf(WorkstreamArtefacts? artefacts)
    {
        if (artefacts?.EvidenceText is not { } text)
        {
            return "none written";
        }

        return EvidenceBlockReader.Last(text) switch
        {
            null => "no block",
            { Result: { } result } => result,
            { Problem: var problem } => problem ?? "no result",
        };
    }

    private static IReadOnlyList<string> LinkLines(PieceNode node, LinkStatus links, PieceTree tree,
        IReadOnlyDictionary<PieceOfWork, PieceOfWork> parents)
    {
        var piece = node.Piece;
        var lines = new List<string>();

        if (parents.TryGetValue(piece, out var parent))
        {
            lines.Add($"{parent.Name} hands work to this{StateWords(HandOver(links, parent, piece), links)}");
        }

        lines.AddRange(node.Children.Select(child =>
            $"Hands work to {child.Piece.Name}{StateWords(HandOver(links, piece, child.Piece), links)}"));

        if (node.ReportBack is { } mark)
        {
            lines.Add($"Reports back to {mark.ParentPiece}{StateWords(mark.State)}");
        }

        lines.AddRange(tree.ThenWorkedOn.Where(link => link.From == piece).Select(link => $"Then worked on {link.To.Name}"));

        return lines;
    }

    private static Link? HandOver(LinkStatus links, PieceOfWork from, PieceOfWork to) =>
        links.Links.LastOrDefault(link => link.IsActive && link.Kind == LinkKind.HandsOver
                                          && string.Equals(link.From, from.CardKey, StringComparison.OrdinalIgnoreCase)
                                          && string.Equals(link.To, to.CardKey, StringComparison.OrdinalIgnoreCase));

    private static string StateWords(Link? link, LinkStatus links) => link is null ? string.Empty : StateWords(links.StateOf(link));

    // The board's own words for a link's state (LinkLine), so the view and a card's link line agree.
    private static string StateWords(LinkState state) => state switch
    {
        LinkState.Lineage => string.Empty,
        LinkState.Satisfied => ": accepted",
        LinkState.Reported => ": reported, not confirmed yet",
        LinkState.NoReportYet => ": ran since, no report yet",
        LinkState.ReportLost => ": report lost",
        LinkState.CardMissing => ": card missing",
        LinkState.Rejected => ": report rejected",
        _ => ": not accepted yet",
    };

    private string SignatureOf()
    {
        var view = this;

        IEnumerable<string> Lines(PieceNodeViewModel node, int depth) =>
            node.Children.SelectMany(child => Lines(child, depth + 1)).Prepend(string.Join("/",
                depth.ToString(CultureInfo.InvariantCulture), node.Name, node.Workstream, node.ColourHex, node.KindWord,
                node.ObjectiveLabel, node.StatusText, node.WhoActs, node.HandInText, node.ReportedAgain, node.Title, node.KindLabel, node.Gate?.ToolTip,
                node.Piece.Round, node.ReportBackText, node.IsReportLost, node.Piece.RecordsFolder,
                node.Evidence, node.Spec?.ContentHash, string.Join(";", node.CrossMarks), node.Piece.Card?.StopLine, node.BranchRulesToolTip,
                node.Objective is { } objective
                    ? $"{objective.Title}:{string.Join(",", objective.Criteria.Select(each => $"{each.Text}={each.IsTicked}"))}"
                    : string.Empty,
                string.Join(";", node.LinkLines),
                string.Join(";", node.Actions.Select(action => $"{action.Label}:{action.IsEnabled}:{action.ToolTip}")),
                string.Join(";", node.Stages.Select(stage =>
                    $"{stage.Name}={stage.State}={stage.Node.Reason}={stage.Node.RecordFile}"
                    + $"={string.Join(",", (stage.Node as VerifyStageNode)?.Verifiers ?? [])}"
                    + "=" + string.Join(",", stage.MenuItems.Select(item => $"{item.Label}:{item.IsEnabled}:{item.ToolTip}"))))));

        string Heading(ObjectiveHeadingViewModel objective) =>
            string.Join("/", objective.Title, objective.CriteriaText, objective.RollupText, objective.Rollup?.ToolTip,
                string.Join(",", objective.Counts.Select(chip => chip.Text)),
                string.Join(",", objective.Pieces.Select(piece => piece.Name)));

        return string.Join("|", new[] { view.Name, view.Title, view.Sentence, view.ClearUpNotice ?? string.Empty, view.UnderwayText, view.LeadWaitsText, view.OthersText,
                view._grouping.ToString(),
                string.Join(";", view.Waits.Concat(view.OtherWaits).Concat(view.Phases.SelectMany(phase => phase.Held)).Select(wait =>
                    $"{wait.Text}:{wait.Button}:{string.Join(",", wait.Item.Pieces)}:{wait.Item.CardKey}:"
                    + string.Join(",", wait.Item.Criteria.Select(criterion => $"{criterion.Objective}/{criterion.Index}/{criterion.On}/{criterion.Ask}")))) }
            .Concat(view.Tabs.Select(tab => $"{tab.Name}:{tab.Title}:{tab.IsOpen}:{tab.ColourHex}"))
            .Concat(view.Roots.SelectMany(root => Lines(root, 0)))
            .Concat(view._thenWorkedOn.Select(link => $"{link.From.Name}>{link.To.Name}"))
            .Concat(view.Rounds.Select(round => string.Join("/", round.Title, round.Dates, round.StateText, round.Line, round.Banner,
                string.Join(",", round.Objectives.Select(objective => $"{objective.Text}:{objective.CriteriaText}")),
                string.Join(",", round.Items.Select(group => $"{group.State}:{string.Join(",", group.Boxes.Select(box => box.Name))}")))))
            .Concat(view.Phases.Select(phase => $"{phase.Title}/{phase.ExitGate}/{phase.DoneText}/{phase.RollupText}/{phase.Rollup.ToolTip}:"
                                                + string.Join(";", phase.Objectives.Select(Heading))))
            .Concat(view.Unlisted.Select(Heading)));
    }
}
