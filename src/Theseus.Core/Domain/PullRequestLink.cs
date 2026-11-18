namespace Theseus.Core.Domain;

/// <summary>A pull request a workstream mentions, and where to open it.</summary>
/// <param name="Number">The pull request number.</param>
/// <param name="Url">Its address, or null when the host could not be addressed.</param>
public sealed record PullRequestLink(int Number, string? Url)
{
    /// <summary>
    /// Why there is no link, when the card's repos have more than one origin and the number could belong to any of
    /// them (card-branches spec AC 11).
    /// </summary>
    public string? Note { get; init; }
}
