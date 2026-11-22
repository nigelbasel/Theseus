namespace Theseus.Core.Finding;

/// <summary>
/// How many tags a card shows under its title (card-search-and-tags item 11): at most three, then "+2" with the rest in
/// its tooltip, so a well-tagged card does not grow taller than its neighbours.
/// </summary>
public static class TagDisplay
{
    /// <summary>How many show.</summary>
    public const int Limit = 3;

    /// <summary>The tags shown on the card.</summary>
    public static IReadOnlyList<string> Visible(IReadOnlyList<string>? tags) => tags is null ? [] : [.. tags.Take(Limit)];

    /// <summary>"+2" for the tags not shown, or empty when all show.</summary>
    public static string More(IReadOnlyList<string>? tags) => tags is not null && tags.Count > Limit ? $"+{tags.Count - Limit}" : string.Empty;

    /// <summary>The tags not shown, one per line, for the "+2" label's tooltip.</summary>
    public static string Rest(IReadOnlyList<string>? tags) =>
        tags is null ? string.Empty : string.Join(Environment.NewLine, tags.Skip(Limit).Select(tag => "#" + tag));
}
