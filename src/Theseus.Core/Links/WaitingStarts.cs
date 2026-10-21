using Theseus.Core.Domain;

namespace Theseus.Core.Links;

/// <summary>
/// Which of the cards a Start was asked for are still waiting on an input. They are held back and
/// asked about rather than launched: a fresh session with nothing to read costs 42k-71k tokens to open
/// and can only go looking for a document that does not exist yet (Nigel, 2026-10-21).
/// </summary>
public static class WaitingStarts
{
    /// <summary>The workstreams among these that wait on an input not yet accepted.</summary>
    public static IReadOnlyList<Workstream> Among(IReadOnlyList<Workstream> workstreams, LinkStatus status)
    {
        ArgumentNullException.ThrowIfNull(workstreams);
        ArgumentNullException.ThrowIfNull(status);

        return [.. workstreams.Where(workstream => status.ReadinessOf(workstream.Key) == CardReadiness.Waiting)];
    }

    /// <summary>What the strip says about the held cards.</summary>
    public static string Summary(IReadOnlyList<Workstream> held, LinkStatus status)
    {
        ArgumentNullException.ThrowIfNull(held);
        ArgumentNullException.ThrowIfNull(status);

        if (held.Count == 0)
        {
            return string.Empty;
        }

        if (held.Count == 1)
        {
            return $"{held[0].Title} is waiting on an input ({On(held[0], status)}).";
        }

        return $"{held.Count} cards are waiting on inputs: "
               + $"{string.Join(", ", held.Select(workstream => $"{workstream.Title} ({On(workstream, status)})"))}.";
    }

    private static string On(Workstream workstream, LinkStatus status) =>
        "on " + string.Join(", ", status.WaitingOn(workstream.Key).Select(key => status.Card(key)?.Title ?? key));
}
