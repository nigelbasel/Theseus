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
    IReadOnlyList<Link> Before)
{
    /// <summary>When it was pressed; Undo lasts a day from then.</summary>
    public DateTimeOffset AtUtc { get; init; }

    /// <summary>The project's round when it was pressed; Undo lasts until the next round starts.</summary>
    public int Round { get; init; }

    /// <summary>The rows it took out of project.md, in the order taken, for Undo.</summary>
    public IReadOnlyList<ClearedRow> Rows { get; init; } = [];

    /// <summary>
    /// This, dated now if it was kept before clear-ups carried a time, so the upgrade does not lapse it at once; a dated one
    /// is returned as it is.
    /// </summary>
    public VerifierClearUpRecord Dated(DateTimeOffset now, int round) =>
        AtUtc == default ? this with { AtUtc = now.ToUniversalTime(), Round = round } : this;
}
