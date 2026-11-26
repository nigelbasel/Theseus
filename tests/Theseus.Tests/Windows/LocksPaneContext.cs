using Theseus.App.ViewModels;

namespace Theseus.Tests.Windows;

/// <summary>What the locks pane binds to, with a real locks view in place of the fake shell's stand-in.</summary>
internal sealed class LocksPaneContext(LocksViewModel locks)
{
    public LocksViewModel Locks { get; } = locks;
}
