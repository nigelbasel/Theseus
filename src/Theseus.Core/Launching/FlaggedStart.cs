using Theseus.Core.Domain;

namespace Theseus.Core.Launching;

/// <summary>
/// What Theseus asks before starting a workstream marked paused or blocked (Nigel, 2026-10-30):
/// whether the mark still holds. Clear marks and start clears it and starts; Start only the others leaves it marked and
/// unstarted. The buttons say what they do, so the question names the cards and nothing else (baseline A3, G9).
/// </summary>
public static class FlaggedStart
{
    /// <summary>The workstreams among these that are marked paused or blocked.</summary>
    public static IReadOnlyList<Workstream> Among(IReadOnlyList<Workstream> workstreams) =>
        [.. workstreams.Where(workstream => workstream.IsPaused || workstream.IsBlocked)];

    /// <summary>The same workstreams with both marks cleared, so the start goes ahead.</summary>
    public static Workstream Cleared(Workstream workstream) =>
        workstream with { IsPaused = false, IsBlocked = false };

    /// <summary>The question, naming each marked workstream and what it is marked as.</summary>
    /// <param name="flagged">The marked workstreams about to be started.</param>
    /// <param name="others">How many unmarked ones are being started with them.</param>
    public static string Question(IReadOnlyList<Workstream> flagged, int others)
    {
        ArgumentNullException.ThrowIfNull(flagged);

        IEnumerable<string> lines = flagged.Select(workstream => $"  •  {workstream.Title} is marked {Marks(workstream)}.");
        string first = (flagged.Count, others) switch
        {
            (1, 0) => "The card you are starting is marked paused or blocked:",
            (_, 0) => $"The {flagged.Count} cards you are starting are marked paused or blocked:",
            _ => $"{flagged.Count} of the {flagged.Count + others} cards you are starting {(flagged.Count == 1 ? "is" : "are")} marked paused or blocked:",
        };

        return $"{first}\n\n{string.Join("\n", lines)}";
    }

    private static string Marks(Workstream workstream) => (workstream.IsPaused, workstream.IsBlocked) switch
    {
        (true, true) => "paused and blocked",
        (true, false) => "paused",
        _ => "blocked",
    };
}
