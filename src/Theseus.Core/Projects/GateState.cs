namespace Theseus.Core.Projects;

/// <summary>Where the gate after a stage stands (spec AC 7).</summary>
public enum GateState
{
    /// <summary>The stage has not been reached.</summary>
    NotReached,

    /// <summary>Reached and not passed yet: for the spec, waiting on the owner.</summary>
    Waiting,

    /// <summary>Passed.</summary>
    Passed,

    /// <summary>The owner accepted the spec, and it has changed since; waiting on him again.</summary>
    ChangedSinceAccepted,

    /// <summary>At or past verify with no evidence file or no result block: never shown as passed.</summary>
    NoEvidence,

    /// <summary>Not reached, though a later stage was: a bug whose fix needed no rule, or an artefact never written.</summary>
    Skipped,

    /// <summary>The primary's pull request into the trunk, until objective 1.2 reads pull request status.</summary>
    NotReadYet,
}
