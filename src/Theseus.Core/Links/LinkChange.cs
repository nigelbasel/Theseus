namespace Theseus.Core.Links;

/// <summary>What became of asking the book for a change: the link as it now stands, or why not.</summary>
/// <param name="Link">The link after the change, when it was made.</param>
/// <param name="Refusal">One sentence saying why nothing changed, when it was refused.</param>
public sealed record LinkChange(Link? Link, string? Refusal)
{
    /// <summary>A change that was made.</summary>
    public static LinkChange Made(Link link) => new(link, null);

    /// <summary>A change that was refused.</summary>
    public static LinkChange Refused(string refusal) => new(null, refusal);
}
