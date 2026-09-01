using Theseus.Core.Layout;

namespace Theseus.App.Services;

/// <summary>
/// One terminal window on the desktop, and what it holds.
/// </summary>
/// <param name="Handle">The window handle.</param>
/// <param name="Bounds">Where it is now, in the same space <c>SetWindowPos</c> expects.</param>
/// <param name="IsMinimised">Whether it is minimised, which hides its real position.</param>
/// <param name="Tabs">
/// The titles of its tabs, which is the only thing that says which sessions it holds: one terminal
/// process hosts every window, so ownership identifies nothing. Empty for a window that could not be
/// read, which includes any running elevated.
/// </param>
public sealed record TerminalWindow(
    nint Handle,
    LayoutRect Bounds,
    bool IsMinimised,
    IReadOnlyList<string> Tabs);
