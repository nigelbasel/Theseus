using System.ComponentModel;
using System.Windows.Input;

namespace Theseus.App.ViewModels;

/// <summary>
/// A command that says why it cannot run (baseline W13, finding C-m1; checklist G157). Each time WPF asks whether it can
/// run, the reason is worked out again and <see cref="WhyNot"/> follows, so a disabled menu item or toolbar button bound
/// to it explains itself as the state changes.
/// </summary>
public sealed class ReasonedCommand : ICommand, INotifyPropertyChanged
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, string?> _whyNot;
    private string? _reason;

    /// <summary>Creates a command.</summary>
    /// <param name="execute">What it does.</param>
    /// <param name="whyNot">Why it cannot run just now, or null when it can.</param>
    public ReasonedCommand(Action<object?> execute, Func<object?, string?> whyNot)
    {
        _execute = execute;
        _whyNot = whyNot;
    }

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Why it could not run when last asked, or null when it could.</summary>
    public string? WhyNot
    {
        get => _reason;
        private set
        {
            if (!string.Equals(_reason, value, StringComparison.Ordinal))
            {
                _reason = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WhyNot)));
            }
        }
    }

    /// <inheritdoc />
    public bool CanExecute(object? parameter)
    {
        WhyNot = _whyNot(parameter);

        return WhyNot is null;
    }

    /// <inheritdoc />
    public void Execute(object? parameter) => _execute(parameter);
}
