using System.Collections.ObjectModel;
using Theseus.Core.Domain;
using Theseus.Core.Finding;
using Theseus.Core.Links;
using Theseus.Core.Settings;

namespace Theseus.App.ViewModels;

/// <summary>
/// The links between workstreams on the board (Nigel, 2026-10-21): the Chains tab beside Locks, the
/// link line on every card, and the Related section of the drawer. Links are made by hand here;
/// sessions only report on them, and a report waits under Needs you for Nigel's Confirm. Ready is
/// shown and counted, never acted on: nothing in here can start a session.
/// </summary>
public sealed class ChainsViewModel : ObservableObject
{
    private readonly LinkBook _book;
    private readonly Func<IReadOnlyList<WorkstreamCardViewModel>> _cards;
    private readonly Func<string> _briefFolderName;
    private readonly Action _changed;
    private readonly Action<string> _showCard;
    private LinkStatus _status = new([], []);
    private string _treeShape = string.Empty;
    private Dictionary<string, ChainNodeViewModel> _shown = new(StringComparer.OrdinalIgnoreCase);
    private string _needsShape = string.Empty;
    private readonly Dictionary<string, DateTimeOffset> _setAside = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _seenReports = new(StringComparer.Ordinal);
    private bool _reportsSeeded;
    private string? _focusKey;
    private bool _isOpen;
    private LinkTarget? _target;
    private LinkKindChoice _kind = LinkKindChoice.All[0];
    private string _output = string.Empty;
    private string _suggestedOutput = string.Empty;
    private string _linkRefusal = string.Empty;
    private bool _isAddingLink;
    private ProposalsViewModel? _proposals;
    private const string GroupPrefix = "project:";
    private ISettingsStore? _settings;
    private ChainFilters _filters = new();
    private Dictionary<string, bool> _folds = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, bool>? _foldsBeforeSearch;
    private bool _folding;
    private string _searchText = string.Empty;
    private Dictionary<string, ChainRowFacts> _facts = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, SearchableCard> _searchable = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _projects = new(StringComparer.OrdinalIgnoreCase);
    private List<ChainEntry> _entries = [];
    private readonly List<ChainNodeViewModel> _groups = [];
    private ChainViewResult _view = new(new Dictionary<string, ChainVisibility>(), 0, 0, 0, 0, 0, []);
    private RelayCommand? _clearSearch;
    private RelayCommand? _clearFilters;

    /// <summary>Creates the view over a link book.</summary>
    /// <param name="cards">The cards on the board, read on every update.</param>
    /// <param name="briefFolderName">The board's brief folder, relative to a working directory.</param>
    /// <param name="changed">Called after a link changes, so the board refreshes.</param>
    /// <param name="showCard">Opens a card's details, by key.</param>
    public ChainsViewModel(LinkBook book, Func<IReadOnlyList<WorkstreamCardViewModel>> cards,
        Func<string> briefFolderName, Action changed, Action<string> showCard)
    {
        _book = book;
        _cards = cards;
        _briefFolderName = briefFolderName;
        _changed = changed;
        _showCard = showCard;

        ToggleCommand = new RelayCommand(_ => IsOpen = !IsOpen);
        OpenCommand = new RelayCommand(_ => IsOpen = true);
        FoldAllCommand = new RelayCommand(_ => FoldEverything(false), _ => Roots.Count > 0);
        UnfoldAllCommand = new RelayCommand(_ => FoldEverything(true), _ => Roots.Count > 0);
        AddLinkCommand = new RelayCommand(_ => AddLink(), _ => _focusKey is not null && _target is not null);
        BeginAddLinkCommand = new RelayCommand(_ =>
        {
            ClearForm();
            IsAddingLink = true;
        }, _ => _focusKey is not null);
        CancelAddLinkCommand = new RelayCommand(_ =>
        {
            ClearForm();
            IsAddingLink = false;
        });
    }

    // Fold all and Unfold all replace what was remembered with the one choice, for every row (criterion 16).
    private void FoldEverything(bool expanded)
    {
        _folding = true;
        _folds.Clear();

        foreach (var node in AllRows())
        {
            node.IsExpanded = expanded;
            _folds[node.Key] = expanded;

            if (_foldsBeforeSearch is not null)
            {
                _foldsBeforeSearch[node.Key] = expanded;
            }
        }

        _folding = false;
        SaveFolds();
    }

