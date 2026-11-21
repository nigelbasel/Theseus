namespace Theseus.Core.Projects;

/// <summary>
/// The cards each project's file has named, kept after the file stops reading (review of 1.1e PR 4, finding 1): a file
/// that does not read names no cards, yet its cards still write it, so Update must still wait for them.
/// </summary>
public sealed class ProjectMemberHistory
{
    private readonly Dictionary<string, HashSet<string>> _byProject = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _gate = new();

    /// <summary>Adds the members of what reads now to what is known; nothing known is forgotten.</summary>
    public void Remember(ProjectMembers members)
    {
        ArgumentNullException.ThrowIfNull(members);

        lock (_gate)
        {
            foreach (var (card, member) in members.ByCard)
            {
                if (!_byProject.TryGetValue(member.Project, out var cards))
                {
                    _byProject[member.Project] = cards = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }

                cards.Add(card);
            }
        }
    }

    /// <summary>The keys of every card a project's file has named while this Theseus ran, or knew from its last run.</summary>
    public IReadOnlySet<string> CardsOf(string project)
    {
        lock (_gate)
        {
            return _byProject.TryGetValue(project, out var cards)
                ? new HashSet<string>(cards, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
