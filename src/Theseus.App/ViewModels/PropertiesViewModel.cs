using System.Windows.Input;
using Theseus.Core.Domain;
using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// The Properties pane of the project view (1.1d AC 34, 49): a hint while nothing is selected; for a piece of work or one
/// of its stages, its state in words, label and value rows, its links, what can be done, and its objective's criteria;
/// for an objective or phase heading, what it holds and its edits.
/// </summary>
public sealed class PropertiesViewModel
{
    /// <summary>What the pane says while nothing is selected.</summary>
    public const string HintText =
        "Click a piece of work, or one of its stages, to see its details here: its state in words, its record, its workstream and what you can do.";

    private PropertiesViewModel(string heading)
    {
        Heading = heading;
    }

    /// <summary>The pane with nothing selected.</summary>
    public static PropertiesViewModel Empty { get; } = new("Properties") { Hint = HintText };

    /// <summary>"Properties", the piece's name, or "&lt;Stage&gt; of &lt;piece&gt;".</summary>
    public string Heading { get; }

    /// <summary>The hint, while nothing is selected; otherwise empty.</summary>
    public string Hint { get; private init; } = string.Empty;

    /// <summary>Whether nothing is selected.</summary>
    public bool IsEmpty => Hint.Length > 0;

    /// <summary>The piece's status, or the stage's word.</summary>
    public string StatusText { get; private init; } = string.Empty;

    /// <summary>Whether it has a status line: everything but the empty pane.</summary>
    public bool HasStatus => StatusText.Length > 0;

    /// <summary>The state that colours the status.</summary>
    public StageNodeState State { get; private init; } = StageNodeState.ToDo;

    /// <summary>The label and value rows.</summary>
    public IReadOnlyList<PropertyRowViewModel> Rows { get; private init; } = [];

    /// <summary>A piece's Records panel: one row per stage, then its other records; empty for anything else (Nigel, 2026-11-22).</summary>
    public IReadOnlyList<RecordRowViewModel> Records { get; private init; } = [];

    /// <summary>What the Records panel says when there are no rows, or null.</summary>
    public string? RecordsNone { get; private init; }

    /// <summary>Whether it shows a Records panel.</summary>
    public bool HasRecords => Records.Count > 0 || RecordsNone is not null;

    /// <summary>One line per link, with its state in words.</summary>
    public IReadOnlyList<string> Links { get; private init; } = [];

    /// <summary>Whether it has a Links section.</summary>
    public bool HasLinks => Links.Count > 0;

    /// <summary>Accept spec… while it waits, then Open &lt;record&gt; or Open records.</summary>
    public IReadOnlyList<PieceActionViewModel> Buttons { get; private init; } = [];

    /// <summary>"Objective 1.1: Checks shown on cards", "Criteria" for an objective heading, or empty with no objective.</summary>
    public string ObjectiveHeading { get; private init; } = string.Empty;

    /// <summary>Whether it has an Objective section.</summary>
    public bool HasObjective => ObjectiveHeading.Length > 0;

    /// <summary>The objective's criteria, ticked in the project file.</summary>
    public IReadOnlyList<CriterionViewModel> Criteria { get; private init; } = [];

    /// <summary>Rename objective…, or null when the view cannot edit.</summary>
    public ICommand? EditObjectiveCommand { get; private init; }

    /// <summary>Whether Rename objective… is offered.</summary>
    public bool CanEditObjective => EditObjectiveCommand is not null;

