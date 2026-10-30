using Theseus.Core.Domain;

namespace Theseus.Core.Launching;

/// <summary>
/// What Theseus asks before starting a workstream marked paused or blocked (Nigel, 2026-10-30):
/// whether the mark still holds. Yes clears it and starts; No leaves it marked and unstarted.
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

        var lines = flagged.Select(workstream => $"  •  {workstream.Title} is marked {Marks(workstream)}.");
        var one = flagged.Count == 1;
        var question = one
            ? "Is it no longer paused or blocked?"
            : "Are these no longer paused or blocked?";
        var yes = one ? "Yes clears the mark and starts it." : "Yes clears the marks and starts them.";
        var no = others == 0
            ? (one ? "No leaves it marked and does not start it." : "No leaves them marked and starts nothing.")
            : $"No leaves {(one ? "it" : "them")} marked and starts only the other {others}.";

        return string.Join("\n", lines) + $"\n\n{question}\n\n{yes}\n{no}";
    }

    private static string Marks(Workstream workstream) => (workstream.IsPaused, workstream.IsBlocked) switch
    {
        (true, true) => "paused and blocked",
        (true, false) => "paused",
        _ => "blocked",
    };
}
