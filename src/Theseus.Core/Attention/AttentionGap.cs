using System.Text.Json.Serialization;

namespace Theseus.Core.Attention;

/// <summary>
/// A stretch in which nothing was measured (7.3): the machine asleep or the sampler stalled, or Theseus not running. Kept
/// in <c>attention-gaps.jsonl</c>, so "not measured" is told apart from "nothing happened".
/// </summary>
/// <param name="From">The last moment measured.</param>
/// <param name="To">The next moment measured.</param>
/// <param name="Reason">Why: <see cref="SamplerGap"/>, <see cref="NotRunning"/> or <see cref="Crashed"/>.</param>
public sealed record AttentionGap(DateTimeOffset From, DateTimeOffset To, string Reason)
{
    /// <summary>Samples stopped while Theseus ran: the machine slept, or the sampler stalled.</summary>
    public const string SamplerGap = "sampler gap";

    /// <summary>Theseus was closed cleanly, and later started again.</summary>
    public const string NotRunning = "Theseus not running";

    /// <summary>Theseus ended without closing what it had open: crashed, killed, or the machine went down.</summary>
    public const string Crashed = "Theseus ended without closing";

    /// <summary>How long nothing was measured.</summary>
    [JsonIgnore]
    public TimeSpan Length => To - From;

    /// <summary>The machine's UTC offset when it was written, as <c>+02:00</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Offset { get; init; }
}
