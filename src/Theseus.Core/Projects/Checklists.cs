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

    private static string Sentence(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..].TrimEnd('.') + ".";
}
