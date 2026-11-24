namespace Theseus.Core.CiStatus;

/// <summary>What a card shows about its repositories' pull requests and CI.</summary>
/// <param name="ButtonText">The drop-down's button: "PR 41 · ✓ CI passed", "3 repos · ✗ CI failed".</param>
/// <param name="State">The worst CI state among the card's repositories.</param>
/// <param name="Rows">The drop-down's lines, per repository.</param>
public sealed record CardStatus(string ButtonText, CiState State, IReadOnlyList<CardStatusRow> Rows)
{
    /// <summary>A card with no repository that has a remote: it shows no status (spec D5).</summary>
    public static readonly CardStatus None = new(string.Empty, CiState.NotRead, []);

    /// <summary>Whether there is anything to show.</summary>
    public bool IsShown => Rows.Count > 0;
}
