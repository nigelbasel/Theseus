using System.Globalization;
using System.Windows.Input;
using Theseus.Core.Links;
using Theseus.Core.Projects;
using Theseus.Core.Settings;

namespace Theseus.App.ViewModels;

/// <summary>
/// The whole-screen view of one project (1.1d AC 15 to 37; Nigel accepted the mockup, 2026-11-20): where the project is
/// in one sentence and six counts, then its pieces of work as a tree of boxes that runs down the page, or under their
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
    private IReadOnlyList<string> _folds;
    private object? _selection;
    private PropertiesViewModel _properties = PropertiesViewModel.Empty;
    private ProjectViewGrouping _grouping;
    private bool _isPaneDocked = true;
    private bool _isOverlayOpen;
    private IReadOnlyList<ThenWorkedOnLink> _thenWorkedOn = [];

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
        _grouping = stored.ProjectGrouping.GetValueOrDefault(file.Name);

        CollapseAllCommand = new RelayCommand(_ => SetAll(false));
        ExpandAllCommand = new RelayCommand(_ => SetAll(true));
        OpenWhatNeedsAttentionCommand = new RelayCommand(_ => OpenWhatNeedsAttention());
        ClearSelectionCommand = new RelayCommand(_ => Escape(), _ => _selection is not null);
        CloseOverlayCommand = new RelayCommand(_ => SetOverlay(false), _ => _isOverlayOpen);
    }

    /// <summary>The project's name.</summary>
    public string Name { get; }

    /// <summary>Its title.</summary>
    public string Title { get; }

    /// <summary>Where the project is (AC 8): the only source of its counts and sentence.</summary>
    public ProjectSummary Summary { get; }

    /// <summary>The summary's sentence.</summary>
    public string Sentence => Summary.Sentence;

    /// <summary>The six count chips, zeros included (AC 19).</summary>
    public IReadOnlyList<StateCountViewModel> Counts => StateCountViewModel.For(Summary, zeros: true);

    /// <summary>
    /// The project's own colour, for the summary's mark (1.1d AC 40): its tab's, or null before it has one, when the mark
    /// takes the accent colour.
    /// </summary>
    public string? ColourHex => Tabs.FirstOrDefault(tab => string.Equals(tab.Name, Name, StringComparison.OrdinalIgnoreCase))?.ColourHex;

    /// <summary>The Waiting on you band's buttons, one per waiting piece in file order (AC 20).</summary>
    public IReadOnlyList<WaitingButtonViewModel> Waiting { get; private set; } = [];

    /// <summary>Whether the band shows: only while something waits on the owner.</summary>
    public bool HasWaiting => Waiting.Count > 0;

    /// <summary>The legend's states, in the order AC 22 gives.</summary>
    public static IReadOnlyList<LegendItemViewModel> Legend { get; } =
    [
        new(StageNodeState.Done), new(StageNodeState.WaitingOnYou), new(StageNodeState.Busy), new(StageNodeState.Stuck),
        new(StageNodeState.HoldoutsNotRun), new(StageNodeState.ToDo),
    ];

    /// <summary>The tree's roots, in file order (AC 23).</summary>
    public IReadOnlyList<PieceNodeViewModel> Roots { get; private set; } = [];

    /// <summary>Every box, depth first.</summary>
    public IEnumerable<PieceNodeViewModel> AllNodes => Roots.SelectMany(root => root.SelfAndDescendants);

    /// <summary>The lines to draw between boxes (AC 25): hands work to, then then worked on.</summary>
    public IReadOnlyList<ConnectorLink> Connectors { get; private set; } = [];

    /// <summary>One tab per project that reads (AC 17).</summary>
    public IReadOnlyList<ProjectTabViewModel> Tabs { get; private set; } = [];

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
        ProjectViewEdits? edits = null, ProjectViewModel? previous = null)
    {
        ArgumentNullException.ThrowIfNull(reading);
        ArgumentNullException.ThrowIfNull(pieces);
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(tabs);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(actions);

        var file = reading.File ?? throw new ArgumentException("Only a readable project has a view.", nameof(reading));
        var tree = PieceTree.Build(pieces, links);
        var marks = ObjectiveGrouping.Marks(tree);
        var view = new ProjectViewModel(file, ProjectSummary.For(pieces), settings, actions, edits) { BackCommand = back };
        var specs = SpecsOf(reading, file.Name, pieces);
        var parents = tree.All.SelectMany(node => node.Children.Select(child => (Child: child.Piece, Parent: node.Piece)))
            .ToDictionary(pair => pair.Child, pair => pair.Parent);

        PieceNodeViewModel Node(PieceNode node) => view.NodeFor(node, [.. node.Children.Select(Node)], reading, file, links,
            tree, parents, specs.GetValueOrDefault(node.Piece.Name), marks.GetValueOrDefault(node.Piece) ?? []);

        view.Roots = [.. tree.Roots.Select(Node)];
        view.Tabs = tabs;
        view.WaitingSpecs = [.. pieces.Select(piece => specs.GetValueOrDefault(piece.Name)).OfType<SpecAtGate>()];

        var byPiece = view.AllNodes.ToDictionary(node => node.Piece);

        view.Connectors =
        [
            .. view.AllNodes.SelectMany(node => node.Children.Select(child => new ConnectorLink(node, child, ConnectorKind.HandsOver))),
            .. tree.ThenWorkedOn.Select(link => new ConnectorLink(byPiece[link.From], byPiece[link.To], ConnectorKind.ThenWorkedOn)),
        ];
        view.Waiting = [.. pieces.Select(piece => view.WaitingButton(byPiece[piece])).OfType<WaitingButtonViewModel>()];
        view.Phases = [.. ObjectiveGrouping.Phases(file, pieces).Select(group => view.PhaseFor(group, byPiece, edits))];
        view.Unlisted = ObjectiveGrouping.Unlisted(pieces) is { } unlisted ? [view.ObjectiveFor(unlisted, null, byPiece, edits)] : [];
        view._thenWorkedOn = tree.ThenWorkedOn;
        view.Signature = view.SignatureOf();

        // The window is as wide as it was, so a rebuild keeps the pane docked or not, and says nothing again.
        view._isPaneDocked = previous?._isPaneDocked ?? true;

        if (previous is not null && string.Equals(previous.Name, view.Name, StringComparison.OrdinalIgnoreCase))
        {
            view.Select(view.Find(previous._selection));
            view.SetOverlay(previous._isOverlayOpen && view._selection is not null);
        }

        return view;
    }

    /// <summary>
    /// The specs of a project's pieces that wait at their gate, for a project card's waiting count (project-view AC 12)
    /// without building the whole view.
    /// </summary>
    public static IReadOnlyList<SpecAtGate> SpecsWaiting(ProjectFileReading reading, IReadOnlyList<PieceOfWork> pieces)
    {
        ArgumentNullException.ThrowIfNull(reading);
        ArgumentNullException.ThrowIfNull(pieces);

        var specs = SpecsOf(reading, reading.Name, pieces);

        return [.. pieces.Select(piece => specs.GetValueOrDefault(piece.Name)).OfType<SpecAtGate>()];
    }

    /// <summary>
    /// Selects a box, a stage, a heading, or nothing, and shows it in Properties (AC 32, 49); while the pane is not
    /// docked, selecting something opens Properties as an overlay (AC 53).
    /// </summary>
    public void Select(object? item)
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

        switch (_selection)
        {
            case PieceNodeViewModel piece:
                piece.IsSelected = true;
                Properties = PropertiesViewModel.ForPiece(piece, _actions, _edits);
                break;
            case StageNodeViewModel stage:
                stage.IsSelected = true;
                stage.Piece.IsSelected = true;
                Properties = PropertiesViewModel.ForStage(stage, _actions, _edits);
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
            ? new PieceActionViewModel("Open records", () => actions.OpenPath(folder), toolTip: folder)
            : new PieceActionViewModel("Open records", () => { }, false, "No records folder in the project yet");

    private PieceNodeViewModel NodeFor(PieceNode node, IReadOnlyList<PieceNodeViewModel> children, ProjectFileReading reading,
        ProjectFile file, LinkStatus links, PieceTree tree, IReadOnlyDictionary<PieceOfWork, PieceOfWork> parents, SpecAtGate? spec,
        IReadOnlyList<string> crossMarks)
    {
        var piece = node.Piece;
        var stages = piece.Stages
            .Select((stage, index) => new StageNodeViewModel(stage, index, Select,
                [.. stage.Actions.Select(offer => Action(offer, piece, stage, spec))]))
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

        var addToProject = !piece.IsListed && _actions.AddToProject is { } add
            ? new PieceActionViewModel(AddToProjectLabel, () => add(reading.Name, piece.CardKey),
                toolTip: $"Add {piece.Workstream} to an objective of this project in project.md")
            : null;

        if (addToProject is not null)
        {
            boxActions.Add(addToProject);
        }

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
        };
    }

    // Headings start open and a done phase folded (AC 46); after that, as the owner left them (AC 50).
    private PhaseHeadingViewModel PhaseFor(PhaseGroup group, IReadOnlyDictionary<PieceOfWork, PieceNodeViewModel> byPiece,
        ProjectViewEdits? edits)
    {
        var title = $"Phase {group.Phase.Number.ToString(CultureInfo.InvariantCulture)}: {group.Phase.Name}";
        var objectives = group.Objectives.Select(objective => ObjectiveFor(objective, title, byPiece, edits)).ToList();

        return new PhaseHeadingViewModel(group, objectives,
            PieceFolds.IsOpen(_folds, PieceFolds.PhaseEntry(group.Phase.Name), !group.IsDone), ToggleFold, Select, edits);
    }

    private ObjectiveHeadingViewModel ObjectiveFor(ObjectiveGroup group, string? phaseTitle,
        IReadOnlyDictionary<PieceOfWork, PieceNodeViewModel> byPiece, ProjectViewEdits? edits) =>
        new(group, phaseTitle, [.. group.Pieces.Select(piece => byPiece[piece])],
            PieceFolds.IsOpen(_folds, group.Objective is { } objective ? PieceFolds.ObjectiveEntry(objective.Title) : PieceFolds.UnlistedEntry,
                true), ToggleFold, Select, edits);

    private PieceActionViewModel Action(StageActionOffer offer, PieceOfWork piece, StageNode stage, SpecAtGate? spec)
    {
        var key = piece.CardKey;

        (Action Run, string? ToolTip) what = offer.Action switch
        {
            StageAction.OpenSpec => (() => Run(spec, _actions.OpenSpec), "Open the spec as it was read, at the commit Accept spec names"),
            StageAction.AcceptSpec => (() => Run(spec, _actions.AcceptSpec), AcceptSpecToolTip),
            StageAction.VerifyWithHoldouts => (() => _actions.VerifyWithHoldouts(key),
                $"Create a verifier card for {piece.Workstream}: it runs the holdout scenarios and reports only a verdict"),
            StageAction.Focus => (() => _actions.Focus(key), "Bring its terminal forward"),
            StageAction.Start => (() => _actions.Start(key), $"Start {piece.Workstream}"),
            StageAction.Stop => (() => _actions.Stop(key), $"Ask {piece.Workstream} to write its handover and stop"),
            StageAction.OpenRecord => (() => _actions.OpenPath(stage.RecordFile!), stage.RecordFile),
            _ => (() => _actions.OpenPath(piece.RecordsFolder!), piece.RecordsFolder),
        };

        // A button that cannot run is shown disabled with its own reason, never enabled to do nothing (1.1e AC 6a).
        var why = offer.IsEnabled && offer.Action != StageAction.OpenRecords && offer.Action != StageAction.OpenRecord
            ? _actions.WhyNot?.Invoke(key, offer.Action)
            : null;

        return new PieceActionViewModel(offer.Label, what.Run, offer.IsEnabled && why is null, offer.DisabledReason ?? why ?? what.ToolTip);
    }

    private static void Run(SpecAtGate? spec, Action<SpecAtGate> action)
    {
        if (spec is not null)
        {
            action(spec);
        }
    }

    private WaitingButtonViewModel? WaitingButton(PieceNodeViewModel node)
    {
        if (node.State != StageNodeState.WaitingOnYou
            || node.Stages.FirstOrDefault(stage => stage.State == StageNodeState.WaitingOnYou) is not { } stage)
        {
            return null;
        }

        if (stage.Node is SpecStageNode { AwaitsAcceptance: true } && node.Spec is { } spec)
        {
            var noun = node.Piece.Pipeline == PipelineKind.Bug ? "rule" : "spec";

            return new WaitingButtonViewModel($"Accept the {noun} of {node.Name}…", new RelayCommand(_ =>
            {
                Reveal(stage);
                _actions.AcceptSpec(spec);
            }));
        }

        return new WaitingButtonViewModel($"Answer {node.Workstream} in its terminal", new RelayCommand(_ =>
        {
            Reveal(stage);
            _actions.Focus(node.Piece.CardKey);
        }));
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

    private IEnumerable<IFoldable> Headings => Phases.Cast<IFoldable>().Concat(AllObjectives);

    private void ToggleFold(IFoldable fold)
    {
        fold.IsOpen = !fold.IsOpen;
        RememberFolds(PieceFolds.With(_folds, fold.FoldName, fold.IsOpen));
    }

    // Headings and boxes together, in either grouping, through the one * entry (AC 21, 48).
    private void SetAll(bool open)
    {
        foreach (var fold in AllNodes.Cast<IFoldable>().Concat(Headings))
        {
            fold.IsOpen = open;
        }

        RememberFolds(PieceFolds.All(open));
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
        Raise(nameof(ShownPhases));
        Raise(nameof(ShownUnlisted));
    }

    private void Escape()
    {
        if (_isOverlayOpen)
        {
            SetOverlay(false);
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
            $"hands work to {child.Piece.Name}{StateWords(HandOver(links, piece, child.Piece), links)}"));

        if (node.ReportBack is { } mark)
        {
            lines.Add($"reports back to {mark.ParentPiece}{StateWords(mark.State)}");
        }

        lines.AddRange(tree.ThenWorkedOn.Where(link => link.From == piece).Select(link => $"then worked on {link.To.Name}"));

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
        _ => ": not accepted yet",
    };

    private string SignatureOf()
    {
        var view = this;

        IEnumerable<string> Lines(PieceNodeViewModel node, int depth) =>
            node.Children.SelectMany(child => Lines(child, depth + 1)).Prepend(string.Join("/",
                depth.ToString(CultureInfo.InvariantCulture), node.Name, node.Workstream, node.ColourHex, node.KindWord,
                node.ObjectiveLabel, node.StatusText, node.ReportBackText, node.IsReportLost, node.Piece.RecordsFolder,
                node.Evidence, node.Spec?.ContentHash, string.Join(";", node.CrossMarks), node.Piece.Card?.StopLine,
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
            string.Join("/", objective.Title, objective.CriteriaText, string.Join(",", objective.Counts.Select(chip => chip.Text)),
                string.Join(",", objective.Pieces.Select(piece => piece.Name)));

        return string.Join("|", new[] { view.Name, view.Title, view.Sentence, view._grouping.ToString() }
            .Concat(view.Tabs.Select(tab => $"{tab.Name}:{tab.Title}:{tab.IsOpen}:{tab.ColourHex}"))
            .Concat(view.Roots.SelectMany(root => Lines(root, 0)))
            .Concat(view._thenWorkedOn.Select(link => $"{link.From.Name}>{link.To.Name}"))
            .Concat(view.Phases.Select(phase => $"{phase.Title}/{phase.ExitGate}/{phase.DoneText}:"
                                                + string.Join(";", phase.Objectives.Select(Heading))))
            .Concat(view.Unlisted.Select(Heading)));
    }
}
