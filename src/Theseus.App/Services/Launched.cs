namespace Theseus.App.Services;

/// <summary>What a redeemed ticket belongs to.</summary>
/// <param name="WorkstreamKey">The workstream whose card the shim's reports move.</param>
/// <param name="RunId">The run in the log those reports end.</param>
/// <param name="Ended">Whether an ending has already been recorded for it.</param>
public sealed record Launched(string WorkstreamKey, Guid RunId, bool Ended = false);
