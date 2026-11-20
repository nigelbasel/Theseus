using System.Globalization;
using System.Windows.Input;
using Theseus.Core.Links;
using Theseus.Core.Projects;
using Theseus.Core.Settings;

namespace Theseus.App.ViewModels;

/// <summary>
/// The whole-screen view of one project (1.1d AC 15 to 37; Nigel accepted the mockup, 2026-11-20): where the project is
/// in one sentence and six counts, then its pieces of work as a tree of boxes that runs down the page, beside a
/// Properties pane. Every state it shows comes from the model in <c>Theseus.Core.Projects</c>; the view model only
/// lays it out, remembers what is folded and what is selected, and runs the shell's commands.
/// </summary>
public sealed class ProjectViewModel : ObservableObject
{
    /// <summary>Accept spec's tooltip, on the box and in Properties.</summary>
    internal const string AcceptSpecToolTip = "Pass this gate: asks first, naming the piece of work and the commit";

    private static readonly StageAction[] BoxActionOrder =
    [
        StageAction.OpenSpec, StageAction.AcceptSpec, StageAction.VerifyWithHoldouts, StageAction.Focus, StageAction.Start,
        StageAction.Stop, StageAction.OpenRecords,
    ];

    private static readonly (StageNodeState State, string Words)[] CountOrder =
    [
        (StageNodeState.Done, "done"), (StageNodeState.Busy, "busy"), (StageNodeState.WaitingOnYou, "waiting on you"),
        (StageNodeState.HoldoutsNotRun, "holdouts not run"), (StageNodeState.Stuck, "stuck"), (StageNodeState.ToDo, "to do"),
    ];

    private static readonly StageNodeState[] NeedsAttention =
        [StageNodeState.Busy, StageNodeState.WaitingOnYou, StageNodeState.Stuck, StageNodeState.HoldoutsNotRun];

    private readonly ISettingsStore _settings;
    private readonly ProjectViewActions _actions;
    private readonly ProjectViewEdits? _edits;
    private IReadOnlyList<string> _folds;
    private object? _selection;
    private PropertiesViewModel _properties = PropertiesViewModel.Empty;

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

        CollapseAllCommand = new RelayCommand(_ => SetAll(false));
        ExpandAllCommand = new RelayCommand(_ => SetAll(true));
        OpenWhatNeedsAttentionCommand = new RelayCommand(_ => OpenWhatNeedsAttention());
        ClearSelectionCommand = new RelayCommand(_ => Select(null), _ => _selection is not null);
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
    public IReadOnlyList<StateCountViewModel> Counts =>
        [.. CountOrder.Select(entry => new StateCountViewModel(entry.State, Summary.CountOf(entry.State), entry.Words))];

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

    /// <summary>What is selected: a <see cref="PieceNodeViewModel"/>, a <see cref="StageNodeViewModel"/>, or null.</summary>
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

    /// <summary>Esc: nothing selected (AC 16, 32).</summary>
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
        var view = new ProjectViewModel(file, ProjectSummary.For(pieces), settings, actions, edits) { BackCommand = back };
        var specs = SpecsOf(reading, file.Name, pieces);
        var parents = tree.All.SelectMany(node => node.Children.Select(child => (Child: child.Piece, Parent: node.Piece)))
            .ToDictionary(pair => pair.Child, pair => pair.Parent);

        PieceNodeViewModel Node(PieceNode node) => view.NodeFor(node, [.. node.Children.Select(Node)], reading, file, links,
            tree, parents, specs.GetValueOrDefault(node.Piece.Name));

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
        view.Signature = SignatureOf(view, tree);

