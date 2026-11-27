using Theseus.Core.Domain;

namespace Theseus.Core.Attention;

/// <summary>What a sample needs beyond the terminal and the session registry (7.3).</summary>
/// <param name="Theseus">What Theseus's own window in front showed, or null when it was not in front.</param>
/// <param name="SessionCards">Every session the run log knows, with the card it ran for.</param>
/// <param name="StampOf">What a card is part of now, or null to stamp nothing.</param>
/// <param name="Zone">The zone whose offset is stamped, or null to stamp none.</param>
public sealed record AttentionContext(
    TheseusFocus? Theseus,
    IReadOnlyDictionary<string, string> SessionCards,
    Func<Workstream, AttentionStamp?>? StampOf,
    TimeZoneInfo? Zone)
{
    /// <summary>The sample as it was before 7.3: the terminal only, a card's last session only, nothing stamped.</summary>
    public static AttentionContext None { get; } =
        new(null, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), null, null);
}
