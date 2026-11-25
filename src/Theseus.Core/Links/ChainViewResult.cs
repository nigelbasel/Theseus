namespace Theseus.Core.Links;

/// <summary>What the Chains panel shows for one search and set of filters, and what each filter hides on its own.</summary>
/// <param name="Visibility">Each row's visibility, by card key.</param>
/// <param name="Shown">How many rows are on screen, context rows included.</param>
/// <param name="Total">How many rows there are.</param>
/// <param name="HiddenByActiveOnly">How many rows Active only would hide on its own.</param>
/// <param name="HiddenByWaitingOnMe">How many rows Waiting on me would hide on its own.</param>
/// <param name="HiddenByHideCompleted">How many rows Hide completed would hide on its own.</param>
/// <param name="Unfold">The rows a search unfolds so that its matches can be seen.</param>
public sealed record ChainViewResult(
    IReadOnlyDictionary<string, ChainVisibility> Visibility,
    int Shown,
    int Total,
    int HiddenByActiveOnly,
    int HiddenByWaitingOnMe,
    int HiddenByHideCompleted,
    IReadOnlyCollection<string> Unfold)
{
    /// <summary>A row's visibility; a key it does not know is hidden.</summary>
    public ChainVisibility Of(string key) => Visibility.TryGetValue(key, out var visibility) ? visibility : ChainVisibility.Hidden;
}