    /// <summary>
    /// Where the panel keeps the owner's filters and folds between runs; read when set. Without one, they last as long as
    /// the view (chains-panel-at-scale criteria 11 and 16).
    /// </summary>
    public ISettingsStore? Settings
    {
        get => _settings;
        set
        {
            _settings = value;

            if (value?.Read() is { } read)
            {
                _filters = read.ChainsFilters;
                _folds = new Dictionary<string, bool>(read.ChainsFolds, StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>Whether a start of the card, by key, waits for the owner's Allow. Set by the app; null means none does.</summary>
    public Func<string, bool>? StartAsked { get; set; }

    /// <summary>A project's title from its name, for its header. Set by the app; null shows the name.</summary>
    public Func<string, string>? ProjectTitle { get; set; }

    /// <summary>What is typed in the panel's search box (criterion 2). Not remembered between runs.</summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (Set(ref _searchText, value ?? string.Empty))
            {
                ApplyView();
            }
        }
    }

    /// <summary>Clears the search box.</summary>
    public RelayCommand ClearSearchCommand => _clearSearch ??= new RelayCommand(_ => SearchText = string.Empty);

    /// <summary>Keep only cards with a session or something waiting on the owner, and the cards above them.</summary>
    public bool ActiveOnly
    {
        get => _filters.ActiveOnly;
        set => SetFilters(_filters with { ActiveOnly = value });
    }

    /// <summary>Keep only cards with something waiting on the owner, and the cards above them.</summary>
    public bool WaitingOnMe
    {
        get => _filters.WaitingOnMe;
        set => SetFilters(_filters with { WaitingOnMe = value });
    }

    /// <summary>Hide a whole chain once every card in it is Done or archived and nothing in it waits on the owner.</summary>
    public bool HideCompleted
    {
        get => _filters.HideCompleted;
        set => SetFilters(_filters with { HideCompleted = value });
    }

    /// <summary>Turns every filter off.</summary>
    public RelayCommand ClearFiltersCommand => _clearFilters ??= new RelayCommand(_ => SetFilters(new ChainFilters(false, false, false)));

    /// <summary>The Active only menu item, with what it hides on its own.</summary>
    public string ActiveOnlyText => $"Active only ({_view.HiddenByActiveOnly})";

    /// <summary>The Waiting on me menu item, with what it hides on its own.</summary>
    public string WaitingOnMeText => $"Waiting on me ({_view.HiddenByWaitingOnMe})";

    /// <summary>The Hide completed menu item, with what it hides on its own.</summary>
    public string HideCompletedText => $"Hide completed ({_view.HiddenByHideCompleted})";

    /// <summary>The Filter button: "Filter", or "Filter · 2" while two are on.</summary>
    public string FilterButtonText => _filters.Count == 0 ? "Filter" : $"Filter · {_filters.Count}";

    /// <summary>What a screen reader hears of the Filter button's state: "2 filters on", or "No filters on" (criterion 21).</summary>
    public string FilterHelpText => _filters.Count switch
    {
        0 => "No filters on",
        1 => "1 filter on",
        var count => $"{count} filters on",
    };

    /// <summary>
    /// What the search and filters hide, as "Showing 14 of 52 · 31 hidden by Hide completed · 7 by Active only"; empty when
    /// nothing narrows the tree (criterion 9).
    /// </summary>
    public string HiddenSummary
    {
        get
        {
            if (_filters.Count == 0 && string.IsNullOrWhiteSpace(_searchText))
            {
                return string.Empty;
            }

            var hidden = new[]
            {
                (_filters.HideCompleted, _view.HiddenByHideCompleted, "Hide completed"),
                (_filters.ActiveOnly, _view.HiddenByActiveOnly, "Active only"),
                (_filters.WaitingOnMe, _view.HiddenByWaitingOnMe, "Waiting on me"),
            }.Where(filter => filter.Item1 && filter.Item2 > 0).Select((filter, index) =>
                index == 0 ? $"{filter.Item2} hidden by {filter.Item3}" : $"{filter.Item2} by {filter.Item3}");

            return string.Join(" · ", new[] { $"Showing {_view.Shown} of {_view.Total}" }.Concat(hidden));
        }
    }

    /// <summary>"No chain card matches 'xyz'" while a search finds nothing; empty otherwise (criterion 5).</summary>
    public string NoMatchText => !SearchQuery.Parse(_searchText).IsEmpty && _view.Total > 0 && _view.Shown == 0
        ? $"No chain card matches '{_searchText.Trim()}'"
        : string.Empty;

    /// <summary>Said in place of the tree when the filters alone hide every chain (criterion 10).</summary>
    public string AllHiddenText => SearchQuery.Parse(_searchText).IsEmpty && _filters.Count > 0 && _view.Total > 0 && _view.Shown == 0
        ? "Every chain is hidden by your filters"
        : string.Empty;

    /// <summary>The Ready cards the panel's search keeps (criterion 4).</summary>
    public ObservableCollection<ChainNodeViewModel> ShownReady { get; } = [];

    private void SetFilters(ChainFilters filters)
    {
        if (filters == _filters)
        {
            return;
        }

        _filters = filters;

        if (Settings is { } settings)
        {
            settings.Write(settings.Read() with { ChainsFilters = filters });
        }

        Raise(nameof(ActiveOnly));
        Raise(nameof(WaitingOnMe));
        Raise(nameof(HideCompleted));
        ApplyView();
    }

    /// <summary>The links read against the board as of the last update.</summary>
    public LinkStatus Status => _status;

    /// <summary>Whether the Chains tab is open at the side of the board.</summary>
    public bool IsOpen
    {
        get => _isOpen;
        set => Set(ref _isOpen, value);
    }

    /// <summary>Opens or closes the Chains tab.</summary>
    public RelayCommand ToggleCommand { get; }

    /// <summary>Opens the Chains tab, from a card's link line.</summary>
    public RelayCommand OpenCommand { get; }

    /// <summary>Folds every row of the tree (walkthrough row 11; W17's Fold, W25's F165).</summary>
    public RelayCommand FoldAllCommand { get; }

    /// <summary>Unfolds every row of the tree (walkthrough row 11; W17's Unfold, W25's F164).</summary>
    public RelayCommand UnfoldAllCommand { get; }

    /// <summary>One tree per card with links and no parent.</summary>
    public ObservableCollection<ChainNodeViewModel> Roots { get; } = [];

    /// <summary>The cards whose every input has been accepted and which have not run since.</summary>
    public ObservableCollection<ChainNodeViewModel> Ready { get; } = [];

    /// <summary>Reports waiting for Nigel's Confirm, and reports lost; set-aside ones last.</summary>
    public ObservableCollection<NeedsYouRowViewModel> NeedsYou { get; } = [];

    /// <summary>Every link ever made, removed ones included, for what is read from links elsewhere, such as a verifier having ended.</summary>
    public IReadOnlyList<Link> AllLinks => _book.All;

    /// <summary>Told, in words, when a report arrives that the board has not shown before.</summary>
    public Action<string>? ReportArrived { get; set; }

    /// <summary>Told when the owner confirms a report, after the link is satisfied (verifier-card AC 13).</summary>
    public Action<Link>? ReportConfirmed { get; set; }

    /// <summary>Whether no card has a link yet, so the tab explains how to make one.</summary>
    public bool HasNoChains => Roots.Count == 0;

    /// <summary>The workstreams sessions have proposed, shown at the top of the tab.</summary>
    public ProposalsViewModel? Proposals
    {
        get => _proposals;
        set
        {
            if (_proposals is not null)
            {
                _proposals.Updated -= ProposalsUpdated;
            }

            _proposals = value;

            if (_proposals is not null)
            {
                _proposals.Updated += ProposalsUpdated;
            }

            Raise(nameof(Proposals));
            RaiseButton();
        }
    }

    /// <summary>The command bar button's label: "Chains", and how many proposals and cards wait.</summary>
    public string ButtonText => string.Join(" · ", new[]
    {
        "Chains",
        _proposals?.Pending.Count is > 0 and var proposed ? $"{proposed} proposed" : string.Empty,
        NeedsYou.Count == 0 ? string.Empty : $"{NeedsYou.Count} need you",
        Ready.Count == 0 ? string.Empty : $"{Ready.Count} ready",
    }.Where(part => part.Length > 0));

    /// <summary>The toolbar's count: proposals waiting, cards that need you, and cards ready to start.</summary>
    public int BadgeCount => (_proposals?.Pending.Count ?? 0) + NeedsYou.Count + Ready.Count;

    /// <summary>Whether the toolbar's count should turn amber: something waits on the user.</summary>
    public bool IsUrgent => (_proposals?.Pending.Count ?? 0) > 0 || NeedsYou.Count > 0;

    private void ProposalsUpdated(object? sender, EventArgs e) => RaiseButton();

    private void RaiseButton()
    {
        Raise(nameof(ButtonText));
        Raise(nameof(BadgeCount));
        Raise(nameof(IsUrgent));
    }

    /// <summary>The links of the card open in the drawer.</summary>
    public ObservableCollection<RelatedLinkViewModel> Related { get; } = [];

    /// <summary>The card the open card takes over from: one at most.</summary>
    public ObservableCollection<RelatedLinkViewModel> Parents { get; } = [];

    /// <summary>The cards the open card hands over to.</summary>
    public ObservableCollection<RelatedLinkViewModel> Children { get; } = [];

    /// <summary>The cards that report back to the open card.</summary>
    public ObservableCollection<RelatedLinkViewModel> Inputs { get; } = [];

    /// <summary>The card the open card reports back to.</summary>
    public ObservableCollection<RelatedLinkViewModel> Outputs { get; } = [];

    /// <summary>Whether the open card has no links at all.</summary>
    public bool HasNoLinks => Related.Count == 0;

    /// <summary>Whether the form for a new link is showing.</summary>
    public bool IsAddingLink
    {
        get => _isAddingLink;
        private set => Set(ref _isAddingLink, value);
    }

    /// <summary>Shows the form for a new link.</summary>
    public RelayCommand BeginAddLinkCommand { get; }

    /// <summary>Hides the form for a new link, forgetting what was chosen in it.</summary>
    public RelayCommand CancelAddLinkCommand { get; }

    /// <summary>The cards "Link to…" can link the open card to.</summary>
    public ObservableCollection<LinkTarget> Targets { get; } = [];

    /// <summary>The ways "Link to…" can link them.</summary>
    public IReadOnlyList<LinkKindChoice> Kinds => LinkKindChoice.All;

    /// <summary>The card chosen to link to.</summary>
    public LinkTarget? Target
    {
        get => _target;
        set
        {
            if (Set(ref _target, value))
            {
                SuggestOutput();
            }
        }
    }

    /// <summary>How the link joins them.</summary>
    public LinkKindChoice Kind
    {
        get => _kind;
        set
        {
            if (Set(ref _kind, value))
            {
                SuggestOutput();
            }
        }
    }

    /// <summary>
    /// The document one card writes and the other reads: a hand-over the child waits on, or a report
    /// back. Left empty, the link holds nothing back.
    /// </summary>
    public string Output
    {
        get => _output;
        set => Set(ref _output, value);
    }

    /// <summary>Why the last link could not be made, or nothing.</summary>
    public string LinkRefusal
    {
        get => _linkRefusal;
        private set => Set(ref _linkRefusal, value);
    }

    /// <summary>Makes the link chosen in "Link to…".</summary>
    public RelayCommand AddLinkCommand { get; }

    /// <summary>The pairs of cards joined by a standing link, for the clash check.</summary>
    public IReadOnlyCollection<(string From, string To)> LinkedPairs =>
        [.. _status.Links.Select(link => (link.From, link.To))];

    /// <summary>Every key a link has ever named, so a new card never takes a deleted card's key.</summary>
    public IReadOnlyCollection<string> TakenKeys =>
        [.. _book.All.SelectMany(link => new[] { link.From, link.To }).Concat(Proposals?.TakenKeys ?? []).Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>The links as a fresh session of this card should be told them.</summary>
    public string RelatedParagraph(string key) => RelatedWorkstreams.Paragraph(key, Current());

    /// <summary>
    /// The chain a card is in, named by the title of the card it starts from, or null when the card
    /// is in no chain. The board's Rows by > Chain view groups by this.
    /// </summary>
    public string? ChainName(string key)
    {
        if (!ChainRoots.Of(_book.Active).TryGetValue(key, out var root))
        {
            return null;
        }

        return _cards().FirstOrDefault(card => string.Equals(card.Key, root, StringComparison.OrdinalIgnoreCase))?.Title ?? root;
    }

    /// <summary>What changed in this card's links since its last session started.</summary>
    public string RelatedSince(string key, DateTimeOffset lastSessionStartedUtc) =>
        RelatedWorkstreams.Since(key, Current(), lastSessionStartedUtc);

    /// <summary>The waiting cards among these, which a Start holds back and asks about.</summary>
    public IReadOnlyList<Workstream> Waiting(IReadOnlyList<Workstream> workstreams) =>
        WaitingStarts.Among(workstreams, _status);

    /// <summary>What the strip says about held-back cards.</summary>
    public string WaitingSummary(IReadOnlyList<Workstream> held) => WaitingStarts.Summary(held, _status);

    /// <summary>
    /// Links a card just created as a follow-on to the card it came from: a hand-over with no document,
    /// since the follow-on starts from what Nigel wrote for it, and a report back when one was asked for.
    /// </summary>
    public void FollowedOn(string parentKey, string childKey, string? reportTo)
    {
        Report(_book.Add(LinkKind.HandsOver, parentKey, childKey));

        if (reportTo is not null)
        {
            Report(_book.Add(LinkKind.ReportsBack, childKey, parentKey, output: reportTo));
        }

        _changed();
    }

    /// <summary>Removes every link of a card that has been deleted.</summary>
    public void Deleted(string key)
    {
        _book.RemoveAllFor(key);
        _changed();
    }

    /// <summary>Shows a card's links in the drawer, and offers the others to link it to.</summary>
    public void ShowFor(string? key)
    {
        _focusKey = key;
        ClearForm();
        IsAddingLink = false;

        Targets.Clear();

        foreach (var card in _cards()
                     .Where(card => !card.IsArchived && !string.Equals(card.Key, key, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(card => card.Title, StringComparer.CurrentCultureIgnoreCase))
        {
            Targets.Add(new LinkTarget(card.Key, card.Title));
        }

        UpdateRelated();
    }

    /// <summary>
    /// Reads the links against the board as it now stands, and brings the cards, the tree and the
    /// drawer into line. The tree is rebuilt only when its shape changes, so an unchanged board raises
    /// nothing for UI Automation to report.
    /// </summary>
    public void Update()
    {
        var cards = _cards();

        _status = Current(cards);

        foreach (var card in cards)
        {
            card.Links = LinkLine.For(card.Key, _status);
        }

        var rows = cards.Where(card => _status.For(card.Key).Count > 0).ToList();
        var shared = rows.GroupBy(card => card.Title, StringComparer.CurrentCultureIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        var nodes = rows.ToDictionary(card => card.Key, card => Node(card, shared.Contains(card.Title)), StringComparer.OrdinalIgnoreCase);
        _facts = rows.ToDictionary(card => card.Key, Facts, StringComparer.OrdinalIgnoreCase);
        _searchable = rows.ToDictionary(card => card.Key, card => card.Searchable, StringComparer.OrdinalIgnoreCase);
        _projects = rows.ToDictionary(card => card.Key, card => card.Workstream.Project ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        // The session's mark, the row's state and Focus are left out: they change on every turn, and are updated on the rows in place.
        var shape = string.Join("|", nodes.Values.Select(node =>
            $"{node.Key}:{node.Title}:{node.State}:{node.WaitsOn}:{node.ReportsBack}:{Parent(node.Key)}:{node.Copy}:{_projects[node.Key]}"));

        if (shape != _treeShape)
        {
            _treeShape = shape;
            Rebuild(nodes);
            _shown = nodes;
        }
        else
        {
            foreach (var (key, fresh) in nodes)
            {
                if (_shown.TryGetValue(key, out var shown))
                {
                    shown.Session = fresh.Session;
                    shown.Focus = fresh.HasFocus == shown.HasFocus ? shown.Focus : fresh.Focus;
                }
            }
        }

        foreach (var (key, node) in _shown)
        {
            if (_facts.TryGetValue(key, out var facts))
            {
                node.RowState = ChainRowStates.Of(facts);
            }
        }

        // Read again on every update, not only on a rebuild: a session starting, a card finishing or a tag added changes
        // what the filters and the search keep without changing the tree's shape.
        _entries = [.. TopChains().Select(Entry)];
        ApplyView();
        UpdateNeedsYou(cards);
        AnnounceReports();
        UpdateRelated();
    }

    /// <summary>Outlines the cards linked to this one while the pointer is over it, or clears the outline.</summary>
    public void Highlight(string? key)
    {
        var linked = key is null
            ? []
            : _status.For(key).Select(link => Same(link.From, key) ? link.To : link.From)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var card in _cards())
        {
            card.IsLinkHighlighted = linked.Contains(card.Key);
        }
    }

    private LinkStatus Current() => Current(_cards());

    private LinkStatus Current(IReadOnlyList<WorkstreamCardViewModel> cards) =>
        new(_book.Active, cards.Select(card => new LinkCard(card.Key, card.Title, card.IsArchived,
            card.IsBlocked || card.IsPaused, card.IsLive, card.LastStartedUtc, card.IsInLastStage, card.LastRunCrashed)));

    private void UpdateNeedsYou(IReadOnlyList<WorkstreamCardViewModel> cards)
    {
        var rows = _status.Links
            .Select(link => (Link: link, State: _status.StateOf(link)))
            .Where(item => item.State is LinkState.Reported or LinkState.ReportLost)
            .Select(item => (item.Link, item.State,
                SetAside: item.Link.LatestReport is { } report && _setAside.TryGetValue(item.Link.Id, out var at) && at == report.AtUtc))
            .OrderBy(item => item.SetAside)
            .ThenByDescending(item => item.Link.LatestReport?.AtUtc ?? item.Link.CreatedUtc)
            .ToList();

        var shape = string.Join("|", rows.Select(row => $"{row.Link.Id}:{row.State}:{row.Link.LatestReport?.AtUtc:o}:{row.SetAside}"));

        if (shape == _needsShape)
        {
            return;
        }

        _needsShape = shape;
        NeedsYou.Clear();

        foreach (var (link, state, setAside) in rows)
        {
            var writer = cards.FirstOrDefault(card => Same(card.Key, link.From));

            NeedsYou.Add(new NeedsYouRowViewModel(link, state, $"{Title(link.From)} → {Title(link.To)}",
                Detail(link, state, writer), writer?.ColorHex ?? "#808080",
                writer is null ? null : Theseus.Core.Stopping.HandoffPath.For(writer.Workstream),
                setAside, Confirm, Satisfy, SetAside, Reject));
        }

        RaiseButton();
    }

    private static string Detail(Link link, LinkState state, WorkstreamCardViewModel? writer)
    {
        if (state == LinkState.Reported && link.LatestReport is { } report)
        {
            return $"reported {report.AtUtc.ToLocalTime():ddd HH:mm}"
                   + (report.Verdict is { } verdict ? $" · verdict {verdict}" : " · no verdict")
                   + $" · {System.IO.Path.GetFileName(report.Document)}";
        }

        return writer switch
        {
            { IsArchived: true } => "no report, and the card is archived",
            { IsInLastStage: true } => "no report, and the card is in Done",
            { LastRunCrashed: true } => "no report, and its last run ended unexpectedly",
            _ => "no report",
        };
    }

    private void AnnounceReports()
    {
        foreach (var link in _status.Links.Where(link => link.LatestReport is not null && link.SatisfiedUtc is null))
        {
            var at = link.LatestReport!.AtUtc;

            if (_seenReports.TryGetValue(link.Id, out var seen) && seen == at)
            {
                continue;
            }

            _seenReports[link.Id] = at;

            if (_reportsSeeded)
            {
                ReportArrived?.Invoke($"{Title(link.From)} reported to {Title(link.To)}"
                                      + (link.LatestReport.Verdict is { } verdict ? $" ({verdict})" : string.Empty)
                                      + ". Confirm it in Chains.");
            }
        }

        _reportsSeeded = true;
    }

    private void SetAside(NeedsYouRowViewModel row)
    {
        if (row.Link.LatestReport is { } report)
        {
            _setAside[row.Link.Id] = report.AtUtc;
            UpdateNeedsYou(_cards());
        }
    }

    /// <summary>Brings a card's tab forward, by key: the row's ⌖ Focus (item 8). Set by the app; null shows no Focus.</summary>
    public Action<string>? FocusCard { get; set; }

    private ChainNodeViewModel Node(WorkstreamCardViewModel card, bool titleShared)
    {
        var readiness = _status.ReadinessOf(card.Key);
        var inputs = _status.Inputs(card.Key);
        var reportsTo = _status.For(card.Key)
            .FirstOrDefault(link => link.Kind == LinkKind.ReportsBack && Same(link.From, card.Key));

        return new ChainNodeViewModel(
            card.Key,
            card.Title,
            card.ColorHex,
            readiness switch
            {
                CardReadiness.Ready => "Ready",
                CardReadiness.Waiting => "⧗ waiting",
                _ => string.Empty,
            },
            readiness == CardReadiness.Ready,
            inputs.Count == 0
                ? string.Empty
                : "waits on: " + string.Join(", ", inputs.Select(link => $"{Title(link.From)} {Mark(_status.StateOf(link))}")),
            reportsTo is null ? string.Empty : $"↩ reports back to {Title(reportsTo.To)} {Mark(_status.StateOf(reportsTo))}",
            new RelayCommand(_ => _showCard(card.Key)))
        {
            Session = SessionMark.Of(card.Lifecycle),
            // Launching has no session to bring forward yet: its mark shows, and its Focus once it is running.
            Focus = FocusCard is { } focus && SessionMark.HasSession(card.Lifecycle) && card.Lifecycle != SessionLifecycle.Launching
                ? new RelayCommand(_ => focus(card.Key))
                : null,
            Copy = titleShared ? card.Key : string.Empty,
        };
    }

    private void Rebuild(Dictionary<string, ChainNodeViewModel> nodes)
    {
        Roots.Clear();
        Ready.Clear();
        _groups.Clear();
        _foldsBeforeSearch = null;

        List<ChainNodeViewModel> chains = [];

        foreach (var node in nodes.Values)
        {
            if (Parent(node.Key) is { } parent && nodes.TryGetValue(parent, out var parentNode))
            {
                parentNode.Children.Add(node);
            }
            else
            {
                chains.Add(node);
            }

            if (node.IsReady)
            {
                Ready.Add(node);
            }

            node.PropertyChanged += RowFoldChanged;
        }

        _entries = [.. chains.Select(Entry)];
        _folding = true;

        foreach (var (node, entry) in chains.Zip(_entries))
        {
            SetInitialFolds(node, entry);
        }

        var byProject = chains.GroupBy(node => _projects[node.Key], StringComparer.OrdinalIgnoreCase).ToList();

        if (byProject.Count > 1 && byProject.Any(group => group.Key.Length > 0))
        {
            foreach (var group in byProject
                         .OrderBy(group => group.Key.Length == 0)
                         .ThenBy(group => TitleOfProject(group.Key), StringComparer.CurrentCultureIgnoreCase))
            {
                var header = Header(group.Key, group.Count() + group.Sum(node => Count(node.Children)));

                foreach (var node in group)
                {
                    header.Children.Add(node);
                }

                header.IsExpanded = !_folds.TryGetValue(header.Key, out var unfolded) || unfolded;
                header.PropertyChanged += RowFoldChanged;
                _groups.Add(header);
                Roots.Add(header);
            }
        }
        else
        {
            foreach (var node in chains)
            {
                Roots.Add(node);
            }
        }

        _folding = false;
        RaiseButton();
        Raise(nameof(HasNoChains));
    }

    private static int Count(IEnumerable<ChainNodeViewModel> nodes) => nodes.Sum(node => 1 + Count(node.Children));

    private ChainNodeViewModel Header(string project, int count)
    {
        var title = project.Length == 0 ? "Not in a project" : TitleOfProject(project);

        return new ChainNodeViewModel(GroupPrefix + project, $"{title} · {count}", "#00000000", string.Empty, false,
            string.Empty, string.Empty, new RelayCommand(_ => { }))
        {
            IsGroup = true,
        };
    }

    private string TitleOfProject(string project) => ProjectTitle?.Invoke(project) ?? project;

    private ChainEntry Entry(ChainNodeViewModel node) =>
        new(_searchable[node.Key], _facts[node.Key], [.. node.Children.Select(Entry)]);

    // A row the owner folded or unfolded keeps that; any other starts folded when everything below it is finished.
    private void SetInitialFolds(ChainNodeViewModel node, ChainEntry entry)
    {
        node.IsExpanded = _folds.TryGetValue(node.Key, out var unfolded) ? unfolded : !ChainView.IsFinishedBelow(entry);

        foreach (var (child, childEntry) in node.Children.Zip(entry.Children))
        {
            SetInitialFolds(child, childEntry);
        }
    }

    private void RowFoldChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_folding || e.PropertyName != nameof(ChainNodeViewModel.IsExpanded) || sender is not ChainNodeViewModel node)
        {
            return;
        }

        _folds[node.Key] = node.IsExpanded;

        if (_foldsBeforeSearch is not null)
        {
            _foldsBeforeSearch[node.Key] = node.IsExpanded;
        }

        SaveFolds();
    }

    private void SaveFolds()
    {
        if (Settings is { } settings)
        {
            settings.Write(settings.Read() with { ChainsFolds = new Dictionary<string, bool>(_folds, StringComparer.OrdinalIgnoreCase) });
        }
    }

    private ChainRowFacts Facts(WorkstreamCardViewModel card)
    {
        var written = _status.Links.Where(link => Same(link.From, card.Key)).Select(link => _status.StateOf(link)).ToList();

        return new ChainRowFacts(card.Lifecycle)
        {
            IsPaused = card.IsPaused,
            IsBlocked = card.IsBlocked,
            IsArchived = card.IsArchived,
            IsDone = card.IsDone,
            Readiness = _status.ReadinessOf(card.Key),
            ReportWaiting = written.Contains(LinkState.Reported),
            ReportLost = written.Contains(LinkState.ReportLost),
            StartAsked = StartAsked?.Invoke(card.Key) == true,
        };
    }

    // The search and filters over the tree, as they stand: what shows, what each filter hides, and the folds a search needs.
    private void ApplyView()
    {
        var query = SearchQuery.Parse(_searchText);
        var view = ChainView.Apply(_entries, query, _filters);

        foreach (var (key, node) in _shown)
        {
            var visibility = view.Of(key);
            node.IsShown = visibility != ChainVisibility.Hidden;
            node.IsContext = visibility == ChainVisibility.Context;
            node.TitleHighlights = visibility == ChainVisibility.Shown ? CardSearch.Highlights(node.Title, query.Words) : [];
        }

        foreach (var header in _groups)
        {
            header.IsShown = header.Children.Any(node => node.IsShown);
        }

        ApplySearchFolds(query, view);

        _view = view;
        var search = new CardFilter(query, [], TagMatch.Any);
        var ready = Ready.Where(node => _searchable.TryGetValue(node.Key, out var card) && search.Admits(card)).ToList();

        if (!ready.SequenceEqual(ShownReady))
        {
            ShownReady.Clear();

            foreach (var node in ready)
            {
                ShownReady.Add(node);
            }
        }

        Raise(nameof(HiddenSummary));
        Raise(nameof(ActiveOnlyText));
        Raise(nameof(WaitingOnMeText));
        Raise(nameof(HideCompletedText));
        Raise(nameof(FilterButtonText));
        Raise(nameof(FilterHelpText));
        Raise(nameof(NoMatchText));
        Raise(nameof(AllHiddenText));
    }

    // A search unfolds the rows above its matches for as long as it lasts, and the folds go back as they were when it is
    // cleared; the remembered folds are never changed by it.
    private void ApplySearchFolds(SearchQuery query, ChainViewResult view)
    {
        _folding = true;

        if (!query.IsEmpty)
        {
            _foldsBeforeSearch ??= AllRows().ToDictionary(node => node.Key, node => node.IsExpanded, StringComparer.OrdinalIgnoreCase);

            foreach (var node in AllRows().Where(node => view.Unfold.Contains(node.Key, StringComparer.OrdinalIgnoreCase)
                                                         || (node.IsGroup && node.IsShown)))
            {
                node.IsExpanded = true;
            }
        }
        else if (_foldsBeforeSearch is { } before)
        {
            foreach (var node in AllRows())
            {
                if (before.TryGetValue(node.Key, out var unfolded))
                {
                    node.IsExpanded = unfolded;
                }
            }

            _foldsBeforeSearch = null;
        }

        _folding = false;
    }

    private IEnumerable<ChainNodeViewModel> AllRows() => Flatten(Roots);

    private IEnumerable<ChainNodeViewModel> TopChains() => Roots.SelectMany(row => row.IsGroup ? row.Children : [row]);

    private static IEnumerable<ChainNodeViewModel> Flatten(IEnumerable<ChainNodeViewModel> nodes) =>
        nodes.SelectMany(node => new[] { node }.Concat(Flatten(node.Children)));

    private string? Parent(string key) => _status.ParentLink(key)?.From;

    private void UpdateRelated()
    {
        Related.Clear();
        Parents.Clear();
        Children.Clear();
        Inputs.Clear();
        Outputs.Clear();

        if (_focusKey is { } key)
        {
            foreach (var link in _status.For(key).OrderBy(link => link.CreatedUtc))
            {
                var here = string.Equals(link.From, key, StringComparison.OrdinalIgnoreCase);
                var other = here ? link.To : link.From;
                var row = new RelatedLinkViewModel(link, Describe(key, link), _status.StateOf(link), Satisfy, Remove,
                    other, Title(other), new RelayCommand(_ => _showCard(other)), Confirm);

                var group = (link.Kind, here) switch
                {
                    (LinkKind.HandsOver, false) => Parents,
                    (LinkKind.HandsOver, true) => Children,
                    (LinkKind.ReportsBack, false) => Inputs,
                    _ => Outputs,
                };

                Related.Add(row);
                group.Add(row);
            }
        }

        Raise(nameof(HasNoLinks));
    }

    private void ClearForm()
    {
        LinkRefusal = string.Empty;
        Target = null;
        Output = string.Empty;
        Kind = LinkKindChoice.All[0];
    }

    private string Describe(string key, Link link)
    {
        var here = string.Equals(link.From, key, StringComparison.OrdinalIgnoreCase);
        var other = Title(here ? link.To : link.From);

        return (link.Kind, here) switch
        {
            (LinkKind.HandsOver, true) => $"Hands over to {other}",
            (LinkKind.HandsOver, false) => $"Takes over from {other}",
            (LinkKind.ReportsBack, true) => $"Reports back to {other}",
            _ => $"{other} reports back to this card",
        };
    }

    private string? Satisfy(string id, string reason) => Changed(_book.Satisfy(id, reason));

    /// <summary>Rejects a link's report for the reason given (git-22, 2026-11-23); it leaves NEEDS YOU on the update.</summary>
    public string? Reject(string id, string reason)
    {
        var refusal = Changed(_book.Reject(id, reason));

        if (refusal is null)
        {
            Update();
        }

        return refusal;
    }

    /// <summary>Retires a verifier's link for Clear up verifiers…: no report on it, now or later, counts as met.</summary>
    public string? Retire(string id, string reason) => Changed(_book.Retire(id, reason));

    /// <summary>Puts a link back exactly as it was, for Undo of Clear up verifiers….</summary>
    public string? Restore(Link before) => Changed(_book.Restore(before));

    private string? Confirm(string id)
    {
        var link = _book.All.FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.Ordinal));
        var refusal = Changed(_book.Confirm(id));

        if (refusal is null && link is not null)
        {
            ReportConfirmed?.Invoke(link);
        }

        return refusal;
    }

    private static string Mark(LinkState state) => state switch
    {
        LinkState.Satisfied => "✓",
        LinkState.Reported => "?",
        LinkState.ReportLost => "!",
        LinkState.Rejected => "✗",
        _ => "⧗",
    };

    private static bool Same(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private string? Remove(string id) => Changed(_book.Remove(id));

    /// <summary>
    /// Removes the standing hand-over from one card to another, and its report back with it; the reason it was refused,
    /// or null. For a box shown in a project only by that hand-over (Nigel, 2026-11-22).
    /// </summary>
    public string? RemoveHandOver(string from, string to) =>
        _book.Active.FirstOrDefault(link => link.Kind == LinkKind.HandsOver && Same(link.From, from) && Same(link.To, to)) is { } link
            ? Remove(link.Id)
            : $"{from} no longer hands over to {to}.";

    private string? Changed(LinkChange change)
    {
        if (change.Refusal is null)
        {
            _changed();
        }

        return change.Refusal;
    }

    private void AddLink()
    {
        if (_focusKey is not { } key || _target is not { } target)
        {
            return;
        }

        var (from, to) = _kind.ThisCardIsFrom ? (key, target.Key) : (target.Key, key);
        var change = _book.Add(_kind.Kind, from, to, output: Output);

        LinkRefusal = change.Refusal ?? string.Empty;

        if (change.Refusal is null)
        {
            ClearForm();
            IsAddingLink = false;
            _changed();
        }
    }

    private void SuggestOutput()
    {
        if (_target is null || _focusKey is null || (Output.Length > 0 && Output != _suggestedOutput))
        {
            return;
        }

        var writer = _kind.ThisCardIsFrom ? _focusKey : _target.Key;

        if (_cards().FirstOrDefault(card => string.Equals(card.Key, writer, StringComparison.OrdinalIgnoreCase)) is { } card)
        {
            _suggestedOutput = BriefFolder.ReportPath(card.Workstream.WorkingDirectory, _briefFolderName(), card.Key);
            Output = _suggestedOutput;
        }
    }

    private static void Report(LinkChange change)
    {
        if (change.Refusal is { } refusal)
        {
            Theseus.Core.Diagnostics.TheseusLog.Warning("A follow-on's link was refused: {Refusal}", refusal);
        }
    }

    private string Title(string key) => _status.Card(key)?.Title ?? key;
}
