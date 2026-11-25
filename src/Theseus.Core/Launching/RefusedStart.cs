namespace Theseus.Core.Launching;

/// <summary>What the board says when a press of Start was refused by a breaker or a limit.</summary>
/// <param name="Title">The dialog's title, naming what could not be done.</param>
/// <param name="Text">Why, and what clears it.</param>
/// <param name="BreakerKeys">The cards whose breakers the action resets.</param>
/// <param name="Action">The label of the button that resets them and starts again, or null when there is none.</param>
public sealed record RefusedStart(string Title, string Text, IReadOnlyList<string> BreakerKeys, string? Action)
{
    /// <summary>The message for the starts refused by one press, or null when none was.</summary>
    public static RefusedStart? For(IReadOnlyList<StartBlock> blocks) => null;
}
