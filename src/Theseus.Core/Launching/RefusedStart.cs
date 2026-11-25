using Theseus.Core.Running;

namespace Theseus.Core.Launching;

/// <summary>
/// What the board says when a press of Start was refused by a breaker or a limit: at once and where it was pressed, with
/// the action that clears it (Nigel, 2026-11-25: it was only a Warning in the log, and "Start again did not do anything").
/// </summary>
/// <param name="Title">The dialog's title, naming what could not be done (baseline A5).</param>
/// <param name="Text">Why, and what clears it.</param>
/// <param name="BreakerKeys">The cards whose breakers <paramref name="Action"/> resets before starting them again.</param>
/// <param name="Action">The label of the button that resets them and starts again, or null when there is none.</param>
public sealed record RefusedStart(string Title, string Text, IReadOnlyList<string> BreakerKeys, string? Action)
{
    private const string ResetAndStart = "_Reset the breaker and start";

    /// <summary>The message for the starts refused by one press, or null when none was.</summary>
    public static RefusedStart? For(IReadOnlyList<StartBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        if (blocks.Count == 0)
        {
            return null;
        }

        var title = blocks.Count == 1 ? $"Couldn't start {blocks[0].Title}" : $"Couldn't start {blocks.Count} cards";
        var lines = blocks.Select(block => blocks.Count == 1 ? $"{block.Detail} {WayOn(block)}" : $"{block.Title}: {block.Detail} {WayOn(block)}");
        IReadOnlyList<string> breakers = [.. blocks.Where(block => block.Reason == RefusalReason.BreakerTripped).Select(block => block.Key)];

        return new RefusedStart(title, string.Join(Environment.NewLine + Environment.NewLine, lines), breakers,
            breakers.Count == 0 ? null : ResetAndStart);
    }

    private static string WayOn(StartBlock block) => block.Reason == RefusalReason.BreakerTripped
        ? "Reset the breaker once you have seen why it tripped, and the card starts again."
        : "Raise or switch off the limit in Options › General › Headless runs, or start it tomorrow.";
}