    /// <summary>The pane for a piece of work.</summary>
    internal static PropertiesViewModel ForPiece(PieceNodeViewModel node, ProjectViewActions actions, ProjectViewEdits? edits)
    {
        var piece = node.Piece;
        var status = node.Status;

        // Properties carries what the box leaves out (1.1f H40): never a path or a folder name; Open records opens them.
        return new PropertiesViewModel(node.Title)
        {
            StatusText = status.Text,
            State = status.State,
            Rows =
            [
                new("Where", node.StatusText),
                new("What it means", status.Meaning),
                new("Who acts next", node.WhoActs),
                new("Kind", $"{node.KindLabel}: {PieceKinds.Means(piece.Kind)}"),
                new("Card", $"{piece.Workstream} ({SessionWords(piece.Card)})"),
                .. piece.Card?.StopLine is { } stopped ? new PropertyRowViewModel[] { new("Stopped", stopped) } : [],
                new("Objective", ObjectiveText(piece)),
                new("Round", $"Round {piece.Round}"),
                .. piece.ParentTitle is { } parent ? new PropertyRowViewModel[] { new("Handed on from", parent) } : [],
                new("Records", piece.RecordsFolder is null ? "none in the project" : "in the project: Open records below"),
                new("Branch", piece.Branch ?? "not recorded"),
                new("Evidence", node.Evidence),
                .. piece.IsFreeform ? [] : new PropertyRowViewModel[] { new("Accepted", AcceptedText(piece)) },
                .. node.Stages.Select(stage => new PropertyRowViewModel(stage.Name,
                    $"{stage.Mark} {stage.Word}{(stage.Node.Reason is { } why ? $": {why}" : string.Empty)}. {StageNodeStates.Meaning(stage.State)}")),
            ],
            Links = node.LinkLines,
            Records = [.. PieceRecords.For(piece).Select(entry => new RecordRowViewModel(entry, new RelayCommand(_ =>
            {
                if (actions.ShowRecords is { } show)
                {
                    show(piece, entry.IsOther ? null : entry.Stage);
                }
                else if (entry.File is { } file)
                {
                    actions.OpenPath(file);
                }
            })))],
            RecordsNone = PieceRecords.NoneText(piece),
            Buttons = [.. ButtonsOf(node, null, actions)],
            ObjectiveHeading = ObjectiveHeadingOf(node),
            Criteria = CriteriaOf(node, edits),
            EditObjectiveCommand = EditOf(node, edits),
        };
    }

    private static string SessionWords(PieceCard? card) => card switch
    {
        null => "not on the board",
        { IsWorking: true } => "its session is busy",
        { Lifecycle: SessionLifecycle.NeedsAttention, Question: { } question } => Theseus.Core.Questions.QuestionWords.Detail(question),
        { Lifecycle: SessionLifecycle.NeedsAttention } => "its session asks you something",
        { HasSession: true } => "its session is idle",
        _ => "no session",
    };

    /// <summary>The pane for one stage of a piece of work.</summary>
    internal static PropertiesViewModel ForStage(StageNodeViewModel stage, ProjectViewActions actions, ProjectViewEdits? edits)
    {
        var node = stage.Piece;
        var piece = node.Piece;
        var rows = new List<PropertyRowViewModel>
        {
            new("Stage", stage.Name),
            new("State", $"{stage.Mark} {stage.Word}"),
            new("Means", StageNodeStates.Meaning(stage.State)),
            new("Record", stage.Node.RecordFile is null ? "not written yet" : $"{stage.Node.RecordName}, in its records"),
            new("Piece", node.Title),
            new("Worker", piece.Workstream),
            new("Objective", ObjectiveText(piece)),
        };

        if (stage.Node.Reason is { } reason)
        {
            rows.Add(new("Why", reason));
        }

        if (stage.Node is VerifyStageNode { Verifiers.Count: > 0 } verify)
        {
            rows.Add(new("Verifier", string.Join(", ", verify.Verifiers)));
        }

        return new PropertiesViewModel($"{stage.Name} of {node.Title}")
        {
            StatusText = stage.Word,
            State = stage.State,
            Rows = rows,
            Links = node.LinkLines,
            Buttons = [.. ButtonsOf(node, stage, actions)],
            ObjectiveHeading = ObjectiveHeadingOf(node),
            Criteria = CriteriaOf(node, edits),
            EditObjectiveCommand = EditOf(node, edits),
        };
    }

