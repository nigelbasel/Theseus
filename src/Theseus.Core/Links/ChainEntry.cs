using Theseus.Core.Finding;

namespace Theseus.Core.Links;

/// <summary>One card in the Chains tree, as the panel's search and filters read it.</summary>
/// <param name="Card">The fields the search reads.</param>
/// <param name="Facts">What decides its state and the filters.</param>
/// <param name="Children">The cards it hands over to.</param>
public sealed record ChainEntry(SearchableCard Card, ChainRowFacts Facts, IReadOnlyList<ChainEntry> Children)
{
    /// <summary>The card's key.</summary>
    public string Key => Card.Key;
}
