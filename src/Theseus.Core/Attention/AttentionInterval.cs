using System.Text.Json.Serialization;

namespace Theseus.Core.Attention;

/// <summary>A stretch of time on one clock, for one workstream.</summary>
/// <param name="Kind">Attended or machine.</param>
/// <param name="Workstream">
/// The workstream's key; <c>unmatched: </c> and a tab title for a terminal tab no workstream is named after, kept rather
/// than dropped; or <c>project: </c> and a project's name for time at that project's view in Theseus (7.3).
/// </param>
/// <param name="Start">When it began.</param>
/// <param name="End">When it ended.</param>
public sealed record AttentionInterval(
    AttentionKind Kind,
    string Workstream,
    DateTimeOffset Start,
    DateTimeOffset End)
{
    /// <summary>How long it lasted.</summary>
    [JsonIgnore]
    public TimeSpan Length => End - Start;

    /// <summary>
    /// The session it was measured on: the busy session for machine time, the card's session for attended time. Null on
    /// lines written before 7.3, and for time at a project's view.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Session { get; init; }

    /// <summary>The card's project when the line was written, so a later move or re-slug cannot re-file it (7.3).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Project { get; init; }

    /// <summary>The stable id of the piece of work the card belonged to when the line was written (7.3).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PieceId { get; init; }

    /// <summary>The stage the card's session last reported with now_at when the line was written.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Stage { get; init; }

    /// <summary>
    /// The machine's UTC offset in force when the line was written, as <c>+02:00</c>, so its day stays where it was after
    /// the owner travels between zones (7.3, decision 17). Null on lines written before 7.3.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Offset { get; init; }

    /// <summary>
    /// Where it was seen: <c>terminal</c> or <c>theseus</c> for attended time, <c>ledger</c> for a headless run merged in
    /// when read. Null on lines written before 7.3 and on sampled machine time.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Source { get; init; }
}
