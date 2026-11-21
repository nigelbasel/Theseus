using System.IO;
using Theseus.Core.Domain;
using Theseus.Core.Links;

namespace Theseus.App.ViewModels;

/// <summary>
/// Whether Verify with holdouts can run for a card, why not, and the way on (1.1e AC 6a, 6c), from each project's holdout
/// folder as last read. The folders are looked at once per refresh, not on every command query, since a chosen folder may
/// be slow to reach (review of PR 2b, finding 3); the shell asks this and nothing else.
/// </summary>
public sealed class HoldoutChecks
{
    private readonly Dictionary<string, (HoldoutLocation Location, bool HasScenarios)> _byProject = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _beingWritten = new(StringComparer.OrdinalIgnoreCase);
    private readonly string? _localAppData;
    private string _chosen = string.Empty;

    /// <summary>Creates the checks.</summary>
    /// <param name="localAppData">Where default holdout folders go, for tests; null for the user's own.</param>
    public HoldoutChecks(string? localAppData = null) => _localAppData = localAppData;

    /// <summary>
    /// Looks at the holdout folders again: the one chosen in Options, else each project's default; and which have a
    /// scenario-writing card already, so Write scenarios… is not offered twice (review, finding 8).
    /// </summary>
    /// <param name="chosen">The folder chosen in Options, or empty.</param>
    /// <param name="cards">The board's cards.</param>
    public void Refresh(string chosen, IEnumerable<Workstream> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);

        var all = cards.ToList();
        _chosen = chosen ?? string.Empty;
        _byProject.Clear();
        _beingWritten.Clear();

        foreach (var project in all.Select(card => card.Project ?? string.Empty).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var location = Locate(project);
            _byProject[project] = (location, location.HasScenarios);
        }

        foreach (var writer in all.Where(card => card is { Role: WorkstreamRole.ScenarioWriter, IsArchived: false }))
        {
            _beingWritten.Add(Path.TrimEndingDirectorySeparator(writer.WorkingDirectory));
        }
    }

    /// <summary>A card's holdout folder.</summary>
    public HoldoutLocation LocationOf(Workstream card)
    {
        ArgumentNullException.ThrowIfNull(card);

        return _byProject.TryGetValue(card.Project ?? string.Empty, out var known) ? known.Location : Locate(card.Project);
    }

    /// <summary>Why Verify with holdouts cannot run for this card, or null when it can.</summary>
    public string? WhyCannotVerify(Workstream card, string title)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (card.IsArchived)
        {
            return $"{title} is archived";
        }

        if (card.Role != WorkstreamRole.None)
        {
            return $"{title} checks or writes holdout scenarios itself";
        }

        var (location, hasScenarios) = Known(card);

        if (location.IsMissing)
        {
            return $"The holdout folder {location.Folder} is missing";
        }

        return hasScenarios ? null : "No holdout scenarios for this project yet";
    }

    /// <summary>The way on from a disabled Verify: set a missing chosen folder, or write the first scenarios.</summary>
    public HoldoutWayOn WayOn(Workstream card)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (card.IsArchived || card.Role != WorkstreamRole.None)
        {
            return HoldoutWayOn.None;
        }

        var (location, hasScenarios) = Known(card);

        if (location.IsMissing)
        {
            return HoldoutWayOn.SetFolder;
        }

        return hasScenarios || _beingWritten.Contains(Path.TrimEndingDirectorySeparator(location.Folder))
            ? HoldoutWayOn.None
            : HoldoutWayOn.WriteScenarios;
    }

    private (HoldoutLocation Location, bool HasScenarios) Known(Workstream card) =>
        _byProject.TryGetValue(card.Project ?? string.Empty, out var known) ? known : (Locate(card.Project), false);

    private HoldoutLocation Locate(string? project) =>
        HoldoutLocation.For(_chosen, string.IsNullOrEmpty(project) ? null : project, _localAppData);
}
