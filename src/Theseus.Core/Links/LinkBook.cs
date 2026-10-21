using Theseus.Core.Abstractions;

namespace Theseus.Core.Links;

/// <summary>
/// Every link between workstreams, and the only thing that changes them. It refuses what would make
/// the board lie: a card linked to itself, the same link twice, a card waiting on its own output
/// through a ring of others, a card that is its own ancestor, or a child with two parents.
///
/// Every change in this phase comes from the board, on the UI thread, so there is no lock yet. One
/// arrives with the session tools, whose calls land on pool threads.
/// </summary>
public sealed class LinkBook
{
    private readonly LinkStateStore _store;
    private readonly ISystemClock _clock;
    private readonly List<Link> _links;

    /// <summary>Opens the book from its saved state.</summary>
    public LinkBook(LinkStateStore store, ISystemClock clock)
    {
        _store = store;
        _clock = clock;
        _links = [.. store.Load()];
    }

    /// <summary>Every link ever made, removed ones included.</summary>
    public IReadOnlyList<Link> All => [.. _links];

    /// <summary>The links that still stand.</summary>
    public IReadOnlyList<Link> Active => [.. _links.Where(link => link.IsActive)];

    /// <summary>Makes a link, unless it would contradict the ones already standing.</summary>
    public LinkChange Add(LinkKind kind, string from, string to, string? output = null, string? brief = null,
        string? handOver = null)
    {
        if (SameKey(from, to))
        {
            return LinkChange.Refused("A card cannot be linked to itself.");
        }

        var active = Active;

        if (active.Any(link => link.Kind == kind && SameKey(link.From, from) && SameKey(link.To, to)))
        {
            return LinkChange.Refused("Those two cards are already linked that way.");
        }

        if (kind == LinkKind.Feeds && Reaches(active, LinkKind.Feeds, to, from))
        {
            return LinkChange.Refused(
                $"That would make a cycle: {from} already waits, through other cards, on {to}.");
        }

        if (kind == LinkKind.Spawned)
        {
            if (active.FirstOrDefault(link => link.Kind == LinkKind.Spawned && SameKey(link.To, to)) is { } parent)
            {
                return LinkChange.Refused($"{to} was already spawned by {parent.From}.");
            }

            if (Reaches(active, LinkKind.Spawned, to, from))
            {
                return LinkChange.Refused($"That would make {to} its own ancestor.");
            }
        }

        var link = new Link(NewId(), kind, from, to, _clock.UtcNow, Link.MadeOnTheBoard,
            Brief: Blank(brief), HandOver: Blank(handOver), Output: Blank(output));

        _links.Add(link);
        Save();

        return LinkChange.Made(link);
    }

    /// <summary>Removes a link. It is kept as a tombstone, never deleted.</summary>
    public LinkChange Remove(string id) =>
        Change(id, link => LinkChange.Made(link with { RemovedUtc = _clock.UtcNow }));

    /// <summary>Accepts a feeds link's input by hand, for the reason Nigel gives.</summary>
    public LinkChange Satisfy(string id, string reason) =>
        Change(id, link =>
        {
            if (link.Kind != LinkKind.Feeds)
            {
                return LinkChange.Refused("Only a link a card waits on can be satisfied.");
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                return LinkChange.Refused("Say why it is satisfied; the reason is kept with the link.");
            }

            return LinkChange.Made(link with
            {
                SatisfiedUtc = _clock.UtcNow,
                SatisfiedBy = $"by hand: {reason.Trim()}",
            });
        });

    /// <summary>Removes every link touching a card that has been deleted.</summary>
    public void RemoveAllFor(string key)
    {
        var now = _clock.UtcNow;
        var changed = false;

        for (var index = 0; index < _links.Count; index++)
        {
            if (_links[index].IsActive && _links[index].Touches(key))
            {
                _links[index] = _links[index] with { RemovedUtc = now };
                changed = true;
            }
        }

        if (changed)
        {
            Save();
        }
    }

    /// <summary>
    /// Whether any link, removed or not, names this key. A deleted card's key stays taken, so a new
    /// card with the same title gets a fresh key and inherits nothing.
    /// </summary>
    public bool IsKeyTaken(string key) => _links.Any(link => link.Touches(key));

    private LinkChange Change(string id, Func<Link, LinkChange> change)
    {
        var index = _links.FindIndex(link => link.Id == id && link.IsActive);

        if (index < 0)
        {
            return LinkChange.Refused("That link no longer exists.");
        }

        var result = change(_links[index]);

        if (result.Link is { } changed)
        {
            _links[index] = changed;
            Save();
        }

        return result;
    }

    private static bool Reaches(IReadOnlyList<Link> links, LinkKind kind, string start, string target)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>([start]);

        while (pending.TryPop(out var key))
        {
            if (SameKey(key, target))
            {
                return true;
            }

            if (!seen.Add(key))
            {
                continue;
            }

            foreach (var link in links.Where(link => link.Kind == kind && SameKey(link.From, key)))
            {
                pending.Push(link.To);
            }
        }

        return false;
    }

    private static bool SameKey(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static string NewId() => $"lk-{Guid.NewGuid():N}"[..11];

    private void Save() => _store.Save(_links);
}
