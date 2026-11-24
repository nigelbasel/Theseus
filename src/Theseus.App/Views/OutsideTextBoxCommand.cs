using System.Windows.Media;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Theseus.App.Views;

/// <summary>
/// A view's command bound to a key that a text box also uses, such as Ctrl+Z (baseline W27, finding B18; checklist G75):
/// it runs only while keyboard focus is outside the view's text boxes. A text box with nothing to undo lets Ctrl+Z
/// through, which would otherwise undo something else while the owner was typing.
/// </summary>
public sealed class OutsideTextBoxCommand : ICommand
{
    private readonly Visual _view;
    private readonly Func<ICommand?> _target;

    /// <summary>Creates the command.</summary>
    /// <param name="view">The view whose text boxes keep the key.</param>
    /// <param name="target">The command to run, read each time, since the view's data context changes.</param>
    public OutsideTextBoxCommand(Visual view, Func<ICommand?> target)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(target);

        _view = view;
        _target = target;
    }

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    /// <inheritdoc />
    public bool CanExecute(object? parameter) =>
        !(Keyboard.FocusedElement is TextBoxBase box && _view.IsAncestorOf(box)) && _target() is { } command && command.CanExecute(parameter);

    /// <inheritdoc />
    public void Execute(object? parameter)
    {
        if (CanExecute(parameter))
        {
            _target()!.Execute(parameter);
        }
    }
}
