using System.Collections.ObjectModel;
using Theseus.Core.Abstractions;
using Theseus.Core.Domain;
using Theseus.Core.Finding;
using Theseus.Core.Settings;
using Theseus.Core.Stopping;

namespace Theseus.App.ViewModels;

/// <summary>The board: lanes of cards, rebuilt from the plans on disk.</summary>
public sealed class BoardViewModel : ObservableObject
{
    private readonly IPlanStore _plans;
    private readonly IBoardStore _board;
    private readonly ISettingsStore _settings;
    private string _status = string.Empty;
    private WorkstreamCardViewModel? _selectedCard;
    private bool _showArchived;
    private BoardShows _shows;
    private bool _activeOnly;
    private string _activeOnlyNote = string.Empty;
    private RelayCommand? _toggleActiveOnly;
    private BoardColumnsBy _columnsBy;
    private BoardRowsBy _rowsBy;
    private List<WorkstreamCardViewModel> _all = [];
    private IBoardCard? _anchor;
    private IReadOnlyList<ProjectCardViewModel> _projects = [];
    private IReadOnlyDictionary<string, ProjectCardPlace>? _projectPlaces;
    private IReadOnlyList<Workstream> _archived = [];
    private IReadOnlyList<IBoardCard> _matches = [];
    private HashSet<IBoardCard> _matching = [];
    private IBoardCard? _current;

    /// <summary>Creates the board over its stores.</summary>
    public BoardViewModel(IPlanStore plans, IBoardStore board, ISettingsStore settings)
    {
        _plans = plans;
        _board = board;
        _settings = settings;
        _showArchived = settings.Read().ShowArchived;
        _shows = settings.Read().BoardShows;
        _activeOnly = settings.Read().ShowActiveOnly;
        _columnsBy = settings.Read().ColumnsBy;
        _rowsBy = settings.Read().RowsBy;
        Search = new BoardSearchViewModel(OnSearchChanged);
        Tags = new BoardTags(this, board, settings);

        SyncConfigured(ConfiguredLanes(), ConfiguredAreas());
        Place();

        RefreshCommand = new RelayCommand(_ => Refresh());
    }

    /// <summary>The search box and tag filter (card-search-and-tags): applied on every placing of the cards.</summary>
    public BoardSearchViewModel Search { get; }

    /// <summary>The owner's tags on the cards (card-search-and-tags items 9 to 13).</summary>
    public BoardTags Tags { get; }

    /// <summary>The cards and project cards the search matches, in reading order; empty with no search.</summary>
    public IReadOnlyList<IBoardCard> Matches => _matches;

    /// <summary>Whether the search matches this card or project card; true for everything with no search.</summary>
    public bool IsMatch(IBoardCard item) => !Search.IsOn || _matching.Contains(item);

    /// <summary>
    /// Selects the next match, or the one before, and returns it for the view to scroll to (item 6): the first from none,
    /// wrapping at either end. Null when nothing matches.
    /// </summary>
    public IBoardCard? StepMatch(bool forward)
    {
        if (!Search.IsOn)
        {
            return null;
        }

        var current = CurrentMatch is { } item ? IndexOf(item) : -1;
        var next = MatchCursor.Step(_matches.Count, current, forward);

        if (next < 0)
        {
            _current = null;

            return null;
        }

        _current = _matches[next];
        ClickSelect(_current, toggle: false, extend: false);

        return _current;
    }

    /// <summary>The match the keys last moved to, if it is still a match and still selected: what Ctrl+Enter opens.</summary>
    public IBoardCard? CurrentMatch => _current is { IsSelected: true } item && IndexOf(item) >= 0 ? item : null;

