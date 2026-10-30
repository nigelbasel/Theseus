using Theseus.Core.Abstractions;
using Theseus.Core.Diagnostics;
using Theseus.Core.Domain;
using Theseus.Core.Settings;

namespace Theseus.App.Services;

/// <summary>
/// Turns a board's single row of lanes into two axes, once, the first time it is opened after the
/// grid exists.
///
/// It runs at startup rather than being offered, because a board half-migrated is worse than either
/// state: the lanes would still be a mixed metaphor and the rows would be empty. It is safe to run
/// because it moves nothing — every card keeps a place, and the settings it writes can be edited
/// afterwards like any other.
/// </summary>
public static class BoardMigration
{
    /// <summary>Splits the board if it has not been split, and says whether it did.</summary>
    public static bool Run(ISettingsStore settings, IBoardStore board, IPlanStore plans)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(plans);

        var current = settings.Read();

        if (!BoardSplit.IsNeeded(current.Lanes, current.Areas))
        {
            return false;
        }

        var (stages, areas) = BoardSplit.Split(current.Lanes);

        TheseusLog.Information("Splitting the board into {Stages} stage(s) and {Areas} area(s)",
            stages.Count, areas.Count);

        // The sidecars first, so that if writing the settings fails the cards still know where they
        // are. The other way round leaves a two-axis board full of cards that only know one axis.
        var sidecars = board.Read().ToList();
        var keys = plans.Keys();

        for (var index = 0; index < sidecars.Count; index++)
        {
            var (stage, area) = BoardSplit.Place(sidecars[index].Column, areas);

            sidecars[index] = sidecars[index] with { Column = stage, Area = area };
        }

        // A workstream with no sidecar has never been placed by hand, so it has no lane to convert;
        // it lands in the first cell the way a new card does, and needs nothing written for it.
        foreach (var key in keys.Where(key => !sidecars.Any(sidecar =>
                     string.Equals(sidecar.Key, key, StringComparison.OrdinalIgnoreCase))))
        {
            TheseusLog.Debug("Workstream {Key} had no board entry, so it takes the first cell", key);
        }

        board.Write(sidecars);
        settings.Write(current with { Lanes = stages, Areas = areas });

        return true;
    }

    /// <summary>
    /// Tidies the grid once (<see cref="BoardTidy"/>): Paused becomes a mark on the card, and the
    /// columns and rows Nigel agreed on 2026-10-30 replace the old ones. Says whether it ran.
    /// </summary>
    public static bool Tidy(ISettingsStore settings, IBoardStore board)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(board);

        var current = settings.Read();

        if (!BoardTidy.IsNeeded(current))
        {
            return false;
        }

        var (tidied, sidecars) = BoardTidy.Apply(current, board.Read());

        TheseusLog.Information("Tidying the board: stages {Stages}, areas {Areas}, {Paused} card(s) marked paused",
            string.Join(", ", tidied.Lanes), string.Join(", ", tidied.Areas), sidecars.Count(sidecar => sidecar.IsPaused));

        // The cards first, as above: a card that knows its new place on a board still showing the old
        // columns is put right by the next start, and the other way round is not.
        board.Write(sidecars);
        settings.Write(tidied);

        return true;
    }
}
