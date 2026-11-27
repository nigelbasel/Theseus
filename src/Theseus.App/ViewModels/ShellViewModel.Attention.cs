using Theseus.Core.Attention;
using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// What the main window shows, for attended time (7.3): a project's tab goes to the card of its selected box, or to the
/// project when none is selected; the board goes to its selected card. Read on the UI thread, where these live.
/// </summary>
public sealed partial class ShellViewModel
{
    /// <summary>The pieces' stable ids (7.3), given as each read of the projects lands.</summary>
    public PieceIdBook PieceIds { get; } = new(System.IO.Path.Combine(App.DataDirectory, "piece-ids.json"));

    /// <summary>Where the owner's spec acceptances, ticks and report confirmations are recorded against their piece (7.3); null for nowhere.</summary>
    public PieceEvents? PieceEvents { get; set; }

    /// <summary>What the main window shows now.</summary>
    public TheseusFocus AttentionFocus()
    {
        if (IsProjectOpen && ProjectView is { } view)
        {
            return new TheseusFocus(view.SelectedCardKey, view.Name);
        }

        return new TheseusFocus(IsAllProjectsOpen ? null : Board.SelectedCard?.Workstream.Key, null);
    }
}
