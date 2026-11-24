namespace Theseus.Core.CiStatus;

/// <summary>
/// What a card says about a repository's CI, worst first: a card covering several repositories shows the worst of
/// them (pr-ci-status spec AC 6). Only <see cref="Passed"/> is good news, and only GitHub's answer makes it.
/// </summary>
public enum CiState
{
    /// <summary>The newest run failed or timed out.</summary>
    Failed,

    /// <summary>The newest run is queued or running.</summary>
    Running,

    /// <summary>The newest run ended without passing or failing: cancelled, skipped, neutral and the like.</summary>
    Ended,

    /// <summary>Theseus does not know: not read yet, the limit used up, not shown, a failed read, an unreadable branch.</summary>
    Unknown,

    /// <summary>The repository is on a host Theseus does not read.</summary>
    NotRead,

    /// <summary>GitHub was read and has no run on the branch among the newest it gave.</summary>
    NoneFound,

    /// <summary>The newest run passed.</summary>
    Passed,
}
