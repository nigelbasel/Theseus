namespace Theseus.Core.Sessions;

/// <summary>
/// A workflow a session is running now, as its journal reports it. The journal is appended while the
/// run goes, unlike the summary written when it ends, so this is the one account of a workflow that
/// exists while it matters.
/// </summary>
/// <param name="RunId">The run's identifier.</param>
/// <param name="Started">How many agents have been dispatched.</param>
/// <param name="Finished">How many have returned a result.</param>
/// <param name="LastActivityUtc">When the journal last grew.</param>
public sealed record WorkflowActivity(string RunId, int Started, int Finished, DateTimeOffset LastActivityUtc)
{
    /// <summary>Whether the run has anything left outstanding.</summary>
    public bool InFlight => Started > Finished;

    /// <summary>
    /// How long the run has been silent. A run whose agents are all outstanding looks identical to
    /// one killed mid-flight; the journal standing still is what tells them apart.
    /// </summary>
    public TimeSpan Silence(DateTimeOffset now) => now - LastActivityUtc;

    /// <summary>The line a card shows.</summary>
    public string Describe => $"workflow · {Finished}/{Started} agents";
}
