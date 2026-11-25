namespace Theseus.Tests.Windows;

/// <summary>A side panel's members as MainWindow binds them: whether it is open, and the command that opens or closes it.</summary>
internal sealed class FakePanel
{
    /// <summary>Whether the panel is open.</summary>
    public bool IsOpen { get; set; }

    /// <summary>Opens or closes the panel.</summary>
    public RecordingCommand ToggleCommand { get; } = new();
}