        if (previous is not null && string.Equals(previous.Name, view.Name, StringComparison.OrdinalIgnoreCase))
        {
            view.Select(view.Find(previous._selection));
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

    /// <summary>Selects a box, a stage, or nothing, and shows it in Properties (AC 32).</summary>
    public void Select(object? item)
    {
        if (_selection is PieceNodeViewModel oldPiece)
        {
            oldPiece.IsSelected = false;
        }
        else if (_selection is StageNodeViewModel oldStage)
        {
            oldStage.IsSelected = false;
            oldStage.Piece.IsSelected = false;
        }

        _selection = item is PieceNodeViewModel or StageNodeViewModel ? item : null;

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
            default:
                Properties = PropertiesViewModel.Empty;
                break;
        }

        Raise(nameof(Selection));
        CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>Folds or opens one box, and remembers it (AC 33).</summary>
    public void Toggle(PieceNodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);

        node.IsOpen = !node.IsOpen;
        RememberFolds(PieceFolds.With(_folds, node.Name, node.IsOpen));
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

    /// <summary>Open records, disabled with its reason when the piece has no records folder (AC 27).</summary>
    internal static PieceActionViewModel OpenRecords(PieceOfWork piece, ProjectViewActions actions) =>
        piece.RecordsFolder is { } folder
            ? new PieceActionViewModel("Open records", () => actions.OpenPath(folder), toolTip: folder)
            : new PieceActionViewModel("Open records", () => { }, false, "No records folder in the project yet");

    private PieceNodeViewModel NodeFor(PieceNode node, IReadOnlyList<PieceNodeViewModel> children, ProjectFileReading reading,
        ProjectFile file, LinkStatus links, PieceTree tree, IReadOnlyDictionary<PieceOfWork, PieceOfWork> parents, SpecAtGate? spec)
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

        return new PieceNodeViewModel(node, children, stages, boxActions, PieceFolds.IsOpen(_folds, piece.Name), Toggle, Select)
        {
            LinkLines = LinkLines(node, links, tree, parents),
            Evidence = EvidenceOf(reading.Artefacts.GetValueOrDefault(piece.Name)),
            Objective = piece.ObjectiveNumber is { } number
                ? file.Objectives.FirstOrDefault(objective => objective.Number == number)
                : null,
            Spec = spec,
        };
    }

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

        return new PieceActionViewModel(offer.Label, what.Run, offer.IsEnabled, offer.DisabledReason ?? what.ToolTip);
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

    private void SetAll(bool open)
    {
        foreach (var node in AllNodes)
        {
            node.IsOpen = open;
        }

        RememberFolds(PieceFolds.All(open));
    }

    private void OpenWhatNeedsAttention()
    {
        foreach (var node in AllNodes)
        {
            node.IsOpen = NeedsAttention.Contains(node.State);
        }

        RememberFolds(PieceFolds.Each(AllNodes.Select(node => (node.Name, node.IsOpen))));
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

    private static string SignatureOf(ProjectViewModel view, PieceTree tree)
    {
        IEnumerable<string> Lines(PieceNodeViewModel node, int depth) =>
            node.Children.SelectMany(child => Lines(child, depth + 1)).Prepend(string.Join("/",
                depth.ToString(CultureInfo.InvariantCulture), node.Name, node.Workstream, node.ColourHex, node.KindWord,
                node.ObjectiveLabel, node.StatusText, node.ReportBackText, node.IsReportLost, node.Piece.RecordsFolder,
                node.Evidence, node.Spec?.ContentHash,
                node.Objective is { } objective
                    ? $"{objective.Title}:{string.Join(",", objective.Criteria.Select(each => $"{each.Text}={each.IsTicked}"))}"
                    : string.Empty,
                string.Join(";", node.LinkLines),
                string.Join(";", node.Actions.Select(action => $"{action.Label}:{action.IsEnabled}:{action.ToolTip}")),
                string.Join(";", node.Stages.Select(stage =>
                    $"{stage.Name}={stage.State}={stage.Node.Reason}={stage.Node.RecordFile}"
                    + $"={string.Join(",", (stage.Node as VerifyStageNode)?.Verifiers ?? [])}"
                    + "=" + string.Join(",", stage.MenuItems.Select(item => $"{item.Label}:{item.IsEnabled}:{item.ToolTip}"))))));

        return string.Join("|", new[] { view.Name, view.Title, view.Sentence }
            .Concat(view.Tabs.Select(tab => $"{tab.Name}:{tab.Title}:{tab.IsOpen}"))
            .Concat(view.Roots.SelectMany(root => Lines(root, 0)))
            .Concat(tree.ThenWorkedOn.Select(link => $"{link.From.Name}>{link.To.Name}")));
    }
}
