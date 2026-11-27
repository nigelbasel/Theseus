namespace Theseus.Core.Projects;

/// <summary>
/// Which cards are in which project, read from the project files and nowhere else (1.1c AC 1 and 2): a table row's
/// Workstream names a card by its key, and a card is in the project whose table names it, under the slug of its last
/// row there. A card named by two projects belongs to the first by name, and both are listed as clashing.
/// </summary>
public sealed class ProjectMembers
{
    private readonly Dictionary<string, ProjectMember> _byCard;

    private ProjectMembers(Dictionary<string, ProjectMember> byCard, IReadOnlyList<string> clashes)
    {
        _byCard = byCard;
        Clashes = clashes;
    }

    /// <summary>No cards in any project.</summary>
    public static ProjectMembers None { get; } = new(new Dictionary<string, ProjectMember>(StringComparer.OrdinalIgnoreCase), []);

    /// <summary>Every card named, by its key.</summary>
    public IReadOnlyDictionary<string, ProjectMember> ByCard => _byCard;

    /// <summary>The projects that name a card another project names too, each once.</summary>
    public IReadOnlyList<string> Clashes { get; }

    /// <summary>The card's place, or null when no readable project names it.</summary>
    public ProjectMember? Of(string cardKey) => _byCard.GetValueOrDefault(cardKey);

    /// <summary>Reads the members from the projects as read; pinned and unreadable projects name no one.</summary>
    public static ProjectMembers From(IEnumerable<ProjectFileReading> readings)
    {
        ArgumentNullException.ThrowIfNull(readings);

        var byCard = new Dictionary<string, ProjectMember>(StringComparer.OrdinalIgnoreCase);
        var clashes = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in readings.Where(reading => reading.IsReadable).Select(reading => reading.File!)
                     .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase))
        {
            var lastSlug = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in file.Workstreams)
            {
                lastSlug[row.Workstream] = row.Slug;
            }

            foreach (var (card, slug) in lastSlug)
            {
                if (byCard.TryGetValue(card, out var earlier))
                {
                    clashes.Add(earlier.Project);
                    clashes.Add(file.Name);

                    continue;
                }

                byCard[card] = new ProjectMember(file.Name, slug);
            }
        }

        return new ProjectMembers(byCard, [.. clashes]);
    }

    /// <summary>The same members, each carrying its piece's id where <paramref name="idFor"/> knows one (7.3).</summary>
    public ProjectMembers WithPieceIds(Func<ProjectMember, string?> idFor)
    {
        ArgumentNullException.ThrowIfNull(idFor);

        return new ProjectMembers(
            _byCard.ToDictionary(entry => entry.Key, entry => entry.Value with { PieceId = idFor(entry.Value) ?? entry.Value.PieceId },
                StringComparer.OrdinalIgnoreCase),
            Clashes);
    }

    /// <summary>Members remembered from an earlier run, as <see cref="ByCard"/> gave them.</summary>
    public static ProjectMembers Remembered(IReadOnlyDictionary<string, ProjectMember> byCard)
    {
        ArgumentNullException.ThrowIfNull(byCard);

        return new ProjectMembers(new Dictionary<string, ProjectMember>(byCard, StringComparer.OrdinalIgnoreCase), []);
    }
}
