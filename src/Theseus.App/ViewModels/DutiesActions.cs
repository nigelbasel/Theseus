using Theseus.Core.Duties;

namespace Theseus.App.ViewModels;

/// <summary>What the duties checklist reaches outside itself for.</summary>
/// <param name="Book">The board's duties.</param>
/// <param name="TitleOf">A card's title from its key.</param>
/// <param name="Record">Keeps a change in the card's History and the log.</param>
/// <param name="Edit">Opens the duty dialog; true when the owner pressed OK.</param>
/// <param name="ConfirmDelete">Asks before deleting; true to delete.</param>
/// <param name="Cards">The cards a new duty may go on, as key and title.</param>
/// <param name="RunNow">Looks at the duties at once, so Run now runs now rather than at the next tick.</param>
/// <param name="History">A duty's lines in its card's History, newest first: the card's key, then the duty's name.</param>
public sealed record DutiesActions(
    DutyBook Book,
    Func<string, string> TitleOf,
    Action<string, string> Record,
    Func<DutyEditViewModel, bool> Edit,
    Func<string, bool> ConfirmDelete,
    IReadOnlyList<(string Key, string Title)> Cards,
    Action RunNow,
    Func<string, string, IReadOnlyList<string>> History);
