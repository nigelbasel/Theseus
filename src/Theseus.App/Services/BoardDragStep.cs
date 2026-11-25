using Theseus.App.ViewModels;

namespace Theseus.App.Services;

/// <summary>What a pointer move on the board leads to (1.1d AC 42): a drag of a card or project card, a refusal to say in the status bar, or nothing.</summary>
/// <param name="Drag">What to drag now, or null.</param>
/// <param name="Refusal">Why nothing can be dragged here, said once per press, or null.</param>
public sealed record BoardDragStep(IBoardCard? Drag, string? Refusal)
{
    /// <summary>Nothing happens.</summary>
    public static BoardDragStep None { get; } = new(null, null);
}
