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
    private IReadOnlyDictionary<string, string> _chosenByProject = new Dictionary<string, string>();
    private Func<string, HoldoutConvention> _convention = _ => HoldoutConvention.None;

    /// <summary>Creates the checks.</summary>
    /// <param name="localAppData">Where default holdout folders go, for tests; null for the user's own.</param>
    public HoldoutChecks(string? localAppData = null) => _localAppData = localAppData;

    /// <summary>
    /// Finds each project's holdout folder in the order of <see cref="HoldoutLocation.Find"/>: chosen for the project, the
    /// repository its project.md records, a folder beside the home repo's clone, Theseus's own; and which have a
    /// scenario-writing card already, so Write scenarios… is not offered twice (review, finding 8).
    /// </summary>
    /// <param name="chosenByProject">The folders chosen per project.</param>
    /// <param name="convention">A project's home clone and recorded holdout repository.</param>
    /// <param name="cards">The board's cards, whose projects are looked up.</param>
    public void Refresh(IReadOnlyDictionary<string, string> chosenByProject, Func<string, HoldoutConvention> convention,
        IEnumerable<Workstream> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);

        var all = cards.ToList();
        _chosenByProject = new Dictionary<string, string>(chosenByProject, StringComparer.OrdinalIgnoreCase);
        _convention = convention;
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

    /// <summary>A project's holdout folder, by its folder name, as last refreshed.</summary>
    public HoldoutLocation LocationOfProject(string project) =>
        _byProject.TryGetValue(project, out var known) ? known.Location : Locate(project);

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
            return location.Source != HoldoutSource.Recorded
                ? $"The holdout folder {location.Folder} is missing"
                : location.Folder.Length == 0
                    ? "project.md records a holdout repository, but the home repo's clone is not found, so neither is it"
                    : $"project.md records a holdout repository, but no clone was found at {location.Folder}: clone it there, or choose a folder in Project properties";
        }

        return hasScenarios ? null : "No holdout scenarios for this project yet";
    }

    /// <summary>
    /// A Verify menu item's text: the reason it cannot run follows the label, not only in its tooltip (Nigel, 2026-11-23).
    /// </summary>
    /// <param name="why">Why it cannot run, or null when it can.</param>
    public static string MenuText(string? why) => why is null ? VerifyMenuLabel : $"{VerifyMenuLabel} — {why}";

    /// <summary>The Verify menu item's own label, with its access key.</summary>
    public const string VerifyMenuLabel = "_Verify with holdouts…";

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

    /// <summary>The folders in force, one per project on the board and one for ad hoc cards, as the guard is to cover them.</summary>
    public IReadOnlyList<HoldoutFolderEntry> Entries =>
        [.. _byProject.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => new HoldoutFolderEntry(pair.Key.Length == 0 ? HoldoutLocation.AdHocFolder : pair.Key, pair.Value.Location.Folder,
                pair.Value.Location.Source))];

    /// <summary>
    /// The folders the holdout guard is to cover: each in force that has a place, and also a <c>&lt;repo&gt;-holdout</c> folder
    /// beside the home repo when another folder took precedence, since scenarios may still be in it (review, finding 1).
    /// </summary>
    public IReadOnlyList<HoldoutFolderEntry> GuardedEntries =>
        [.. Entries.Where(entry => entry.Folder.Length > 0),
            .. _byProject.Keys.Where(project => project.Length > 0)
                .Select(project => (project, beside: _convention(project).BesideRepo))
                .Where(each => each.beside is not null
                               && !string.Equals(each.beside, _byProject[each.project].Location.Folder, StringComparison.OrdinalIgnoreCase))
                .OrderBy(each => each.project, StringComparer.OrdinalIgnoreCase)
                .Select(each => new HoldoutFolderEntry(each.project, each.beside!, HoldoutSource.BesideRepo))];

    private HoldoutLocation Locate(string? project) =>
        string.IsNullOrEmpty(project)
            ? HoldoutLocation.Find(null, null, null, _localAppData)
            : HoldoutLocation.Find(project, _chosenByProject.GetValueOrDefault(project), _convention(project), _localAppData);
}
