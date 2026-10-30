using Theseus.Core.Settings;

namespace Theseus.Core.Domain;

/// <summary>
/// The one-off tidy of the board's grid that Nigel agreed on 2026-10-30 ("Option A"): Paused stops
/// being a column and becomes a mark on the card, Testing becomes Review, Done starts folded, and
/// rows that held a card or two merge. Run once, recorded by <see cref="TheseusSettings.BoardRevision"/>,
/// so a row added back later under an old name stays.
/// </summary>
public static class BoardTidy
{
    /// <summary>The revision this tidy brings a board to.</summary>
    public const int Revision = 2;

    private const string PausedLane = "Paused";

    private static readonly Dictionary<string, string> LaneNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Testing"] = "Review",
    };

    private static readonly Dictionary<string, string> AreaNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Research"] = "Research & spikes",
        ["Spikes"] = "Research & spikes",
        ["Admin Tasks"] = "Support & admin",
        ["Support"] = "Support & admin",
        ["Side Project"] = "Side projects",
    };

    /// <summary>Whether this board has still to be tidied.</summary>
    public static bool IsNeeded(TheseusSettings settings) => settings.BoardRevision < Revision;

    /// <summary>
    /// The tidied settings and board entries. A card in the Paused column is marked paused and goes
    /// back to In progress if it has ever run, or to the first stage if it has not, since the column
    /// recorded nothing about where it was up to.
    /// </summary>
    public static (TheseusSettings Settings, IReadOnlyList<WorkstreamSidecar> Sidecars) Apply(
        TheseusSettings settings, IReadOnlyList<WorkstreamSidecar> sidecars)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(sidecars);

        var lanes = settings.Lanes
            .Where(lane => !Same(lane, PausedLane))
            .Select(lane => Rename(LaneNames, lane))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var areas = settings.Areas
            .Select(area => Rename(AreaNames, area))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var first = lanes.FirstOrDefault() ?? string.Empty;
        var started = lanes.FirstOrDefault(lane => Same(lane, "In progress")) ?? first;

        var tidied = sidecars.Select(sidecar =>
        {
            var area = sidecar.Area is null ? null : Rename(AreaNames, sidecar.Area);

            if (Same(sidecar.Column, PausedLane))
            {
                return sidecar with
                {
                    Column = sidecar.LastSessionId is null ? first : started,
                    IsPaused = true,
                    Area = area,
                };
            }

            return sidecar with { Column = Rename(LaneNames, sidecar.Column), Area = area };
        }).ToList();

        var done = lanes.FirstOrDefault(lane => Same(lane, "Done"));

        return (settings with
        {
            Lanes = lanes,
            Areas = areas,
            BoardRevision = Revision,
            CollapsedAreas = [.. settings.CollapsedAreas.Select(area => Rename(AreaNames, area))
                .Distinct(StringComparer.OrdinalIgnoreCase)],
            CollapsedStages = [.. settings.CollapsedStages.Where(stage => !Same(stage, PausedLane))
                .Select(stage => Rename(LaneNames, stage))
                .Concat(done is null ? [] : [done])
                .Distinct(StringComparer.OrdinalIgnoreCase)],
        }, tidied);
    }

    private static string Rename(Dictionary<string, string> names, string name) =>
        names.TryGetValue(name.Trim(), out var renamed) ? renamed : name;

    private static bool Same(string? left, string right) =>
        string.Equals(left?.Trim(), right, StringComparison.OrdinalIgnoreCase);
}
