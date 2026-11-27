using System.Windows;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Theseus.App.Views;

/// <summary>
/// A view's command bound to a key that a text box also uses, such as Ctrl+Z (baseline W27, finding B18; checklist G75):
/// it runs only while keyboard focus is outside the view's text boxes. A text box with nothing to undo lets Ctrl+Z
/// through, which would otherwise undo something else while the owner was typing. A read-only box, such as a banner's
/// selectable text (1.3e), has nothing to undo and takes nothing typed, so it does not keep the key.
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
        !KeepsTheKey(Keyboard.FocusedElement, _view) && _target() is { } command && command.CanExecute(parameter);

    /// <summary>Whether <paramref name="focused"/> is a text box in <paramref name="view"/> that the owner can type in.</summary>
    public static bool KeepsTheKey(IInputElement? focused, Visual view) =>
        focused is TextBoxBase { IsReadOnly: false } box && view.IsAncestorOf(box);

    /// <inheritdoc />
    public void Execute(object? parameter)
    {
        if (CanExecute(parameter))
        {
            _target()!.Execute(parameter);
        }
    }
}
