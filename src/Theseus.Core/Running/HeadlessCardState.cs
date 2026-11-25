namespace Theseus.Core.Running;

/// <summary>What a card's face shows of its headless run: going, queued, or the last one ended.</summary>
/// <param name="IsRunning">Whether a run is going.</param>
/// <param name="QueuePosition">Its place in the queue, or null.</param>
/// <param name="StartedUtc">When the run going started.</param>
/// <param name="LastLine">The run's latest line.</param>
/// <param name="Tokens">Its tokens so far.</param>
/// <param name="CostUsd">Its cost so far, known at the end of each turn.</param>
/// <param name="Last">The card's last ended run, or null.</param>
/// <param name="LogPath">The file the run going, or the last one, wrote its output to.</param>
public sealed record HeadlessCardState(
    bool IsRunning,
    int? QueuePosition,
    DateTimeOffset? StartedUtc,
    string? LastLine,
    RunTokens Tokens,
    decimal? CostUsd,
    RunRecord? Last,
    string? LogPath);
