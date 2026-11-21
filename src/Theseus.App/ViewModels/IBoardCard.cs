namespace Theseus.App.ViewModels;

/// <summary>
/// Anything on the board that is selected, dragged and acted on as a card is: a workstream card or a project card
/// (1.1d AC 41). Click, Ctrl+click, Shift+click, the rubber band, Ctrl+A, Ctrl+I and Esc handle both through this.
/// </summary>
public interface IBoardCard
{
    /// <summary>Its name on screen.</summary>
    string Title { get; }

    /// <summary>Whether it is selected.</summary>
    bool IsSelected { get; set; }

    /// <summary>Whether a session is open on it: for a project, on any of its cards. Select live chooses these.</summary>
    bool IsLive { get; }

    /// <summary>What a drag of it carries, which the drop reads back: a card's key, or a project's name with a prefix.</summary>
    string DragPayload { get; }
}
