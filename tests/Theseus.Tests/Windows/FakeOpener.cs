namespace Theseus.Tests.Windows;

/// <summary>A window's view model as MainWindow binds it from a menu or a key: the command that opens it.</summary>
internal sealed class FakeOpener
{
    /// <summary>Opens the window.</summary>
    public RecordingCommand OpenCommand { get; } = new();
}
