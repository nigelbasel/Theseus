namespace Theseus.App.ViewModels;

/// <summary>
/// Why a command on the ticked cards cannot run just now, for its disabled menu item and toolbar button (baseline W13,
/// W30, W31; findings B6, C-m1; checklist G61, G157). Null means it can run.
/// </summary>
public static class SelectionReasons
{
    /// <summary>The reason while no card is ticked.</summary>
    public const string NothingTicked = "Tick one or more cards first.";

    /// <summary>Start selected: needs a ticked card or project that can start.</summary>
    /// <param name="ticked">How many cards and projects are ticked.</param>
    /// <param name="startable">How many of them can start.</param>
    public static string? Start(int ticked, int startable) =>
        startable > 0 ? null
        : ticked == 0 ? NothingTicked
        : "Every ticked card is already running, or cannot start just now.";

    /// <summary>Stop selected: needs a ticked card or project with a session running.</summary>
    /// <param name="ticked">How many cards and projects are ticked.</param>
    /// <param name="running">How many of them have a session to stop.</param>
    public static string? Stop(int ticked, int running) =>
        running > 0 ? null
        : ticked == 0 ? NothingTicked
        : "No ticked card has a session running.";

    /// <summary>Archive selected: not while the archive is showing, and only cards with no session running.</summary>
    /// <param name="archiveShown">Whether the board shows the archive.</param>
    /// <param name="ticked">How many cards are ticked.</param>
    /// <param name="quiet">How many of them have no session running.</param>
    public static string? Archive(bool archiveShown, int ticked, int quiet) =>
        archiveShown ? "The archive is showing: Restore selected puts cards back."
        : quiet > 0 ? null
        : ticked == 0 ? NothingTicked
        : "Every ticked card has a session running. Stop it first.";

    /// <summary>Restore selected: only while the archive is showing.</summary>
    /// <param name="archiveShown">Whether the board shows the archive.</param>
    /// <param name="ticked">How many cards are ticked.</param>
    public static string? Restore(bool archiveShown, int ticked) =>
        !archiveShown ? "Show the archive first, with View › Archive."
        : ticked == 0 ? NothingTicked
        : null;

    /// <summary>Select live: needs a card on the board with a session running.</summary>
    /// <param name="live">How many cards shown have a session running.</param>
    public static string? SelectLive(int live) => live > 0 ? null : "No card on the board has a session running.";

    /// <summary>Fold cards with no session: needs an unfolded card with no session.</summary>
    /// <param name="foldable">How many cards have no session and are unfolded.</param>
    public static string? FoldInactive(int foldable) => foldable > 0 ? null : "Every card with no session is already folded.";

    /// <summary>Unfold all cards: needs a folded card.</summary>
    /// <param name="folded">How many cards are folded.</param>
    public static string? UnfoldAll(int folded) => folded > 0 ? null : "Every card is already unfolded.";
}
