using Theseus.Core.Abstractions;
using Theseus.Core.Finding;
using Theseus.Core.Settings;

namespace Theseus.App.ViewModels;

/// <summary>
/// The owner's tags on the board's cards (card-search-and-tags items 9 to 13): a workstream card's in its sidecar, a
/// project card's in settings by project name, and each tag's colour in settings. Only the owner sets them, from the
/// board (decision 3): nothing here is reachable from a session's tools.
/// </summary>
public sealed class BoardTags
{
    private readonly BoardViewModel _board;
    private readonly IBoardStore _store;
    private readonly ISettingsStore _settings;

    /// <summary>Creates the tags over the board and its stores.</summary>
    internal BoardTags(BoardViewModel board, IBoardStore store, ISettingsStore settings)
    {
        _board = board;
        _store = store;
        _settings = settings;
    }

    /// <summary>Asks the owner for a new tag's name (New tag…); null when cancelled. Set by the app.</summary>
    public Func<string?>? AskNewTag { get; set; }

    /// <summary>Raised after tags change on any card, so an open Tags field shows the change (review T1).</summary>
    public event Action? Changed;

    /// <summary>Tells the owner something in the status bar, such as why a typed tag was refused. Set by the shell.</summary>
    public Action<string>? Say { get; set; }

    /// <summary>Every tag in use, on any card, archived or not, or any project card, with how many carry it (items 12, 13).</summary>
    public IReadOnlyList<TagCount> InUse() =>
        TagLists.Counts(_store.Read().Select(sidecar => sidecar.Tags).Concat(_settings.Read().ProjectTags.Values));

    /// <summary>A tag's colour: the one set for it, else the palette's first.</summary>
    public string ColourOf(string tag) =>
        _settings.Read().TagColours.GetValueOrDefault(tag) ?? TagPalette.Colours[0];

    /// <summary>Gives each of these tags a colour of its own if it has none yet (item 11: chosen on first use).</summary>
    public void EnsureColours(IEnumerable<string> tags)
    {
        var settings = _settings.Read();
        var assigned = TagPalette.Assign(settings.TagColours, tags);

        if (!ReferenceEquals(assigned, settings.TagColours))
        {
            _settings.Write(settings with { TagColours = assigned });
        }
    }

    /// <summary>Changes a tag's colour (Options, Tags; item 11).</summary>
    public void SetColour(string tag, string colour)
    {
        var settings = _settings.Read();
        _settings.Write(settings with { TagColours = new Dictionary<string, string>(settings.TagColours, StringComparer.Ordinal) { [tag] = colour } });
        _board.Regroup();
    }

    /// <summary>A project card's tags, from settings.</summary>
    public IReadOnlyList<string> OfProject(string name) =>
        _settings.Read().ProjectTags.FirstOrDefault(entry => string.Equals(entry.Key, name, StringComparison.OrdinalIgnoreCase)).Value ?? [];

    /// <summary>Sets a project card's tags, from its Properties (item 10).</summary>
    public void SetProject(string name, IReadOnlyList<string> tags)
    {
        var clean = TagNames.Clean(tags);
        var settings = _settings.Read();
        var all = new Dictionary<string, IReadOnlyList<string>>(settings.ProjectTags, StringComparer.OrdinalIgnoreCase);

        if (clean.Count == 0)
        {
            all.Remove(name);
        }
        else
        {
            all[name] = clean;
        }

        _settings.Write(settings with { ProjectTags = all });
        EnsureColours(clean);

        if (_board.ProjectCards.FirstOrDefault(project => string.Equals(project.Name, name, StringComparison.OrdinalIgnoreCase)) is { } card)
        {
            card.Tags = clean;
        }

        _board.Regroup();
    }

    /// <summary>
    /// The cards a tag from a card's menu acts on (item 10): every selected card when the card clicked is one of several
    /// selected, else the card clicked alone.
    /// </summary>
    public IReadOnlyList<IBoardCard> Targets(IBoardCard clicked)
    {
        var selected = _board.ShownItems.Where(item => item.IsSelected).ToList();

        return clicked.IsSelected && selected.Count > 1 ? selected : [clicked];
    }

    /// <summary>Adds a tag to the cards, or takes it off them (items 10, 11).</summary>
    public void Set(IReadOnlyList<IBoardCard> items, string tag, bool on)
    {
        foreach (var item in items)
        {
            switch (item)
            {
                case WorkstreamCardViewModel card:
                    var tags = on ? TagLists.With(card.Tags, tag) : TagLists.Without(card.Tags, tag);

                    if (!tags.SequenceEqual(card.Tags))
                    {
                        card.Workstream = card.Workstream with { Tags = tags };
                        _board.Persist(card);
                    }

                    break;
                case ProjectCardViewModel project:
                    SetProject(project.Name, on ? TagLists.With(project.Tags, tag) : TagLists.Without(project.Tags, tag));
                    break;
            }
        }

        if (on)
        {
            EnsureColours([tag]);
        }

        _board.Regroup();
        Changed?.Invoke();
    }

