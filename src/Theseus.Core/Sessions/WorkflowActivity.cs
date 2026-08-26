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
/// <param name="HasEnded">Whether the run wrote the summary it writes when it finishes.</param>
public sealed record WorkflowActivity(
    string RunId,
    int Started,
    int Finished,
    DateTimeOffset LastActivityUtc,
    bool HasEnded)
{
    /// <summary>How long a run may go without a journal line before it is presumed over.</summary>
    public static readonly TimeSpan Patience = TimeSpan.FromMinutes(15);

    /// <summary>Whether the run has agents outstanding. True of a killed run for ever after.</summary>
    public bool InFlight => Started > Finished;

    /// <summary>
    /// Whether this workflow is actually going, rather than merely having agents it never accounted
    /// for. A run killed with one agent outstanding leaves that gap in its journal permanently, so
    /// the gap alone says nothing: what settles it is the summary a finished run writes, and failing
    /// that a journal which has stopped growing.
    /// </summary>
    public bool IsRunning(DateTimeOffset now) =>
        InFlight && !HasEnded && Silence(now) < Patience;

    /// <summary>
    /// How long the run has been silent. A run whose agents are all outstanding looks identical to
    /// one killed mid-flight; the journal standing still is what tells them apart.
    /// </summary>
    public TimeSpan Silence(DateTimeOffset now) => now - LastActivityUtc;

    /// <summary>The line a card shows.</summary>
    public string Describe => $"workflow · {Finished}/{Started} agents";
}
