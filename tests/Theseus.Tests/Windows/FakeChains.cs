using System.Collections.ObjectModel;
using Theseus.App.ViewModels;

namespace Theseus.Tests.Windows;

/// <summary>The Chains panel's members as MainWindow binds them: open or closed, its toggle, and the tree's rows.</summary>
internal sealed class FakeChains
{
    /// <summary>Whether the panel is open.</summary>
    public bool IsOpen { get; set; }

    /// <summary>Opens or closes the panel.</summary>
    public RecordingCommand ToggleCommand { get; } = new();

    /// <summary>The tree's top rows; none unless a test adds them.</summary>
    public ObservableCollection<ChainNodeViewModel> Roots { get; } = [];
}
