namespace Theseus.App.ViewModels;

/// <summary>
/// A box's "Properties" (walkthrough row 32: "Show the card" opened the card's properties, so it is named for that): opens the card's details over the project view, at Overview, where Start, Stop and Focus
/// are. It was "Show on the board", which closed the view and selected a card the board does not draw, since a project's
/// cards are not on the board (project-view-usability item 3, Nigel 2026-11-23). The drawer is the simpler of the two
/// ways the brief offered: it needs no board filter, and the owner stays where he was.
/// </summary>
public static class ShowTheCard
{
    /// <summary>The action for one card's box.</summary>
    /// <param name="card">The card the box is drawn for.</param>
    /// <param name="open">Opens a card's details at Overview.</param>
    public static PieceActionViewModel For(WorkstreamCardViewModel card, Action<WorkstreamCardViewModel> open)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(open);

        return new PieceActionViewModel("Properties", () => open(card),
            toolTip: $"Open {card.Title}'s details here, at Overview");
    }
}
