namespace Theseus.Core.Domain;

/// <summary>
/// Turns a single row of mixed lanes into two axes.
///
/// A board configured before the grid existed holds lanes that are a mixed metaphor: "New" and
/// "Done" are stages of work, while "Research", "Dev" and "Side Project" are kinds of it. A card
/// cannot be both in research and done, because on one axis those are the same place — which is the
/// confusion the grid resolves.
///
/// This runs once, when a board has lanes but no areas, and produces both lists from the one it had.
/// It is deliberately conservative: every existing lane becomes either a stage or an area, nothing is
/// invented beyond the stage the areas fold into, and no card is left without a place.
/// </summary>
public static class BoardSplit
{
    /// <summary>The stage that lanes naming a kind of work are folded into.</summary>
    public const string InProgress = "In progress";

    /// <summary>
    /// Lane names that are stages rather than kinds of work. Matched loosely, so "in-flight" and
    /// "In flight" are the same. Anything not here is taken to be an area, which is the safe way
    /// round: a stage mistaken for an area becomes a row nobody wanted and is renamed in a moment,
    /// where an area mistaken for a stage silently merges work that was being kept apart.
    /// </summary>
    private static readonly string[] KnownStages =
    [
        "New", "Today", "To do", "Backlog", "Next",
        "In progress", "In flight", "Doing", "Started",
        "Paused", "Parked", "On hold", "Blocked", "Waiting",
        "Review", "In review", "Done", "Finished", "Complete", "Closed",
    ];

    /// <summary>Whether a board needs splitting: it has lanes, and no areas yet.</summary>
    public static bool IsNeeded(IReadOnlyList<string>? lanes, IReadOnlyList<string>? areas) =>
        lanes is { Count: > 0 } && areas is null or { Count: 0 };

    /// <summary>The two axes derived from one row of lanes.</summary>
    public static (IReadOnlyList<string> Stages, IReadOnlyList<string> Areas) Split(
        IReadOnlyList<string>? lanes)
    {
        var kept = BoardLanes.Sanitise(lanes);

        var stages = kept.Where(IsStage).ToList();
        var areas = kept.Where(lane => !IsStage(lane)).Select(Base).Distinct(LooseName.Comparer).ToList();

        // A board of nothing but categories still needs somewhere for work to be in progress, and one
        // of nothing but stages needs a row to put its cards in.
        //
        // What counts as "in progress" is a set rather than a word. A board already calling it "In
        // flight" does not want a second column called "In progress" beside it, and comparing against
        // the one name would give it one.
        if (stages.Count == 0)
        {
            stages = ["New", InProgress, "Done"];
        }
        else if (!stages.Any(HasWorkUnderWay))
        {
            stages.Insert(Math.Min(1, stages.Count), InProgress);
        }

        return (stages, areas.Count > 0 ? areas : BoardAreas.Default);
    }

    /// <summary>
    /// Where one old lane's cards now belong. A lane that was a stage keeps its column and takes the
    /// first area; one that was a kind of work becomes an area, and its cards are in progress.
    /// </summary>
    public static (string Stage, string Area) Place(string lane, IReadOnlyList<string> areas)
    {
        ArgumentNullException.ThrowIfNull(areas);

        return IsStage(lane)
            ? (lane, areas.Count > 0 ? areas[0] : BoardAreas.Unassigned)
            : (InProgress, Base(lane));
    }

    /// <summary>The names a board might already be using for work that is under way.</summary>
    private static readonly string[] UnderWay =
        ["In progress", "In flight", "Doing", "Started", "Active", "WIP", "Review", "In review"];

    private static bool IsStage(string lane) =>
        KnownStages.Any(stage => LooseName.Same(stage, lane));

    private static bool HasWorkUnderWay(string stage) =>
        UnderWay.Any(name => LooseName.Same(name, stage));

    /// <summary>
    /// The area a lane names, with any trailing qualifier dropped: "Dev - Primary" and "Dev -
    /// Secondary" were never two kinds of work, and keeping them apart would carry the confusion
    /// across rather than resolve it.
    /// </summary>
    private static string Base(string lane)
    {
        var cut = lane.IndexOf('-', StringComparison.Ordinal);

        return cut > 0 ? lane[..cut].Trim() : lane.Trim();
    }
}
