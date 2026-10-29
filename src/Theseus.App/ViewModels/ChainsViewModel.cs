using System.Collections.ObjectModel;
using Theseus.Core.Domain;
using Theseus.Core.Links;

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

    /// <summary>One tree per card with links and no parent.</summary>
    public ObservableCollection<ChainNodeViewModel> Roots { get; } = [];

    /// <summary>The cards whose every input has been accepted and which have not run since.</summary>
    public ObservableCollection<ChainNodeViewModel> Ready { get; } = [];

    /// <summary>Reports waiting for Nigel's Confirm, and reports lost; set-aside ones last.</summary>
    public ObservableCollection<NeedsYouRowViewModel> NeedsYou { get; } = [];

    /// <summary>Told, in words, when a report arrives that the board has not shown before.</summary>
    public Action<string>? ReportArrived { get; set; }

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
            Raise(nameof(ButtonText));
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

    private void ProposalsUpdated(object? sender, EventArgs e) => Raise(nameof(ButtonText));

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
        [.. _book.All.SelectMany(link => new[] { link.From, link.To }).Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>The links as a fresh session of this card should be told them.</summary>
    public string RelatedParagraph(string key) => RelatedWorkstreams.Paragraph(key, Current());

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

        var nodes = cards
            .Where(card => _status.For(card.Key).Count > 0)
            .ToDictionary(card => card.Key, card => Node(card), StringComparer.OrdinalIgnoreCase);

        var shape = string.Join("|", nodes.Values.Select(node =>
            $"{node.Key}:{node.Title}:{node.State}:{node.WaitsOn}:{node.ReportsBack}:{Parent(node.Key)}"));

        if (shape != _treeShape)
        {
            _treeShape = shape;
            Rebuild(nodes);
        }

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
            card.IsBlocked, card.IsLive, card.LastStartedUtc, card.IsInLastStage, card.LastRunCrashed)));

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
                setAside, Confirm, Satisfy, SetAside));
        }

        Raise(nameof(ButtonText));
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

    private ChainNodeViewModel Node(WorkstreamCardViewModel card)
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
            new RelayCommand(_ => _showCard(card.Key)));
    }

    private void Rebuild(Dictionary<string, ChainNodeViewModel> nodes)
    {
        Roots.Clear();
        Ready.Clear();

        foreach (var node in nodes.Values)
        {
            if (Parent(node.Key) is { } parent && nodes.TryGetValue(parent, out var parentNode))
            {
                parentNode.Children.Add(node);
            }
            else
            {
                Roots.Add(node);
            }

            if (node.IsReady)
            {
                Ready.Add(node);
            }
        }

        Raise(nameof(ButtonText));
        Raise(nameof(HasNoChains));
    }

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

    private string? Confirm(string id) => Changed(_book.Confirm(id));

    private static string Mark(LinkState state) => state switch
    {
        LinkState.Satisfied => "✓",
        LinkState.Reported => "?",
        LinkState.ReportLost => "!",
        _ => "⧗",
    };

    private static bool Same(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private string? Remove(string id) => Changed(_book.Remove(id));

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
