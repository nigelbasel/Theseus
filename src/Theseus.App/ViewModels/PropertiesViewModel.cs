using System.Windows.Input;
using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// The Properties pane of the project view (1.1d AC 34): a hint while nothing is selected; for a piece of work or one of
/// its stages, its state in words, label and value rows, its links, what can be done, and its objective's criteria.
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

    /// <summary>One line per link, with its state in words.</summary>
    public IReadOnlyList<string> Links { get; private init; } = [];

    /// <summary>Whether it has a Links section.</summary>
    public bool HasLinks => Links.Count > 0;

    /// <summary>Accept spec… while it waits, then Open &lt;record&gt; or Open records.</summary>
    public IReadOnlyList<PieceActionViewModel> Buttons { get; private init; } = [];

    /// <summary>"Objective 1.1: Checks shown on cards", or empty for a piece no project file lists.</summary>
    public string ObjectiveHeading { get; private init; } = string.Empty;

    /// <summary>Whether it has an Objective section.</summary>
    public bool HasObjective => ObjectiveHeading.Length > 0;

    /// <summary>The objective's criteria, ticked in the project file.</summary>
    public IReadOnlyList<CriterionViewModel> Criteria { get; private init; } = [];

    /// <summary>Edit objective…, or null when the view cannot edit.</summary>
    public ICommand? EditObjectiveCommand { get; private init; }

    /// <summary>Whether Edit objective… is offered.</summary>
    public bool CanEditObjective => EditObjectiveCommand is not null;

    /// <summary>The pane for a piece of work.</summary>
    internal static PropertiesViewModel ForPiece(PieceNodeViewModel node, ProjectViewActions actions, ProjectViewEdits? edits)
    {
        var piece = node.Piece;
        var status = node.Status;

        return new PropertiesViewModel(piece.Name)
        {
            StatusText = status.Text,
            State = status.State,
            Rows =
            [
                new("Where", node.StatusText),
                new("Worker", piece.Workstream),
                new("Kind", piece.KindWord),
                new("Objective", ObjectiveText(piece)),
                new("Records", piece.RecordsFolder ?? "no records folder in the project"),
                new("Evidence", node.Evidence),
                new("Stages", string.Join("  ", node.Stages.Select(stage => stage.Label))),
            ],
            Links = node.LinkLines,
            Buttons = [.. ButtonsOf(node, null, actions)],
            ObjectiveHeading = ObjectiveHeadingOf(node),
            Criteria = CriteriaOf(node, edits),
            EditObjectiveCommand = EditOf(node, edits),
        };
    }

    /// <summary>The pane for one stage of a piece of work.</summary>
    internal static PropertiesViewModel ForStage(StageNodeViewModel stage, ProjectViewActions actions, ProjectViewEdits? edits)
    {
        var node = stage.Piece;
        var piece = node.Piece;
        var rows = new List<PropertyRowViewModel>
        {
            new("Stage", stage.Name),
            new("State", $"{stage.Mark} {stage.Word}"),
            new("Record", stage.Node.RecordFile ?? "not written yet"),
            new("Piece", piece.Name),
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

        return new PropertiesViewModel($"{stage.Name} of {piece.Name}")
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
    }

    private static string ObjectiveHeadingOf(PieceNodeViewModel node) =>
        node.Objective is { } objective ? $"Objective {objective.Number}: {objective.Title}" : string.Empty;

    private static IReadOnlyList<CriterionViewModel> CriteriaOf(PieceNodeViewModel node, ProjectViewEdits? edits) =>
        node.Objective is not { } objective
            ? []
            :
            [
                .. objective.Criteria.Select(criterion => new CriterionViewModel(criterion.Text, criterion.IsTicked,
                    edits is null ? null : new RelayCommand(_ => edits.TickCriterion(criterion.Line, !criterion.IsTicked)))),
            ];

    private static ICommand? EditOf(PieceNodeViewModel node, ProjectViewEdits? edits) =>
        node.Objective is { } objective && edits is not null
            ? new RelayCommand(_ => edits.EditObjective(objective.Number))
            : null;
}
