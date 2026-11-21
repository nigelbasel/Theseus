namespace Theseus.Core.Projects;

/// <summary>
/// A project's pieces of work under its phases and objectives (1.1d AC 46, 47; Nigel's decision 5, 2026-11-21): every
/// phase and every objective, even one nothing works on yet, and each piece under its own objective. No connector is
/// drawn across headings, so a hand-over and a move to the next piece become marks on the boxes.
/// </summary>
public static class ObjectiveGrouping
{
    /// <summary>Every phase of the file in order, each objective with its pieces in file order.</summary>
    public static IReadOnlyList<PhaseGroup> Phases(ProjectFile file, IReadOnlyList<PieceOfWork> pieces)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(pieces);

        return
        [
            .. file.Phases.Select(phase => new PhaseGroup(phase,
            [
                .. phase.Objectives.Select(objective => new ObjectiveGroup(objective,
                    [.. pieces.Where(piece => string.Equals(piece.ObjectiveNumber, objective.Number, StringComparison.Ordinal))])),
            ])),
        ];
    }

    /// <summary>The pieces no project file lists (1.1d AC 13), or null when there are none.</summary>
    public static ObjectiveGroup? Unlisted(IReadOnlyList<PieceOfWork> pieces)
    {
        ArgumentNullException.ThrowIfNull(pieces);

        var unlisted = pieces.Where(piece => piece.ObjectiveNumber is null).ToList();

        return unlisted.Count == 0 ? null : new ObjectiveGroup(null, unlisted);
    }

    /// <summary>
    /// The marks each piece carries in place of the Flow's connectors: "← handed on from", "→ handed on to" and, where
    /// the next piece of its workstream is under another objective, "┈ worked on after" and "┈ then worked on". Every
    /// hand-over is marked, since no connector shows it here; then worked on within one objective is not, since file
    /// order already shows it.
    /// </summary>
    public static IReadOnlyDictionary<PieceOfWork, IReadOnlyList<string>> Marks(PieceTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);

        var marks = tree.All.ToDictionary(node => node.Piece, _ => new List<string>());

        foreach (var node in tree.All)
        {
            foreach (var child in node.Children)
            {
                marks[child.Piece].Add($"← handed on from {node.Piece.Name}");
            }

            if (node.Children.Count > 0)
            {
                marks[node.Piece].Add($"→ handed on to {string.Join(", ", node.Children.Select(child => child.Piece.Name))}");
            }
        }

        foreach (var link in tree.ThenWorkedOn.Where(link =>
                     !string.Equals(link.From.ObjectiveNumber, link.To.ObjectiveNumber, StringComparison.Ordinal)))
        {
            marks[link.To].Add($"┈ worked on after {link.From.Name}");
        }

        foreach (var link in tree.ThenWorkedOn.Where(link =>
                     !string.Equals(link.From.ObjectiveNumber, link.To.ObjectiveNumber, StringComparison.Ordinal)))
        {
            marks[link.From].Add($"┈ then worked on {link.To.Name}");
        }

        return marks.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value);
    }
}