    private int IndexOf(IBoardCard item)
    {
        for (var index = 0; index < _matches.Count; index++)
        {
            if (ReferenceEquals(_matches[index], item))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Include archived (item 5): shows the archive with the search kept, when nothing on the board matches but an archived
    /// card would. The board shows the archive in place of what is being worked on, as View › Archive always has.
    /// </summary>
    public void IncludeArchived() => ShowArchived = true;

    private void OnSearchChanged()
    {
        _current = null;
        Place();
    }

    /// <summary>What the search reads of a project card: its name, title and tags.</summary>
    private static SearchableCard SearchableOf(ProjectCardViewModel project) =>
        new(project.Name, project.Title, string.Empty, string.Empty, [], project.Tags, [], []);

    /// <summary>
    /// Works out what the search matches before the cards are placed (items 2, 3, 7): a card by its own fields, a project
    /// card by its own or by any of its cards.
    /// </summary>
    private void Evaluate()
    {
        var filter = Search.Filter;

        if (!filter.IsOn)
        {
            _matching = [];

            return;
        }

        var matching = new HashSet<IBoardCard>(_all.Where(card => filter.Admits(card.Searchable)));

        foreach (var project in _projects)
        {
            if (filter.Admits(SearchableOf(project)) || KeysIn(project).Any(key => Card(key) is { } member && matching.Contains(member)))
            {
                matching.Add(project);
            }
        }

        _matching = matching;
    }

    /// <summary>
    /// The cards a project holds for the search: those its pieces name, and any card the project claims, such as a verifier
    /// of one of them, which leaves the grid with them (project-view-usability item 4: "Verify: Theseus" found 0 of 69), or
    /// its scenario writer (Nigel, 2026-11-26: "nothing shows when I search for write holdout").
    /// </summary>
    private IEnumerable<string> KeysIn(ProjectCardViewModel project) =>
        project.MemberKeys.Concat(_all
                .Where(card => string.Equals(card.Workstream.Project ?? card.ProjectOfRole, project.Name, StringComparison.OrdinalIgnoreCase))
                .Select(card => card.Key))
            .Distinct(StringComparer.OrdinalIgnoreCase);

    /// <summary>Hides what the search does not match while Hide the rest is on (item 3).</summary>
    private bool HiddenBySearch(IBoardCard item) => Search.IsOn && Search.HideRest && !_matching.Contains(item);

    /// <summary>
    /// Shows the search on every card, project card, cell and row after placing (items 3, 4, 5, 7), and counts what is on
    /// the board and what matches.
    /// </summary>
    private void ShowSearch()
    {
        var filter = Search.Filter;
        var on = filter.IsOn;
        var words = on ? filter.Query.Words : [];

        foreach (var card in _all)
        {
            // Only a match is highlighted: a dimmed card can hold a word yet fail the tags (item 3).
            var matched = on && _matching.Contains(card);
            card.ShowSearch(on && !matched, matched ? CardSearch.Highlights(card.Title, words) : []);
        }

        foreach (var project in _projects)
        {
            var inside = on
                ? KeysIn(project).Where(key => Card(key) is { } member && _matching.Contains(member)).ToList()
                : [];
            var projectMatched = on && _matching.Contains(project);
            project.ShowSearch(on && !projectMatched, projectMatched ? CardSearch.Highlights(project.Title, words) : [], inside);
        }

        var colours = _settings.Read().TagColours;
        string ColourOf(string tag) => colours.GetValueOrDefault(tag) ?? TagPalette.Colours[0];
        IReadOnlyList<TagChipViewModel> Chips(IReadOnlyList<string> tags) =>
            [.. TagDisplay.Visible(tags).Select(tag => new TagChipViewModel(tag, ColourOf(tag), Search.FilterByTagCommand))];

        foreach (var card in _all)
        {
            card.ShowTags(Chips(card.Tags), TagDisplay.More(card.Tags), TagDisplay.Rest(card.Tags));
        }

        foreach (var project in _projects)
        {
            project.ShowTags(Chips(project.Tags), TagDisplay.More(project.Tags), TagDisplay.Rest(project.Tags));
        }

        var matches = new List<IBoardCard>();

        foreach (var row in Rows)
        {
            var inRow = new List<IBoardCard>();

            foreach (var cell in row.Cells)
            {
                var inCell = on
                    ? cell.Projects.Cast<IBoardCard>().Concat(cell.Cards).Where(_matching.Contains).ToList()
                    : [];
                cell.MatchCount = inCell.Count;
                inRow.AddRange(inCell);
            }

            row.ShowMatches(inRow);
            matches.AddRange(inRow);
        }

        _matches = matches;

        var cardsOnGrid = _all.Where(card => KeepOutOfGrid?.Invoke(card) != true).Cast<IBoardCard>().ToList();
        var hiddenKind = Kept(BoardShows.Projects) ? cardsOnGrid : Kept(BoardShows.Workstreams) ? [.. _projects] : [];
        var shownKinds = cardsOnGrid.Concat(_projects).Except(hiddenKind).ToList();
        var onBoard = shownKinds.Where(item => !HiddenByActiveOnly(item)).ToList();
        var onScreen = onBoard.Count(item => !HiddenBySearch(item));
        Search.HiddenByShows = on ? hiddenKind.Count(_matching.Contains) : 0;
        Search.HiddenByActiveOnly = on ? shownKinds.Except(onBoard).Count(_matching.Contains) : 0;
        ActiveOnlyNote = _activeOnly && !ShowArchived ? $"Showing {onScreen} of {shownKinds.Count} cards · active only" : string.Empty;
        var archivedWouldMatch = on && !ShowArchived && _archived.Any(workstream => filter.Admits(new SearchableCard(
            workstream.Key, workstream.Title, workstream.Project ?? string.Empty, workstream.Area ?? string.Empty,
            workstream.JiraKeys, workstream.Tags, [], [])));
        Search.Counted(on ? onBoard.Count(_matching.Contains) : 0, onBoard.Count, archivedWouldMatch);
    }

    /// <summary>The stages, as configured: what a card's stage can be, whatever the columns show.</summary>
    public ObservableCollection<string> Stages { get; } = [];

    /// <summary>The headings across the top of the grid, one per column of the current view, each able to fold it away.</summary>
    public ObservableCollection<StageHeaderViewModel> StageHeaders { get; } = [];

    /// <summary>The rows of the current view, each holding a cell per column.</summary>
    public ObservableCollection<BoardRowViewModel> Rows { get; } = [];

    /// <summary>
    /// Every card loaded, whatever cell it is in, and a project's cards too, which are kept out of the grid while their
    /// project card stands for them (project-view AC 13): refreshing, starting and stopping still reach them. What the
    /// selection commands act on is <see cref="ShownCards"/>.
    /// </summary>
    public IEnumerable<WorkstreamCardViewModel> Cards => _all;

    /// <summary>The cards in the grid's cells, folded rows and columns included, as Select all has always ticked them.</summary>
    public IReadOnlyList<WorkstreamCardViewModel> GridCards => [.. Cells.SelectMany(cell => cell.Cards)];

    /// <summary>
    /// The cards on screen: the open project view's while it is open, else the grid's. Select all, Invert, Select live
    /// and the count of what is selected act on these, so nothing hidden is ticked (PR 2 review, item 1).
    /// </summary>
    public IReadOnlyList<WorkstreamCardViewModel> ShownCards => SelectionOrder?.Invoke() ?? GridCards;

    /// <summary>
    /// What is on screen to select, in reading order: the open project view's cards while it is open, else every cell's
    /// project cards and then its cards (1.1d AC 41). Select all, Invert, Select live and the count of what is selected
    /// act on these. A project card in a folded row or column is left out (review of PR 3, item 8); a folded card still
    /// counts, as Select all has always ticked it.
    /// </summary>
    public IReadOnlyList<IBoardCard> ShownItems => SelectionOrder?.Invoke() is { } order
        ? order
        : [.. Rows.SelectMany(row => row.Cells.SelectMany(cell =>
            (row.IsCollapsed || cell.IsCollapsed ? [] : cell.Projects.Cast<IBoardCard>()).Concat(cell.Cards)))];

    /// <summary>Every card and project card on the board, shown or not.</summary>
    private IEnumerable<IBoardCard> AllItems => _all.Cast<IBoardCard>().Concat(_projects);

    /// <summary>Ticks or unticks everything shown.</summary>
    public void ChooseShown(bool chosen)
    {
        foreach (var item in ShownItems)
        {
            item.IsSelected = chosen;
        }
    }

    /// <summary>Turns over every tick on what is shown.</summary>
    public void InvertShown()
    {
        foreach (var item in ShownItems)
        {
            item.IsSelected = !item.IsSelected;
        }
    }

    /// <summary>
    /// Ticks what is shown with a session open, a project card when any of its cards has one, and unticks the rest, so
    /// Select live then Stop reaches a project's sessions too.
    /// </summary>
    public void SelectLiveShown()
    {
        foreach (var item in ShownItems)
        {
            item.IsSelected = item.IsLive;
        }
    }

    /// <summary>
    /// Unticks everything not shown, when the main area switches between the board and a project view, so a command on
    /// the selection never reaches a card the owner cannot see.
    /// </summary>
    public void DeselectUnshown()
    {
        var shown = ShownItems.ToHashSet();

        foreach (var item in AllItems.Where(item => item.IsSelected && !shown.Contains(item)).ToList())
        {
            item.IsSelected = false;
        }
    }

    /// <summary>Which cards are kept out of the grid: those of a readable project. Null keeps every card in it.</summary>
    public Func<WorkstreamCardViewModel, bool>? KeepOutOfGrid { get; set; }

    /// <summary>
    /// The order Shift+click selects a run in while the project view is open, which is the view's, not the grid's
    /// (AC 15). Null uses the grid's.
    /// </summary>
    public Func<IReadOnlyList<WorkstreamCardViewModel>>? SelectionOrder { get; set; }

    /// <summary>The project cards on the board.</summary>
    public IReadOnlyList<ProjectCardViewModel> ProjectCards => _projects;

    /// <summary>Puts these project cards on the board, each in the cell it was last dragged to, and regroups.</summary>
    public void ShowProjects(IReadOnlyList<ProjectCardViewModel> projects)
    {
        ArgumentNullException.ThrowIfNull(projects);

        var folded = _settings.Read().CollapsedProjects;

        foreach (var project in projects)
        {
            project.Tags = Tags.OfProject(project.Name);
            project.IsCollapsed = folded.Contains(project.Name, StringComparer.OrdinalIgnoreCase);
        }

        _projects = projects;
        Place();
    }

    /// <summary>
    /// Moves a project card along the axes placed by hand and remembers its place (AC 12; 1.1e AC 1 to 3): its stage
    /// only while the columns show stages, its area only while the rows show areas, so a derived row's name is never
    /// stored. Returns what to say when the drop also tried to move it along a derived axis, or null.
    /// </summary>
    public string? MoveProject(string name, string column, string area)
    {
        if (!CanDrag || _projects.FirstOrDefault(project => string.Equals(project.Name, name, StringComparison.OrdinalIgnoreCase)) is not { } moved)
        {
            return null;
        }

        var settings = _settings.Read();
        var kept = settings.ProjectCardPlaces.FirstOrDefault(entry => string.Equals(entry.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
        var shownIn = Cells.FirstOrDefault(cell => cell.Projects.Contains(moved));
        var notice = DerivedAxisNotice(
            !CanMoveColumns && shownIn is not null && !string.Equals(shownIn.Column, column, StringComparison.Ordinal),
            !CanMoveRows && shownIn is not null && !BoardAreas.Same(shownIn.Area, area));
        var place = new ProjectCardPlace(
            CanMoveColumns ? column : kept?.Column ?? string.Empty,
            CanMoveRows ? area : kept?.Area ?? string.Empty);

        if (place != kept)
        {
            var places = new Dictionary<string, ProjectCardPlace>(settings.ProjectCardPlaces, StringComparer.OrdinalIgnoreCase)
            {
                [name] = place,
            };

            _settings.Write(settings with { ProjectCardPlaces = places });
            _projectPlaces = places;
            Place();
        }

        return notice;
    }

    /// <summary>Every cell, in reading order.</summary>
    public IEnumerable<ColumnViewModel> Cells => Rows.SelectMany(row => row.Cells);

    /// <summary>Raised when the grid itself has changed, not merely the cards in it.</summary>
    public event EventHandler? LanesChanged;

    /// <summary>Re-reads the plans and rebuilds every column.</summary>
    public RelayCommand RefreshCommand { get; }

    /// <summary>What the strip under the title bar says.</summary>
    public string Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    /// <summary>The areas, as configured: what a card's row is when rows show areas.</summary>
    public ObservableCollection<string> Areas { get; } = [];

    /// <summary>
    /// Names the chain a card belongs to, by the title of the card the chain starts from, or null when
    /// it is in none. Set by the shell from the links, which the board does not hold.
    /// </summary>
    public Func<string, string?>? ChainOf { get; set; }

    /// <summary>What the columns show. Changing it regroups the board at once and is remembered.</summary>
    public BoardColumnsBy ColumnsBy
    {
        get => _columnsBy;
        set
        {
            if (Set(ref _columnsBy, value))
            {
                _settings.Write(_settings.Read() with { ColumnsBy = value });
                RaiseViews();
                Place();
            }
        }
    }

    /// <summary>What the rows show. Changing it regroups the board at once and is remembered.</summary>
    public BoardRowsBy RowsBy
    {
        get => _rowsBy;
        set
        {
            if (Set(ref _rowsBy, value))
            {
                _settings.Write(_settings.Read() with { RowsBy = value });
                RaiseViews();
                Place();
            }
        }
    }

    /// <summary>View > Columns by > Stage.</summary>
    public bool ColumnsByStage { get => ColumnsBy == BoardColumnsBy.Stage; set => Choose(value, () => ColumnsBy = BoardColumnsBy.Stage); }

    /// <summary>View > Columns by > Session state.</summary>
    public bool ColumnsBySessionState { get => ColumnsBy == BoardColumnsBy.SessionState; set => Choose(value, () => ColumnsBy = BoardColumnsBy.SessionState); }

    /// <summary>View > Rows by > Area.</summary>
    public bool RowsByArea { get => RowsBy == BoardRowsBy.Area; set => Choose(value, () => RowsBy = BoardRowsBy.Area); }

    /// <summary>View > Rows by > Project.</summary>
    public bool RowsByProject { get => RowsBy == BoardRowsBy.Project; set => Choose(value, () => RowsBy = BoardRowsBy.Project); }

    /// <summary>View > Rows by > Chain.</summary>
    public bool RowsByChain { get => RowsBy == BoardRowsBy.Chain; set => Choose(value, () => RowsBy = BoardRowsBy.Chain); }

    /// <summary>View > Rows by > None.</summary>
    public bool RowsByNone { get => RowsBy == BoardRowsBy.None; set => Choose(value, () => RowsBy = BoardRowsBy.None); }

    /// <summary>
    /// Whether a card can be dragged at all: along at least one axis the owner places by hand (1.1e AC 1, Nigel,
    /// 2026-11-21: "the two user defined ways of presenting rows and columns, are also the ones that are draggable").
    /// </summary>
    public bool CanDrag => CanMoveColumns || CanMoveRows;

    /// <summary>Whether a drop moves a card between columns: only while they show stages, not session state.</summary>
    public bool CanMoveColumns => ColumnsBy == BoardColumnsBy.Stage;

    /// <summary>Whether a drop moves a card between rows: only while they show areas, not folders, chains or nothing.</summary>
    public bool CanMoveRows => RowsBy == BoardRowsBy.Area;

    /// <summary>
    /// What the status bar says when a drop tried to move a card along a derived axis, which changes nothing there
    /// (1.1e AC 3); null when it did not.
    /// </summary>
    private string? DerivedAxisNotice(bool alongColumns, bool alongRows)
    {
        if (alongColumns)
        {
            return "Columns show session state: a card's place there follows its work";
        }

        return alongRows
            ? $"Rows show {(RowsBy == BoardRowsBy.Chain ? "chains" : "folders")}: a card's place there follows its work"
            : null;
    }

    /// <summary>
    /// Every workstream on disk, archived or not, whatever the board shows, for the Theseus tools, which answer about a
    /// card whether or not it is shown (review of 1.1e PR 3, finding 10).
    /// </summary>
    public IReadOnlyList<Workstream> AllWorkstreams()
    {
        var plans = new Dictionary<string, SessionPlan>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in _plans.Keys())
        {
            if (_plans.TryRead(key) is { } plan)
            {
                plans[key] = plan;
            }
        }

        return WorkstreamAssembler.Assemble(plans, _board.Read());
    }

    /// <summary>Reads the plans and sidecars and rebuilds the board from them.</summary>
    public void Refresh()
    {
        var plans = new Dictionary<string, SessionPlan>(StringComparer.OrdinalIgnoreCase);
        var unreadable = 0;

        foreach (var key in _plans.Keys())
        {
            if (_plans.TryRead(key) is { } plan)
            {
                plans[key] = plan;
            }
            else
            {
                unreadable++;
            }
        }

        var assembled = WorkstreamAssembler.Assemble(plans, _board.Read());
        var workstreams = assembled.Where(workstream => workstream.IsArchived == ShowArchived).ToList();
        _archived = ShowArchived ? [] : [.. assembled.Where(workstream => workstream.IsArchived)];

        var lanes = ConfiguredLanes();
        var areas = ConfiguredAreas();
        SyncConfigured(lanes, areas);

        // The cards already on the board are kept and brought up to date, not thrown away and made
        // again. Emptying every cell and refilling it replaced the whole of the window's element
        // tree on every refresh, and WPF reports each such change to any UI Automation listener on
        // the machine, waiting while it does. On 2026-09-15 a dump of the frozen board had the UI
        // thread stuck in exactly that report. Keeping the objects also keeps what the user set on
        // them - ticks, folds - without having to carry it across by key.
        var existing = new Dictionary<string, WorkstreamCardViewModel>(StringComparer.OrdinalIgnoreCase);

        foreach (var card in _all)
        {
            existing.TryAdd(card.Key, card);
        }

        IReadOnlyList<string>? folded = null;
        var next = new List<WorkstreamCardViewModel>();

        foreach (var workstream in workstreams)
        {
            var placed = workstream with
            {
                Column = BoardLanes.Resolve(workstream.Column, lanes),
                Area = BoardAreas.Resolve(workstream.Area, areas),
            };

            if (existing.TryGetValue(workstream.Key, out var card))
            {
                card.Workstream = placed;
            }
            else
            {
                folded ??= _settings.Read().CollapsedCards;
                card = new WorkstreamCardViewModel(placed)
                {
                    IsCollapsed = folded.Contains(workstream.Key, StringComparer.OrdinalIgnoreCase),
                };
            }

            next.Add(card);
        }

        _all = next;
        Place();

        Status = unreadable == 0
            ? $"{workstreams.Count} workstreams"
            : $"{workstreams.Count} workstreams · {unreadable} plan file(s) unreadable";
    }

    /// <summary>
    /// Puts every card in the cell the view says, without reading anything from disk. Called after a
    /// refresh, and again once the sessions have been read, since the session-state view depends on
    /// them and the chain view on the links.
    /// </summary>
    public void Regroup() => Place();

    private void Place()
    {
        var (columns, rows) = ViewAxes();
        SyncGrid(columns, rows);
        Evaluate();

        var wanted = Cells.ToDictionary(cell => cell, _ => new List<WorkstreamCardViewModel>());
        var byPlace = Cells.ToDictionary(cell => (cell.Column, cell.Area));

        foreach (var card in _all)
        {
            if (KeepOutOfGrid?.Invoke(card) == true || HiddenBySearch(card) || Kept(BoardShows.Projects) || HiddenByActiveOnly(card))
            {
                continue;
            }

            if (byPlace.TryGetValue((ColumnOf(card), RowOf(card)), out var cell))
            {
                wanted[cell].Add(card);
            }
        }

        PlaceProjects();

        // Everything leaving a cell goes before anything arrives, so a card moving between cells is
        // never in two at once.
        foreach (var cell in Cells)
        {
            for (var index = cell.Cards.Count - 1; index >= 0; index--)
            {
                if (!wanted[cell].Contains(cell.Cards[index]))
                {
                    cell.Cards.RemoveAt(index);
                }
            }
        }

        foreach (var cell in Cells)
        {
            var order = wanted[cell];

            for (var index = 0; index < order.Count; index++)
            {
                var current = cell.Cards.IndexOf(order[index]);

                if (current == index)
                {
                    continue;
                }

                if (current >= 0)
                {
                    cell.Cards.Move(current, index);
                }
                else
                {
                    cell.Cards.Insert(index, order[index]);
                }
            }
        }

        if (SelectedCard is { } selected && !_all.Contains(selected))
        {
            SelectedCard = null;
        }

        foreach (var row in Rows)
        {
            row.RaiseCounts();
        }

        ShowSearch();

        // A ticked card that has just left the grid (its project began to read, or it joined one) is unticked, so the
        // selection commands never reach a card that cannot be seen (PR 2 re-review, item 1).
        if (KeepOutOfGrid is not null || SelectionOrder is not null)
        {
            DeselectUnshown();
        }
    }

    /// <summary>The ticked cards among those shown: what Start, Stop, Archive and the other selected-card commands act on.</summary>
    public IReadOnlyList<WorkstreamCardViewModel> ChosenCards => [.. ShownCards.Where(card => card.IsSelected)];

    /// <summary>The selected project cards among those shown: Start and Stop selected act on their cards (1.1d AC 43).</summary>
    public IReadOnlyList<ProjectCardViewModel> ChosenProjects =>
        [.. ShownItems.OfType<ProjectCardViewModel>().Where(project => project.IsSelected)];

    /// <summary>
    /// Puts each project card in its cell: along a hand-placed axis where it was dragged to, along a derived one by its
    /// cards (1.1e AC 4). A column or row the view does not have falls back to the first, as a card's does.
    /// </summary>
    private void PlaceProjects()
    {
        if (_projects.Count == 0 && Cells.All(cell => cell.Projects.Count == 0))
        {
            return;
        }

        // Read once: Place runs on every refresh, and only a drag here changes the places.
        var places = _projectPlaces ??= _settings.Read().ProjectCardPlaces;
        var first = Cells.FirstOrDefault();

        foreach (var cell in Cells)
        {
            var wanted = _projects
                .Where(project => !HiddenBySearch(project) && !Kept(BoardShows.Workstreams) && !HiddenByActiveOnly(project))
                .Where(project =>
                {
                    var place = places.FirstOrDefault(entry => string.Equals(entry.Key, project.Name, StringComparison.OrdinalIgnoreCase)).Value;
                    var column = CanMoveColumns ? place?.Column : project.StateColumn;
                    var row = RowsBy switch
                    {
                        BoardRowsBy.Area => place?.Area,
                        BoardRowsBy.Project or BoardRowsBy.Chain => project.FirstCardKey is { } key && Card(key) is { } firstCard
                            ? RowOf(firstCard)
                            : null,
                        _ => null,
                    };
                    bool InColumn(ColumnViewModel candidate) => string.IsNullOrEmpty(column) || candidate.Column == column;
                    var target = Cells.FirstOrDefault(candidate => InColumn(candidate) && (string.IsNullOrEmpty(row) || BoardAreas.Same(candidate.Area, row)))
                                 ?? Cells.FirstOrDefault(InColumn)
                                 ?? first;

                    return ReferenceEquals(target, cell);
                })
                .ToList();

            if (!wanted.SequenceEqual(cell.Projects))
            {
                cell.Projects.Clear();

                foreach (var project in wanted)
                {
                    cell.Projects.Add(project);
                }
            }
        }
    }

    /// <summary>The column a card belongs in, in the current view.</summary>
    private string ColumnOf(WorkstreamCardViewModel card) => ColumnsBy == BoardColumnsBy.Stage
        ? card.Column
        : BoardView.StateColumn(card.Lifecycle, string.Equals(card.Column, Stages.LastOrDefault(), StringComparison.Ordinal));

    /// <summary>The row a card belongs in, in the current view.</summary>
    private string RowOf(WorkstreamCardViewModel card) => RowsBy switch
    {
        BoardRowsBy.Project => BoardView.Project(card.Workstream.WorkingDirectory),
        BoardRowsBy.Chain => ChainOf?.Invoke(card.Key) ?? BoardView.NoChain,
        BoardRowsBy.None => BoardView.AllWork,
        _ => card.Area,
    };

    /// <summary>
    /// The view's columns and rows. Stages and areas are as configured, empty ones included, since
    /// they are places to put work; projects and chains exist only where there are cards.
    /// </summary>
    private (IReadOnlyList<string> Columns, IReadOnlyList<string> Rows) ViewAxes()
    {
        IReadOnlyList<string> columns = ColumnsBy == BoardColumnsBy.Stage ? [.. Stages] : BoardView.StateColumns;

        IReadOnlyList<string> rows = RowsBy switch
        {
            BoardRowsBy.Area => [.. Areas],
            BoardRowsBy.None => [BoardView.AllWork],
            BoardRowsBy.Chain => [.. _all.Select(RowOf).Distinct(StringComparer.Ordinal)
                .OrderBy(name => name == BoardView.NoChain)
                .ThenBy(name => name, StringComparer.CurrentCultureIgnoreCase)],
            _ => [.. _all.Select(RowOf).Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)],
        };

        return (columns, rows.Count == 0 ? [BoardView.AllWork] : rows);
    }

    /// <summary>The configured stages, cleaned up.</summary>
    private IReadOnlyList<string> ConfiguredLanes() => BoardLanes.Sanitise(_settings.Read().Lanes);

    /// <summary>The configured areas, cleaned up.</summary>
    private IReadOnlyList<string> ConfiguredAreas() => BoardAreas.Sanitise(_settings.Read().Areas);

    private void SyncConfigured(IReadOnlyList<string> lanes, IReadOnlyList<string> areas)
    {
        if (!Stages.SequenceEqual(lanes, StringComparer.Ordinal))
        {
            Fill(Stages, lanes);
        }

        if (!Areas.SequenceEqual(areas, StringComparer.Ordinal))
        {
            Fill(Areas, areas);
        }
    }

    private static void Fill(ObservableCollection<string> target, IReadOnlyList<string> values)
    {
        target.Clear();

        foreach (var value in values)
        {
            target.Add(value);
        }
    }

    /// <summary>
    /// Brings the grid into line with the view, rebuilding it only when its columns or rows have
    /// actually changed. A rebuild replaces every row and cell, so doing it on every refresh would
    /// throw away each row's collapsed state, its commands and its scroll position for nothing.
    /// </summary>
    private void SyncGrid(IReadOnlyList<string> columns, IReadOnlyList<string> rows)
    {
        var same = StageHeaders.Select(header => header.Stage).SequenceEqual(columns, StringComparer.Ordinal)
                   && Rows.Select(row => row.Area).SequenceEqual(rows, StringComparer.Ordinal);

        if (!same)
        {
            Rebuild(columns, rows);
            LanesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Rebuild(IReadOnlyList<string> columns, IReadOnlyList<string> rows)
    {
        var foldedRows = FoldedRows();
        var foldedColumns = FoldedColumns();
        var weights = _settings.Read().ColumnWeights;
        var heights = _settings.Read().RowHeights;

        StageHeaders.Clear();
        Rows.Clear();

        foreach (var name in rows)
        {
            var row = new BoardRowViewModel(name)
            {
                IsCollapsed = foldedRows.Any(folded => BoardAreas.Same(name, folded)),
                Height = heights.TryGetValue(RowSizeKey(name), out var height) ? height : double.NaN,
            };

            foreach (var column in columns)
            {
                row.Cells.Add(new ColumnViewModel(column, name));
            }

            Rows.Add(row);
        }

        foreach (var column in columns)
        {
            var header = new StageHeaderViewModel(column, [.. Rows.Select(row => row.Cells.First(cell => cell.Column == column))])
            {
                IsCollapsed = foldedColumns.Contains(column, StringComparer.OrdinalIgnoreCase),
                Weight = weights.TryGetValue(ColumnSizeKey(column), out var weight) ? weight : 1,
            };

            header.ToggleCommand = new RelayCommand(_ => ToggleStage(header));
            StageHeaders.Add(header);
        }
    }

    /// <summary>
    /// Moves the edge between a column and the next open one by a share of the width: one widens as
    /// the other narrows, and the rest stay put. Neither goes below a fifth of the even share.
    /// </summary>
    public void ResizeColumns(StageHeaderViewModel left, double shareMoved)
    {
        ArgumentNullException.ThrowIfNull(left);

        var index = StageHeaders.IndexOf(left);
        var right = StageHeaders.Skip(index + 1).FirstOrDefault(header => !header.IsCollapsed);

        if (index < 0 || right is null || left.IsCollapsed)
        {
            return;
        }

        const double least = 0.2;
        var moved = Math.Clamp(shareMoved, least - left.Weight, right.Weight - least);

        left.Weight += moved;
        right.Weight -= moved;
    }

    /// <summary>The open columns' combined share, so a drag in pixels can be turned into a share.</summary>
    public double OpenColumnWeight => StageHeaders.Where(header => !header.IsCollapsed).Sum(header => header.Weight);

    /// <summary>Sets every column back to the even share.</summary>
    public void ResetColumnWidths()
    {
        foreach (var header in StageHeaders)
        {
            header.Weight = 1;
        }

        RememberSizes();
    }

    /// <summary>Gives a row a set height, at least enough for a folded card or two.</summary>
    public void SetRowHeight(BoardRowViewModel row, double height)
    {
        ArgumentNullException.ThrowIfNull(row);

        row.Height = Math.Max(MinimumRowHeight, height);
    }

    /// <summary>Lets a row fit its cards again.</summary>
    public void ResetRowHeight(BoardRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        row.Height = double.NaN;
        RememberSizes();
    }

    /// <summary>The least a row can be dragged to.</summary>
    public const double MinimumRowHeight = 90;

    /// <summary>Writes down this view's column widths and row heights, after a drag ends.</summary>
    public void RememberSizes()
    {
        var settings = _settings.Read();
        var weights = new Dictionary<string, double>(settings.ColumnWeights);
        var heights = new Dictionary<string, double>(settings.RowHeights);

        foreach (var header in StageHeaders)
        {
            if (Math.Abs(header.Weight - 1) < 0.001)
            {
                weights.Remove(ColumnSizeKey(header.Stage));
            }
            else
            {
                weights[ColumnSizeKey(header.Stage)] = Math.Round(header.Weight, 3);
            }
        }

        foreach (var row in Rows)
        {
            if (double.IsNaN(row.Height))
            {
                heights.Remove(RowSizeKey(row.Area));
            }
            else
            {
                heights[RowSizeKey(row.Area)] = Math.Round(row.Height);
            }
        }

        _settings.Write(settings with { ColumnWeights = weights, RowHeights = heights });
    }

    private string ColumnSizeKey(string column) => $"{ColumnsBy}:{column}";

    private string RowSizeKey(string row) => $"{RowsBy}:{row}";

    private string RowFoldKey => $"rows:{RowsBy}";

    private string ColumnFoldKey => $"columns:{ColumnsBy}";

    private IReadOnlyList<string> FoldedRows() => RowsBy == BoardRowsBy.Area
        ? _settings.Read().CollapsedAreas
        : _settings.Read().ViewFolds.GetValueOrDefault(RowFoldKey) ?? [];

    private IReadOnlyList<string> FoldedColumns() => ColumnsBy == BoardColumnsBy.Stage
        ? _settings.Read().CollapsedStages
        : _settings.Read().ViewFolds.GetValueOrDefault(ColumnFoldKey) ?? [];

    /// <summary>Writes down what is folded in this view, in the setting that view keeps it in.</summary>
    private void RememberGridFolds()
    {
        var rows = (IReadOnlyList<string>)[.. Rows.Where(row => row.IsCollapsed).Select(row => row.Area)];
        var columns = (IReadOnlyList<string>)[.. StageHeaders.Where(header => header.IsCollapsed).Select(header => header.Stage)];
        var settings = _settings.Read();
        var views = new Dictionary<string, IReadOnlyList<string>>(settings.ViewFolds);

        if (RowsBy != BoardRowsBy.Area)
        {
            views[RowFoldKey] = rows;
        }

        if (ColumnsBy != BoardColumnsBy.Stage)
        {
            views[ColumnFoldKey] = columns;
        }

        _settings.Write(settings with
        {
            CollapsedAreas = RowsBy == BoardRowsBy.Area ? rows : settings.CollapsedAreas,
            CollapsedStages = ColumnsBy == BoardColumnsBy.Stage ? columns : settings.CollapsedStages,
            ViewFolds = views,
        });
    }

    /// <summary>Folds a column away in every row, or opens it, and remembers which way it was left.</summary>
    public void ToggleStage(StageHeaderViewModel header)
    {
        ArgumentNullException.ThrowIfNull(header);

        header.IsCollapsed = !header.IsCollapsed;
        RememberGridFolds();
    }

    /// <summary>Opens every folded row and column in this view, and remembers that nothing is folded.</summary>
    public void UnfoldGrid()
    {
        foreach (var row in Rows)
        {
            row.IsCollapsed = false;
        }

        foreach (var header in StageHeaders)
        {
            header.IsCollapsed = false;
        }

        RememberGridFolds();
    }

    private void Choose(bool chosen, Action choose)
    {
        if (chosen)
        {
            choose();
        }
        else
        {
            // Unticking the chosen view is not a choice of another, so it stays ticked.
            RaiseViews();
        }
    }

    private void RaiseViews()
    {
        Raise(nameof(ColumnsByStage));
        Raise(nameof(ColumnsBySessionState));
        Raise(nameof(RowsByArea));
        Raise(nameof(RowsByProject));
        Raise(nameof(RowsByChain));
        Raise(nameof(RowsByNone));
        Raise(nameof(CanDrag));
        Raise(nameof(CanMoveColumns));
        Raise(nameof(CanMoveRows));
    }

    /// <summary>Folds every card and project card.</summary>
    public void FoldAll() => SetFolds(_ => true);

    /// <summary>Folds every card and project card with no session, and opens the rest, so folded means nothing runs there.</summary>
    public void FoldInactive() => SetFolds(FoldsWhenInactive);

    /// <summary>Whether Fold cards with no session folds this one: no session on it, nor, for a project, on any of its cards.</summary>
    public bool FoldsWhenInactive(IBoardCard item) => !item.IsLive && !(item is ProjectCardViewModel project && MembersLive(project));

    /// <summary>Opens every card and project card.</summary>
    public void UnfoldAll() => SetFolds(_ => false);

    /// <summary>Folds one project card or opens it, and remembers which way it was left.</summary>
    public void Fold(ProjectCardViewModel project, bool folded)
    {
        ArgumentNullException.ThrowIfNull(project);

        project.IsCollapsed = folded;
        RememberFolds();
    }

    private void SetFolds(Func<IBoardCard, bool> folded)
    {
        foreach (var card in Cards)
        {
            card.IsCollapsed = folded(card);
        }

        foreach (var project in _projects)
        {
            project.IsCollapsed = folded(project);
        }

        RememberFolds();
    }

    private bool MembersLive(ProjectCardViewModel project) => KeysIn(project).Any(key => Card(key) is { IsLive: true });

    /// <summary>Folds one card or opens it, and remembers which way it was left.</summary>
    public void Fold(WorkstreamCardViewModel card, bool folded)
    {
        ArgumentNullException.ThrowIfNull(card);

        card.IsCollapsed = folded;
        RememberFolds();
    }

    /// <summary>
    /// Writes down which cards are folded. Cards not on the board just now - archived, or hidden by
    /// the archive view - keep whatever was remembered for them.
    /// </summary>
    public void RememberFolds()
    {
        var shown = Cards.Select(card => card.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var settings = _settings.Read();

        _settings.Write(settings with
        {
            CollapsedCards =
            [
                .. settings.CollapsedCards.Where(key => !shown.Contains(key)),
                .. Cards.Where(card => card.IsCollapsed).Select(card => card.Key),
            ],
            CollapsedProjects =
            [
                .. settings.CollapsedProjects.Where(name => !_projects.Any(project => string.Equals(project.Name, name, StringComparison.OrdinalIgnoreCase))),
                .. _projects.Where(project => project.IsCollapsed).Select(project => project.Name),
            ],
        });
    }

    /// <summary>
    /// Puts a workstream in a cell of the grid and writes that down, so it opens there next time too.
    /// A stage or area the board does not have resolves to the first one, the way a card read from
    /// disk does, rather than leaving the workstream in a cell that is not on the board.
    /// </summary>
    public void Place(string key, string? stage, string? area)
    {
        if (Card(key) is not { } card)
        {
            return;
        }

        card.Workstream = card.Workstream with
        {
            Column = BoardLanes.Resolve(stage ?? string.Empty, [.. Stages]),
            Area = BoardAreas.Resolve(area ?? string.Empty, [.. Areas]),
        };

        Persist(card);
        Refresh();
    }

    /// <summary>Folds a row away, or opens it, and remembers which way it was left.</summary>
    public void ToggleRow(BoardRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        row.IsCollapsed = !row.IsCollapsed;
        RememberGridFolds();
    }

    /// <summary>
    /// Moves a card along the axes placed by hand and persists the change (1.1e AC 1 to 3): its stage only while the
    /// columns show stages, its area only while the rows show areas. Along a derived axis the drop changes nothing, and
    /// what to say about that is returned; null when there is nothing to say.
    /// </summary>
    /// <param name="destination">The column it was dropped in.</param>
    /// <param name="area">
    /// The row it was dropped in. Null keeps the area it is in, which is what dragging within a row
    /// means — and what every drop meant before the board had rows.
    /// </param>
    /// <param name="index">
    /// Where in the cell to put it, or null for the end. Position is not decoration: a cell's order
    /// is the order its tabs open in when it is started, so dragging a card up brings its session
    /// forward in the morning. Only under stage columns is it used and the plans renumbered; a card moved between
    /// areas under session-state columns keeps its old number, and no plan is rewritten.
    /// </param>
    public string? Move(string key, string destination, string? area = null, int? index = null)
    {
        var card = Card(key);

        if (card is null || !CanDrag)
        {
            return null;
        }

        var notice = DerivedAxisNotice(
            !CanMoveColumns && !string.Equals(destination, ColumnOf(card), StringComparison.Ordinal),
            !CanMoveRows && area is not null && !string.Equals(area, RowOf(card), StringComparison.Ordinal));
        var column = CanMoveColumns ? destination : card.Column;
        var newArea = CanMoveRows && area is not null ? area : card.Area;
        var targetColumn = CanMoveColumns ? destination : ColumnOf(card);
        var targetRow = CanMoveRows ? newArea : RowOf(card);
        var cell = Cells.FirstOrDefault(candidate =>
            candidate.Column == targetColumn && BoardAreas.Same(candidate.Area, targetRow));
        var placeChanged = !string.Equals(column, card.Column, StringComparison.Ordinal) || !BoardAreas.Same(newArea, card.Area);

        if (cell is null || (!placeChanged && (notice is not null || !CanMoveColumns)))
        {
            return notice;
        }

        var cameFrom = cell.Cards.IndexOf(card) is var found && found >= 0 ? found : (int?)null;

        foreach (var other in Cells)
        {
            other.Cards.Remove(card);
        }

        var at = LaneInsertion.At(notice is null && CanMoveColumns ? index ?? cell.Cards.Count + 1 : cell.Cards.Count + 1, cameFrom, cell.Cards.Count);
        var stayed = cameFrom == at && !placeChanged;

        cell.Cards.Insert(at, card);

        if (stayed)
        {
            return notice;
        }

        card.Workstream = card.Workstream with { Column = column, Area = newArea };

        Persist(card);

        if (CanMoveColumns)
        {
            Renumber(cell);
        }

        return notice;
    }

    /// <summary>
    /// Writes each card's position in the lane back to its plan file, so the order survives a
    /// restart and the morning's tabs open in it. Only the cards whose number actually changed are
    /// written: these files are authored by the sessions themselves, and rewriting one that did not
    /// move would put Theseus in a session's file for no reason.
    /// </summary>
    private void Renumber(ColumnViewModel lane)
    {
        for (var position = 0; position < lane.Cards.Count; position++)
        {
            var card = lane.Cards[position];
            var order = position + 1;

            if (card.Workstream.BoardOrder == order)
            {
                continue;
            }

            card.Workstream = card.Workstream with { BoardOrder = order };

            if (_plans.TryRead(card.Key) is { } plan)
            {
                _plans.Write(card.Key, plan with { Order = order });
            }
        }
    }

    /// <summary>The card the next Adopt will attach a session to.</summary>
    public WorkstreamCardViewModel? SelectedCard
    {
        get => _selectedCard;
        private set => Set(ref _selectedCard, value);
    }

    /// <summary>
    /// The cards that can be seen, in reading order: row by row, cell by cell. Shift+click selects a
    /// run of these, so a card in a folded row or column is never caught up in it.
    /// </summary>
    public IReadOnlyList<WorkstreamCardViewModel> VisibleCards =>
        [.. Rows.Where(row => !row.IsCollapsed).SelectMany(row => row.Cells.Where(cell => !cell.IsCollapsed)).SelectMany(cell => cell.Cards)];

    /// <summary>The same, with each cell's project cards before its cards, as they are drawn.</summary>
    public IReadOnlyList<IBoardCard> VisibleItems =>
        [.. Rows.Where(row => !row.IsCollapsed).SelectMany(row => row.Cells.Where(cell => !cell.IsCollapsed))
            .SelectMany(cell => cell.Projects.Cast<IBoardCard>().Concat(cell.Cards))];

    private RelayCommand? _keySelect;
    private RelayCommand? _keyToggleSelect;

    /// <summary>Space on a focused card or project card: selects it alone, as a click does (baseline W44, G64, G69).</summary>
    public RelayCommand KeySelectCommand => _keySelect ??= new RelayCommand(
        item => ClickSelect((IBoardCard)item!, toggle: false, extend: false), item => item is IBoardCard);

    /// <summary>Ctrl+Space on a focused card or project card: adds it to the selection or takes it out, as Ctrl+click does.</summary>
    public RelayCommand KeyToggleSelectCommand => _keyToggleSelect ??= new RelayCommand(
        item => ClickSelect((IBoardCard)item!, toggle: true, extend: false), item => item is IBoardCard);

    /// <summary>
    /// Selects as a click does everywhere in Windows (Nigel, 2026-10-30): a click selects that card
    /// alone, Ctrl+click adds or removes it, and Shift+click selects the run from the last card
    /// clicked to this one, added to the rest when Ctrl is held too. A project card is selected the same way, in the
    /// same reading order (1.1d AC 41).
    /// </summary>
    public void ClickSelect(IBoardCard card, bool toggle, bool extend)
    {
        ArgumentNullException.ThrowIfNull(card);

        IReadOnlyList<IBoardCard> order = SelectionOrder?.Invoke() is { } inView ? inView : VisibleItems;
        var visible = order.ToList();

        if (extend && _anchor is not null && visible.Contains(_anchor) && visible.Contains(card))
        {
            var from = visible.IndexOf(_anchor);
            var to = visible.IndexOf(card);
            var (low, high) = from <= to ? (from, to) : (to, from);

            foreach (var other in AllItems)
            {
                var index = visible.IndexOf(other);
                var inRun = index >= low && index <= high;

                if (inRun || !toggle)
                {
                    other.IsSelected = inRun;
                }
            }

            return;
        }

        if (toggle)
        {
            card.IsSelected = !card.IsSelected;
        }
        else
        {
            foreach (var other in AllItems)
            {
                other.IsSelected = ReferenceEquals(other, card);
            }
        }

        _anchor = card;
    }

    /// <summary>Selects the cards and project cards a rubber band was drawn over, alone or added to the rest.</summary>
    public void BandSelect(IReadOnlyList<IBoardCard> cards, bool add)
    {
        ArgumentNullException.ThrowIfNull(cards);

        foreach (var card in AllItems)
        {
            if (cards.Contains(card))
            {
                card.IsSelected = true;
            }
            else if (!add)
            {
                card.IsSelected = false;
            }
        }

        _anchor = cards.FirstOrDefault() ?? _anchor;
    }

    /// <summary>Clears the selection, as a click on empty board does.</summary>
    public void ClearSelection()
    {
        foreach (var card in AllItems)
        {
            card.IsSelected = false;
        }
    }

    /// <summary>Selects one card and clears any other selection.</summary>
    public void Select(WorkstreamCardViewModel? card)
    {
        foreach (var other in Cards)
        {
            other.IsAdoptTarget = ReferenceEquals(other, card);
        }

        SelectedCard = card;
    }

    /// <summary>Records which session a workstream now follows, so Focus and resume can find it.</summary>
    public void RememberSession(string key, string sessionId)
    {
        var card = Card(key);
        if (card is null)
        {
            return;
        }

        card.Workstream = card.Workstream with { LastSessionId = sessionId };
        Persist(card);
    }

    /// <summary>Which cards the board shows (Nigel, 2026-11-23): all, only project cards, or only workstreams. Remembered.</summary>
    public BoardShows Shows
    {
        get => _shows;
        set
        {
            if (Set(ref _shows, value))
            {
                _settings.Write(_settings.Read() with { BoardShows = value });
                Raise(nameof(ShowsAll));
                Raise(nameof(ShowsProjects));
                Raise(nameof(ShowsWorkstreams));
                Place();
                Raise(nameof(ShowsNote));
                Raise(nameof(ShowsNoteInStatus));
            }
        }
    }

    /// <summary>What the status bar adds while one kind is hidden, such as "showing projects only"; empty showing all.</summary>
    public string ShowsNote => _shows switch
    {
        BoardShows.Projects when !ShowArchived => "showing projects only",
        BoardShows.Workstreams when !ShowArchived => "showing workstreams only",
        _ => string.Empty,
    };

    /// <summary>The note as the status bar shows it, after a separator; empty, with no separator, while all show (walkthrough row 0).</summary>
    public string ShowsNoteInStatus => ShowsNote.Length == 0 ? string.Empty : $"  ·  {ShowsNote}";

    /// <summary>Show all, as a radio button binds it.</summary>
    public bool ShowsAll
    {
        get => _shows == BoardShows.All;
        set => Choose(value, BoardShows.All);
    }

    /// <summary>Show only the project cards, as a radio button binds it.</summary>
    public bool ShowsProjects
    {
        get => _shows == BoardShows.Projects;
        set => Choose(value, BoardShows.Projects);
    }

    /// <summary>Show only the workstreams, as a radio button binds it.</summary>
    public bool ShowsWorkstreams
    {
        get => _shows == BoardShows.Workstreams;
        set => Choose(value, BoardShows.Workstreams);
    }

    /// <summary>
    /// Show active only (Nigel, 2026-11-24): the board keeps only what is active, a card with a session or with something
    /// waiting on the owner, and a project when any of its cards is or something in it waits on the owner. Remembered.
    /// </summary>
    public bool ShowsActiveOnly
    {
        get => _activeOnly;
        set
        {
            if (Set(ref _activeOnly, value))
            {
                _settings.Write(_settings.Read() with { ShowActiveOnly = value });
                Place();
            }
        }
    }

    /// <summary>Turns Show active only on or off.</summary>
    public RelayCommand ToggleActiveOnlyCommand => _toggleActiveOnly ??= new RelayCommand(_ => ShowsActiveOnly = !ShowsActiveOnly);

    /// <summary>Whether something on a card waits on the owner, by key, such as a report to confirm. Set by the shell.</summary>
    public Func<string, bool>? WaitsOnOwner { get; set; }

    /// <summary>"Showing 5 of 40 cards · active only" while it is on; empty otherwise.</summary>
    public string ActiveOnlyNote
    {
        get => _activeOnlyNote;
        private set
        {
            if (Set(ref _activeOnlyNote, value))
            {
                Raise(nameof(ActiveOnlyNoteInStatus));
            }
        }
    }

    /// <summary>The note as the status bar shows it, after a separator; empty, with no separator, while it is off.</summary>
    public string ActiveOnlyNoteInStatus => _activeOnlyNote.Length == 0 ? string.Empty : $"  ·  {_activeOnlyNote}";

    private bool IsActive(IBoardCard item) => item switch
    {
        WorkstreamCardViewModel card => card.IsLive || WaitsOnOwner?.Invoke(card.Key) == true,
        ProjectCardViewModel project => project.IsLive || project.WaitingCount > 0
            || KeysIn(project).Any(key => Card(key) is { } member && IsActive(member)),
        _ => item.IsLive,
    };

    // The archive is a view of its own, so the filter leaves it alone, as Show does.
    private bool HiddenByActiveOnly(IBoardCard item) => _activeOnly && !ShowArchived && !IsActive(item);

    // The archive shows workstreams only, so while it is on the choice hides nothing (review, finding 4).
    private bool Kept(BoardShows shows) => _shows == shows && !ShowArchived;

    private void Choose(bool chosen, BoardShows shows)
    {
        if (chosen)
        {
            Shows = shows;
        }
    }

    /// <summary>
    /// Whether the board is showing what has been put away instead of what is being worked on. The
    /// two are shown apart rather than together: an archived workstream is one somebody decided to
    /// stop looking at, and a board that keeps showing it greyed out has not done what was asked.
    /// </summary>
    public bool ShowArchived
    {
        get => _showArchived;
        set
        {
            if (Set(ref _showArchived, value))
            {
                _settings.Write(_settings.Read() with { ShowArchived = value });
                Refresh();
            }
        }
    }

    /// <summary>Puts a workstream away, or brings it back.</summary>
    public void SetArchived(WorkstreamCardViewModel card, bool archived)
    {
        card.Workstream = card.Workstream with { IsArchived = archived };
        PersistArchived(card, archived);
        Refresh();
    }

    /// <summary>
    /// Destroys a workstream: its plan file, which is the only copy of what its sessions were told,
    /// and its entry on the board. Its handover notes and its work are somebody else's files and are
    /// left where they are.
    /// </summary>
    public void Remove(WorkstreamCardViewModel card)
    {
        _plans.Delete(card.Key);
        _board.Write([.. _board.Read().Where(sidecar =>
            !string.Equals(sidecar.Key, card.Key, StringComparison.OrdinalIgnoreCase))]);

        Refresh();
    }

    private void PersistArchived(WorkstreamCardViewModel card, bool archived)
    {
        var sidecars = _board.Read().ToList();
        var index = sidecars.FindIndex(s => string.Equals(s.Key, card.Key, StringComparison.OrdinalIgnoreCase));
        var now = DateTimeOffset.UtcNow;

        if (index >= 0)
        {
            sidecars[index] = sidecars[index] with { IsArchived = archived, UpdatedUtc = now };
        }
        else
        {
            sidecars.Add(new WorkstreamSidecar(card.Key, card.Column, card.Workstream.IsBlocked, archived,
                ColorIsManual: true, card.Workstream.ReopenTomorrow, card.JiraKeys,
                card.Workstream.StatusDocumentPath, card.Workstream.LastSessionId, now, now)
            {
                Tags = card.Workstream.Tags,
            });
        }

        _board.Write(sidecars);
    }

    /// <summary>
    /// Settles where a workstream's handover notes go and writes that down, so every later launch
    /// and every stop names the same file rather than working one out again.
    /// </summary>
    public Workstream Pin(Workstream workstream)
    {
        var pinned = HandoffPin.Pin(workstream);

        if (ReferenceEquals(pinned, workstream) || Card(workstream.Key) is not { } card)
        {
            return pinned;
        }

        card.Workstream = pinned;
        Persist(card);

        return pinned;
    }

    /// <summary>Finds a card by its workstream key.</summary>
    public WorkstreamCardViewModel? Card(string key) =>
        Cards.FirstOrDefault(card => string.Equals(card.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>Writes one card back to the board file.</summary>
    public void Persist(WorkstreamCardViewModel card)
    {
        var sidecars = _board.Read().ToList();
        var index = sidecars.FindIndex(s => string.Equals(s.Key, card.Key, StringComparison.OrdinalIgnoreCase));
        var now = DateTimeOffset.UtcNow;

        var updated = index >= 0
            ? sidecars[index] with
              {
                  Column = card.Column,
                  IsBlocked = card.Workstream.IsBlocked,
                  IsPaused = card.Workstream.IsPaused,
                  PausedBy = card.Workstream.IsPaused ? card.Workstream.PausedBy : null,
                  PauseReason = card.Workstream.IsPaused ? card.Workstream.PauseReason : null,
                  ReopenTomorrow = card.Workstream.ReopenTomorrow,
                  JiraKeys = card.JiraKeys,
                  RemovedJiraKeys = card.Workstream.RemovedJiraKeys,
                  WindowGroup = card.Workstream.WindowGroup,
                  Area = card.Workstream.Area,
                  StatusDocumentPath = card.Workstream.StatusDocumentPath,
                  LastSessionId = card.Workstream.LastSessionId,
                  EveryStartPrompt = card.Workstream.EveryStartPrompt,
                  Role = card.Workstream.Role,
                  Tags = card.Workstream.Tags,
                  RunMode = card.Workstream.RunMode,
                  Breaker = card.Workstream.Breaker,
                  BreakerResetUtc = card.Workstream.BreakerResetUtc,
                  UpdatedUtc = now,
              }
            : new WorkstreamSidecar(card.Key, card.Column, card.Workstream.IsBlocked, IsArchived: false,
                ColorIsManual: true, card.Workstream.ReopenTomorrow, card.JiraKeys,
                card.Workstream.StatusDocumentPath, card.Workstream.LastSessionId, now, now)
              {
                  RemovedJiraKeys = card.Workstream.RemovedJiraKeys,
                  WindowGroup = card.Workstream.WindowGroup,
                  Area = card.Workstream.Area,
                  EveryStartPrompt = card.Workstream.EveryStartPrompt,
                  IsPaused = card.Workstream.IsPaused,
                  PausedBy = card.Workstream.IsPaused ? card.Workstream.PausedBy : null,
                  PauseReason = card.Workstream.IsPaused ? card.Workstream.PauseReason : null,
                  Role = card.Workstream.Role,
                  Tags = card.Workstream.Tags,
                  RunMode = card.Workstream.RunMode,
                  Breaker = card.Workstream.Breaker,
                  BreakerResetUtc = card.Workstream.BreakerResetUtc,
              };

        if (index >= 0)
        {
            sidecars[index] = updated;
        }
        else
        {
            sidecars.Add(updated);
        }

        _board.Write(sidecars);
        Tags.EnsureColours(card.Workstream.Tags);
    }
}
