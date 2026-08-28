using Theseus.Core.Domain;

namespace Theseus.Core.Stopping;

/// <summary>
/// Settles where a workstream's handover notes live, once, and then leaves it alone.
///
/// Until it is settled the path is worked out afresh every time it is needed - at launch, and again
/// at every stop - from a search of the working directory and a name proposed if the search finds
/// nothing. Two of those workings-out can disagree, and when they do a session writes its notes in
/// one place and the next morning is sent to another, where it finds nothing and concludes the work
/// was never handed over. Recording the answer the first time is what makes the path a contract
/// rather than a coincidence: the session is told it at launch, so whoever stops it, in whatever
/// words, its notes go where the next session will look.
/// </summary>
public static class HandoffPin
{
    /// <summary>
    /// The workstream with its handover document settled. Returns the same instance, unchanged, when
    /// it already has one - a path is chosen once and never revised underneath a running session.
    /// </summary>
    public static Workstream Pin(Workstream workstream)
    {
        ArgumentNullException.ThrowIfNull(workstream);

        return string.IsNullOrWhiteSpace(workstream.StatusDocumentPath)
            ? workstream with { StatusDocumentPath = HandoffPath.For(workstream) }
            : workstream;
    }
}
