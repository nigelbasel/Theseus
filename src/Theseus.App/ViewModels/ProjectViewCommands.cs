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
    /// <param name="openSpec">Opens a spec in its window.</param>
    /// <param name="acceptSpec">Accepts a spec, after asking.</param>
    /// <param name="openPath">Opens a folder or a file.</param>
    /// <param name="notice">Says in the status bar why nothing happened.</param>
    /// <param name="addToProject">The shell's Add to project…, for a card and the project to choose first (1.1e AC 6).</param>
    /// <param name="whyNot">Why a button cannot run for a card now, or null (1.1e AC 6a).</param>
    /// <param name="wayOn">The way on from a disabled button, or null (1.1e AC 6a).</param>
    public static ProjectViewActions For(Func<string, WorkstreamCardViewModel?> cardOf, ICommand focus, ICommand start, ICommand stop,
        Action<SpecAtGate> openSpec, Action<SpecAtGate> acceptSpec, Action<string> openPath, Action<string> notice,
        Action<WorkstreamCardViewModel, string>? addToProject = null,
        Func<WorkstreamCardViewModel, StageAction, string?>? whyNot = null,
        Func<WorkstreamCardViewModel, StageAction, PieceActionViewModel?>? wayOn = null)
    {
        ArgumentNullException.ThrowIfNull(cardOf);
        ArgumentNullException.ThrowIfNull(notice);

        void OnCard(string key, StageAction action, Func<WorkstreamCardViewModel, ICommand?> command, bool passCard)
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
                // The view is drawn from the last refresh; if the card changed since, say why it cannot run now (1.1e AC 6a).
                notice(whyNot?.Invoke(card, action) is { } why ? $"{why}." : $"That cannot be done for {card.Title} just now.");
            }
        }

        return new ProjectViewActions(openSpec, acceptSpec,
            key => OnCard(key, StageAction.VerifyWithHoldouts, card => card.VerifyWithHoldoutsCommand, passCard: false),
            key => OnCard(key, StageAction.Focus, _ => focus, passCard: true),
            key => OnCard(key, StageAction.Start, _ => start, passCard: true),
            key => OnCard(key, StageAction.Stop, _ => stop, passCard: true),
            openPath,
            notice,
            addToProject is null
                ? null
                : (project, key) =>
                {
                    if (cardOf(key) is { } card)
                    {
                        addToProject(card, project);
                    }
                    else
                    {
                        notice($"{key} is not on the board.");
                    }
                },
            whyNot is null ? null : (key, action) => cardOf(key) is { } card ? whyNot(card, action) : $"{key} is not on the board",
            wayOn is null ? null : (key, action) => cardOf(key) is { } card ? wayOn(card, action) : null);
    }
}
