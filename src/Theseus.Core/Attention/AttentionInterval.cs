using System.Text.Json.Serialization;

namespace Theseus.Core.Attention;

/// <summary>A stretch of time on one clock, for one workstream.</summary>
/// <param name="Kind">Attended or machine.</param>
/// <param name="Workstream">
/// The workstream's key, or <c>unmatched: </c> and a tab title for a terminal tab no workstream is
/// named after - kept rather than dropped, because Phase 1 is about whether the totals are believable.
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
}
