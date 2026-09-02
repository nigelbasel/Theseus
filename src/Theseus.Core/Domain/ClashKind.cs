namespace Theseus.Core.Domain;

/// <summary>What two workstreams have turned out to share.</summary>
public enum ClashKind
{
    /// <summary>
    /// One handover document between them. The dangerous one: two sessions told to write the same
    /// file will overwrite each other, and neither knows the other is there.
    /// </summary>
    Handoff,

    /// <summary>
    /// One Jira issue between them. Not dangerous and sometimes deliberate, but worth knowing before
    /// two sessions do the same work.
    /// </summary>
    JiraKey,
}
