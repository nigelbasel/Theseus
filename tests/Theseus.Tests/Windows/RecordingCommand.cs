using System.Windows.Input;

namespace Theseus.Tests.Windows;

/// <summary>A command that records each parameter it is run with, for a test to read back.</summary>
internal sealed class RecordingCommand : ICommand
{
    private readonly Func<object?, bool> _canExecute;

    /// <summary>Creates a command that can always run, or only when the test says.</summary>
    public RecordingCommand(Func<object?, bool>? canExecute = null) => _canExecute = canExecute ?? (_ => true);

    /// <summary>The parameters it was run with, in order.</summary>
    public List<object?> Runs { get; } = [];

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public bool CanExecute(object? parameter) => _canExecute(parameter);

    /// <inheritdoc />
    public void Execute(object? parameter) => Runs.Add(parameter);
}
