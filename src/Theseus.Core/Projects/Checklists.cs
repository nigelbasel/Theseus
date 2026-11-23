namespace Theseus.Core.Projects;

/// <summary>The checklists Theseus asks before acting on several cards at once.</summary>
public static class Checklists
{
    /// <summary>Clear up verifiers…: one row per card that should not be kept, saying what it checks, why, and whether it runs.</summary>
    /// <param name="wrong">The cards.</param>
    /// <param name="titleOf">A card's title from its key, for the card each checks.</param>
    public static Checklist ClearUp(IReadOnlyList<WrongVerifier> wrong, Func<string, string> titleOf)
    {
        ArgumentNullException.ThrowIfNull(wrong);
        ArgumentNullException.ThrowIfNull(titleOf);

        var running = wrong.Where(card => card.IsRunning).Select(card => card.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new Checklist("Clear up cards",
            "Each card ticked is stopped if it is running, then archived; every report it filed is rejected, a confirmed one too, "
            + "so nothing it said counts as met. The newest correct verifier of each build is kept. You can undo this.",
            "Clear up",
            [
                .. wrong.Select(card => new ChecklistRow(card.Key, card.Title,
                    $"{(card.IsWriter ? $"Writes scenarios for {card.Subject}" : $"Checks {titleOf(card.Subject)}")}"
                    // The card's key as well: copies share their title, and the rows must be told apart.
                    + $" · {(card.IsRunning ? "running" : "not running")} · card {card.Key}",
                    Sentence(card.Why))),
            ],
            ticked =>
            {
                var stop = ticked.Count(running.Contains);

                return ticked.Count == 0 ? "Nothing ticked"
                    : stop == 0 ? $"Archive {ticked.Count}"
                    : $"Stop {stop} and archive {ticked.Count}";
            });
    }

    /// <summary>Start the checks…: one row per build to be checked, and the builds left out, with why, above them.</summary>
    /// <param name="builds">The builds, by card key and title.</param>
    /// <param name="refused">The builds that cannot be checked now, by title, with why.</param>
    public static Checklist StartChecks(IReadOnlyList<(string Key, string Title)> builds, IReadOnlyList<(string Title, string Why)> refused)
    {
        ArgumentNullException.ThrowIfNull(builds);
        ArgumentNullException.ThrowIfNull(refused);

        var intro = "Each build ticked gets a verifier card, which starts at once, runs the holdout scenarios that name it, and reports only a verdict."
                    + (refused.Count == 0 ? string.Empty : " Not checked: " + string.Join("; ", refused.Select(each => $"{each.Title}, {each.Why}")) + ".");

        return new Checklist("Start the checks", intro, "Start the checks",
            [.. builds.Select(build => new ChecklistRow(build.Key, build.Title, "Built, not yet checked by an independent test"))],
            ticked => ticked.Count == 0 ? "Nothing ticked" : $"Make and start {ticked.Count} {(ticked.Count == 1 ? "verifier" : "verifiers")}");
    }

    private static string Sentence(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..].TrimEnd('.') + ".";
}