    /// <summary>
    /// The card menu's Tags › (item 10): every tag in use, ticked when every card it acts on has it, then New tag…. Built
    /// as the menu opens, since what is selected decides what it acts on.
    /// </summary>
    public IReadOnlyList<TagMenuItemViewModel> MenuFor(IBoardCard clicked)
    {
        var targets = Targets(clicked);
        var several = targets.Count > 1;
        var items = new List<TagMenuItemViewModel>();

        foreach (var use in InUse())
        {
            var all = targets.All(target => TagsOf(target).Contains(use.Tag, StringComparer.Ordinal));
            var tag = use.Tag;
            items.Add(new TagMenuItemViewModel(tag, all, ColourOf(tag),
                new RelayCommand(_ => Set(targets, tag, !all)),
                all
                    ? several ? $"Take #{tag} off the {targets.Count} selected cards" : $"Take #{tag} off this card"
                    : several ? $"Put #{tag} on the {targets.Count} selected cards" : $"Put #{tag} on this card"));
        }

        items.Add(new TagMenuItemViewModel("New tag…", false, null, new RelayCommand(_ => AddNew(targets)),
            several ? $"Make a tag and put it on the {targets.Count} selected cards" : "Make a tag and put it on this card"));

        return items;
    }

    /// <summary>Renames a tag on every card and project card, and keeps its colour; returns how many carried it (item 13).</summary>
    public int Rename(string from, string to)
    {
        var count = Change(tags => tags.Contains(from, StringComparer.Ordinal) ? TagLists.Renamed(tags, from, to) : null);
        var settings = _settings.Read();

        if (settings.TagColours.TryGetValue(from, out var colour))
        {
            var colours = new Dictionary<string, string>(settings.TagColours, StringComparer.Ordinal);
            colours.Remove(from);
            colours.TryAdd(to, colour);
            _settings.Write(settings with { TagColours = colours });
        }

        var ticked = _board.Search.TickedTags;

        if (ticked.Remove(from) && !ticked.Contains(to))
        {
            ticked.Add(to);
        }

        _board.Search.RetypeTag(from, to);
        _board.Refresh();
        Changed?.Invoke();

        return count;
    }

    /// <summary>Deletes a tag from every card and project card, with its colour; returns how many carried it (item 13).</summary>
    public int Delete(string tag)
    {
        var count = Change(tags => tags.Contains(tag, StringComparer.Ordinal) ? TagLists.Without(tags, tag) : null);
        var settings = _settings.Read();

        if (settings.TagColours.ContainsKey(tag))
        {
            var colours = new Dictionary<string, string>(settings.TagColours, StringComparer.Ordinal);
            colours.Remove(tag);
            _settings.Write(settings with { TagColours = colours });
        }

        _board.Search.TickedTags.Remove(tag);
        _board.Search.RetypeTag(tag, null);
        _board.Refresh();
        Changed?.Invoke();

        return count;
    }

    private static IReadOnlyList<string> TagsOf(IBoardCard item) => item switch
    {
        WorkstreamCardViewModel card => card.Tags,
        ProjectCardViewModel project => project.Tags,
        _ => [],
    };

    private void AddNew(IReadOnlyList<IBoardCard> targets)
    {
        if (AskNewTag?.Invoke() is not { } typed)
        {
            return;
        }

        if (TagNames.Normalise(typed) is { } tag)
        {
            Set(targets, tag, on: true);
        }
        else
        {
            Say?.Invoke($"No tag added: {TagNames.WhyNot(typed)}.");
        }
    }

    // Rewrites every card's and project card's tags that the change touches (null leaves them); returns how many it touched.
    private int Change(Func<IReadOnlyList<string>, IReadOnlyList<string>?> change)
    {
        var count = 0;
        var sidecars = _store.Read().ToList();

        for (var index = 0; index < sidecars.Count; index++)
        {
            if (change(sidecars[index].Tags) is { } changed)
            {
                sidecars[index] = sidecars[index] with { Tags = changed, UpdatedUtc = DateTimeOffset.UtcNow };
                count++;
            }
        }

        if (count > 0)
        {
            _store.Write(sidecars);
        }

        var settings = _settings.Read();
        var projects = new Dictionary<string, IReadOnlyList<string>>(settings.ProjectTags, StringComparer.OrdinalIgnoreCase);
        var projectsChanged = 0;

        foreach (var (name, tags) in settings.ProjectTags)
        {
            if (change(tags) is { } changed)
            {
                projects[name] = changed;
                projectsChanged++;
            }
        }

        if (projectsChanged > 0)
        {
            _settings.Write(settings with { ProjectTags = projects });

            foreach (var card in _board.ProjectCards)
            {
                card.Tags = OfProject(card.Name);
            }
        }

        return count + projectsChanged;
    }
}
