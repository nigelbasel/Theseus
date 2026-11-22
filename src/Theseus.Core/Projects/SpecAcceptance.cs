namespace Theseus.Core.Projects;

/// <summary>
/// The owner's acceptance of a workstream's spec (a bug's rule), recorded on the board in Theseus's own store, never in
/// a file a session can write (spec AC 7).
/// </summary>
/// <param name="Project">The project's name.</param>
/// <param name="Slug">The workstream's slug.</param>
/// <param name="ContentHash">The accepted text's <see cref="SpecIdentity"/>, which a later spec is compared with.</param>
/// <param name="AcceptedUtc">When he accepted it.</param>
public sealed record SpecAcceptance(string Project, string Slug, string ContentHash, DateTimeOffset AcceptedUtc)
{
    /// <summary>Accepted on the board, the owner's press there.</summary>
    public const string OnTheBoard = "on the board";

    /// <summary>Who accepted it (1.1e AC 29): the signed-in user; null for a record from before 1.1e.</summary>
    public string? By { get; init; }

    /// <summary>
    /// How it was accepted (1.1e AC 29): <see cref="OnTheBoard"/>, or <see cref="SaidIn"/> a card's tab and confirmed on
    /// the board; null for a record from before 1.1e, which was on the board.
    /// </summary>
    public string? How { get; init; }

    /// <summary>Said in a card's tab, reported by its session and confirmed on the board.</summary>
    public static string SaidIn(string cardTitle) => $"said in {cardTitle}'s tab, confirmed on the board";

    /// <summary>The record as Properties and <c>gates()</c> give it: when, by whom, how and which version.</summary>
    public string Describe(TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var when = TimeZoneInfo.ConvertTime(AcceptedUtc, zone).ToString("d MMM yyyy, HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        var version = ContentHash.Length >= 7 ? ContentHash[..7] : ContentHash;

        return $"version {version}, {when}{(By is { Length: > 0 } by ? $", by {by}" : string.Empty)}, {How ?? OnTheBoard}";
    }
}