    /// <summary>
    /// The pane for an objective heading of the view grouped by objectives (1.1d AC 49): the objective, its pieces of
    /// work and their counts, then its criteria to tick and Rename objective…. For the heading of pieces no project file
    /// lists, what they are.
    /// </summary>
    internal static PropertiesViewModel ForObjective(ObjectiveHeadingViewModel heading)
    {
        var pieces = heading.IsEmpty ? "nothing yet" : string.Join(", ", heading.Pieces.Select(piece => piece.Name));
        var counts = heading.Counts.Count == 0 ? "nothing yet" : string.Join("  ", heading.Counts.Select(chip => chip.Text));

        if (heading.Objective is not { } objective)
        {
            return new PropertiesViewModel(heading.Title)
            {
                StatusText = heading.CriteriaToolTip,
                Rows = [new("Pieces", pieces), new("Where", counts)],
            };
        }

        return new PropertiesViewModel($"Objective {objective.Number}: {objective.Title}")
        {
            StatusText = heading.CriteriaToolTip,
            State = objective.IsDone ? StageNodeState.Done : StageNodeState.ToDo,
            Rows =
            [
                new("Phase", heading.PhaseTitle ?? string.Empty),
                new("Pieces", pieces),
                new("Where", counts),
            ],
            ObjectiveHeading = "Criteria",
            Criteria = heading.Criteria,
            EditObjectiveCommand = heading.EditObjectiveCommand,
        };
    }

    /// <summary>The pane for a phase heading (1.1d AC 49): its exit gate, its objectives, and Add objective….</summary>
    internal static PropertiesViewModel ForPhase(PhaseHeadingViewModel heading) =>
        new(heading.Title)
        {
            StatusText = heading.DoneText,
            State = heading.IsDone ? StageNodeState.Done : StageNodeState.ToDo,
            Rows =
            [
                new("Exit gate", heading.Group.Phase.ExitGate.Length == 0 ? "none written" : heading.Group.Phase.ExitGate),
                new("Objectives", heading.Objectives.Count == 0
                    ? "none yet"
                    : string.Join("; ", heading.Objectives.Select(objective => objective.Title))),
            ],
            Buttons = heading.AddObjective is { } add ? [add] : [],
        };

    // When, by whom and how its spec was accepted, and whether a session reported more since (1.1e AC 29).
    private static string AcceptedText(PieceOfWork piece)
    {
        var accepted = piece.Acceptance?.Describe(TimeZoneInfo.Local) ?? "not yet";

        // Said when the record is of an earlier version (review of PR 6, finding 15).
        if (piece.Acceptance is not null && piece.Stages.OfType<SpecStageNode>().Any(spec => spec.IsChangedSinceAccepted))
        {
            accepted += "; an earlier version: the spec has changed since";
        }

        return piece.Reported is { } reported
            ? $"{accepted}; {reported.CardTitle}'s session reports you accepted version {reported.ContentHash[..Math.Min(7, reported.ContentHash.Length)]} in its tab: confirm on the board"
            : accepted;
    }

    private static string ObjectiveText(PieceOfWork piece) =>
        piece.ObjectiveNumber is { } number ? $"{number} {piece.ObjectiveTitle}" : "not in project.md";

    private static IEnumerable<PieceActionViewModel> ButtonsOf(PieceNodeViewModel node, StageNodeViewModel? stage,
        ProjectViewActions actions)
    {
        var waits = stage is null
            ? node.Piece.Stages.OfType<SpecStageNode>().Any(spec => spec.AwaitsAcceptance)
            : stage.Node is SpecStageNode { AwaitsAcceptance: true };

        if (waits && node.Spec is { } spec)
        {
            yield return new PieceActionViewModel("Accept spec…", () => actions.AcceptSpec(spec),
                toolTip: ProjectViewModel.AcceptSpecToolTip);
        }

        if (stage?.Node.RecordFile is { } record)
        {
            yield return new PieceActionViewModel($"Open {stage.Node.RecordName}", () => actions.OpenPath(record), toolTip: record);
        }
        else
        {
            yield return ProjectViewModel.OpenRecords(node.Piece, actions);
        }

        if (stage is null && node.AddToProject is { } add)
        {
            yield return add;
        }
    }

    private static string ObjectiveHeadingOf(PieceNodeViewModel node) =>
        node.Objective is { } objective ? $"Objective {objective.Number}: {objective.Title}" : string.Empty;

    private static IReadOnlyList<CriterionViewModel> CriteriaOf(PieceNodeViewModel node, ProjectViewEdits? edits) =>
        node.Objective is { } objective ? CriterionViewModel.Of(objective, edits) : [];

    private static ICommand? EditOf(PieceNodeViewModel node, ProjectViewEdits? edits) =>
        node.Objective is { } objective && edits is not null
            ? new RelayCommand(_ => edits.EditObjective(objective))
            : null;
}
