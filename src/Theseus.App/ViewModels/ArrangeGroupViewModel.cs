using Theseus.App.Services;

namespace Theseus.App.ViewModels;

/// <summary>
/// One terminal window, as something to tick or leave alone.
///
/// It is named by what it holds rather than by any identity of its own, because it has none worth
/// showing: a window handle means nothing to anybody, and every window belongs to the same process.
/// The tabs are the only thing that says which of these is the one you meant.
/// </summary>
public sealed class ArrangeGroupViewModel : ObservableObject
{
    private bool _isIncluded = true;

    /// <summary>Creates a row for one window.</summary>
    public ArrangeGroupViewModel(TerminalWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        Window = window;
    }

    /// <summary>The window this row stands for.</summary>
    public TerminalWindow Window { get; }

    /// <summary>Whether this window takes part in the arrangement.</summary>
    public bool IsIncluded
    {
        get => _isIncluded;
        set
        {
            if (Set(ref _isIncluded, value))
            {
                Included?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>Raised when the tick changes, so the preview can be worked out again.</summary>
    public event EventHandler? Included;

    /// <summary>What to call this window: the session it is showing, or how many it holds.</summary>
    public string Title => Window.Tabs.Count switch
    {
        0 => "A terminal Theseus cannot read",
        1 => Clean(Window.Tabs[0]),
        _ => $"{Clean(Window.Tabs[0])} and {Window.Tabs.Count - 1} more",
    };

    /// <summary>Every tab in the window, for the line underneath.</summary>
    public string Sessions => Window.Tabs.Count == 0
        ? "Its tabs could not be read — a terminal running as administrator looks like this"
        : string.Join(" · ", Window.Tabs.Select(Clean));

    /// <summary>Where it is now, said plainly.</summary>
    public string Where => Window.IsMinimised
        ? "minimised — it will be restored"
        : $"{Window.Bounds.Width}×{Window.Bounds.Height} at {Window.Bounds.X},{Window.Bounds.Y}";

    /// <summary>
    /// A tab title without the glyph Claude Code puts in front of it. The glyph says what that
    /// session is doing this second, which is not what this list is for and only makes it flicker.
    /// </summary>
    private static string Clean(string title) =>
        string.IsNullOrWhiteSpace(title)
            ? "(untitled)"
            : title.TrimStart('◐', '◑', '◒', '◓', '✳', '·', ' ').Trim();
}
