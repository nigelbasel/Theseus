using System.Windows.Input;
using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// The project view's buttons wired to the shell's own commands (1.1d AC 27): Verify with holdouts runs the card's
/// <see cref="WorkstreamCardViewModel.VerifyWithHoldoutsCommand"/>; Focus, Start and Stop run the shell's card commands
/// with the card, exactly as the card's own buttons do, and only when those commands can run.
/// </summary>
public static class ProjectViewCommands
{
    /// <summary>The actions for the view.</summary>
    /// <param name="cardOf">The board card with a key, or null.</param>
    /// <param name="focus">The shell's Focus command, which takes a card.</param>
    /// <param name="start">The shell's Start command, which takes a card.</param>
    /// <param name="stop">The shell's Stop command, which takes a card.</param>
    /// <param name="openSpec">Opens a spec as it was read.</param>
    /// <param name="acceptSpec">Accepts a spec, after asking.</param>
    /// <param name="openPath">Opens a folder or a file.</param>
    /// <param name="notice">Says in the status bar why nothing happened.</param>
    public static ProjectViewActions For(Func<string, WorkstreamCardViewModel?> cardOf, ICommand focus, ICommand start, ICommand stop,
        Action<SpecAtGate> openSpec, Action<SpecAtGate> acceptSpec, Action<string> openPath, Action<string> notice)
    {
        ArgumentNullException.ThrowIfNull(cardOf);
        ArgumentNullException.ThrowIfNull(notice);

        void OnCard(string key, Func<WorkstreamCardViewModel, ICommand?> command, bool passCard)
        {
            if (cardOf(key) is not { } card)
            {
                notice($"{key} is not on the board.");

                return;
            }

            var parameter = passCard ? card : null;

            if (command(card) is { } run && run.CanExecute(parameter))
            {
                run.Execute(parameter);
            }
            else
            {
                notice($"That cannot be done for {card.Title} just now.");
            }
        }

        return new ProjectViewActions(openSpec, acceptSpec,
            key => OnCard(key, card => card.VerifyWithHoldoutsCommand, passCard: false),
            key => OnCard(key, _ => focus, passCard: true),
            key => OnCard(key, _ => start, passCard: true),
            key => OnCard(key, _ => stop, passCard: true),
            openPath,
            notice);
    }
}
