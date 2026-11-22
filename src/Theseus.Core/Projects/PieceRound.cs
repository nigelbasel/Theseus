namespace Theseus.Core.Projects;

/// <summary>One round of a project's tree: the round and the roots of its pieces, in file order (1.1f C10, C14).</summary>
/// <param name="Round">The round.</param>
/// <param name="Roots">Its pieces with no parent in the round.</param>
public sealed record PieceRound(ProjectRound Round, IReadOnlyList<PieceNode> Roots)
{
    /// <summary>Every piece in the round, depth first.</summary>
    public IEnumerable<PieceNode> All => Roots.SelectMany(Flatten);

    private static IEnumerable<PieceNode> Flatten(PieceNode node) => node.Children.SelectMany(Flatten).Prepend(node);
}
