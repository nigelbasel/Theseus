using Theseus.Core.Links;

namespace Theseus.Core.Projects;

/// <summary>
/// A clear-up of verifier cards as Theseus's settings keep it, so a stop that lands after a restart is still followed by
/// its archive, and Undo still knows what to put back (review of item 5, M3).
/// </summary>
/// <param name="Project">The project it cleared up.</param>
/// <param name="Cards">Every card it takes away.</param>
/// <param name="Archived">Those archived so far.</param>
/// <param name="Before">The cards' report-back links as they were before it retired them, for Undo.</param>
public sealed record VerifierClearUpRecord(
    string Project,
    IReadOnlyList<string> Cards,
    IReadOnlyList<string> Archived,
    IReadOnlyList<Link> Before);
