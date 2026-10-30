using System.Collections.ObjectModel;
using Theseus.Core.Abstractions;
using Theseus.Core.Domain;
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
    private BoardColumnsBy _columnsBy;
    private BoardRowsBy _rowsBy;
    private List<WorkstreamCardViewModel> _all = [];

    /// <summary>Creates the board over its stores.</summary>
    public BoardViewModel(IPlanStore plans, IBoardStore board, ISettingsStore settings)
    {
        _plans = plans;
        _board = board;
        _settings = settings;
        _showArchived = settings.Read().ShowArchived;
        _columnsBy = settings.Read().ColumnsBy;
        _rowsBy = settings.Read().RowsBy;

        SyncConfigured(ConfiguredLanes(), ConfiguredAreas());
        Place();

        RefreshCommand = new RelayCommand(_ => Refresh());
    }

    /// <summary>The stages, as configured: what a card's stage can be, whatever the columns show.</summary>
    public ObservableCollection<string> Stages { get; } = [];

    /// <summary>The headings across the top of the grid, one per column of the current view, each able to fold it away.</summary>
    public ObservableCollection<StageHeaderViewModel> StageHeaders { get; } = [];

    /// <summary>The rows of the current view, each holding a cell per column.</summary>
    public ObservableCollection<BoardRowViewModel> Rows { get; } = [];

    /// <summary>Every card on the board, whatever cell it is in.</summary>
    public IEnumerable<WorkstreamCardViewModel> Cards =>
        Rows.SelectMany(row => row.Cells).SelectMany(cell => cell.Cards);

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
    /// Whether a dragged card can be dropped to move it. Only stages are a place a card is put: in the
    /// session-state view a card's column is whatever its session is doing.
    /// </summary>
    public bool CanDrag => ColumnsBy == BoardColumnsBy.Stage;

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

        var workstreams = WorkstreamAssembler.Assemble(plans, _board.Read())
            .Where(workstream => workstream.IsArchived == ShowArchived)
            .ToList();

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

        var wanted = Cells.ToDictionary(cell => cell, _ => new List<WorkstreamCardViewModel>());
        var byPlace = Cells.ToDictionary(cell => (cell.Column, cell.Area));

        foreach (var card in _all)
        {
            if (byPlace.TryGetValue((ColumnOf(card), RowOf(card)), out var cell))
            {
                wanted[cell].Add(card);
            }
        }

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

        if (SelectedCard is { } selected && !Cards.Contains(selected))
        {
            SelectedCard = null;
        }

        foreach (var row in Rows)
        {
            row.RaiseCounts();
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

        StageHeaders.Clear();
        Rows.Clear();

        foreach (var name in rows)
        {
            var row = new BoardRowViewModel(name)
            {
                IsCollapsed = foldedRows.Any(folded => BoardAreas.Same(name, folded)),
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
            };

            header.ToggleCommand = new RelayCommand(_ => ToggleStage(header));
            StageHeaders.Add(header);
        }
    }

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
    }

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

    /// <summary>Moves a card to another cell of the grid and persists the change.</summary>
    /// <param name="destination">The stage to move it to.</param>
    /// <param name="area">
    /// The area to move it to. Null keeps the area it is in, which is what dragging within a row
    /// means — and what every drop meant before the board had rows.
    /// </param>
    /// <param name="index">
    /// Where in the cell to put it, or null for the end. Position is not decoration: a cell's order
    /// is the order its tabs open in when it is started, so dragging a card up brings its session
    /// forward in the morning.
    /// </param>
    public void Move(string key, string destination, string? area = null, int? index = null)
    {
        var card = Card(key);

        if (card is null || !CanDrag)
        {
            return;
        }

        if (RowsBy != BoardRowsBy.Area && area is not null && !string.Equals(area, RowOf(card), StringComparison.Ordinal))
        {
            return;
        }

        var row = area ?? (RowsBy == BoardRowsBy.Area ? card.Workstream.Area ?? card.Area : RowOf(card));
        var cell = Cells.FirstOrDefault(candidate =>
            candidate.Column == destination && BoardAreas.Same(candidate.Area, row));

        if (cell is null)
        {
            return;
        }

        var cameFrom = cell.Cards.IndexOf(card) is var found && found >= 0 ? found : (int?)null;

        foreach (var other in Cells)
        {
            other.Cards.Remove(card);
        }

        var at = LaneInsertion.At(index ?? cell.Cards.Count + 1, cameFrom, cell.Cards.Count);
        var newArea = RowsBy == BoardRowsBy.Area ? cell.Area : card.Area;
        var stayed = cameFrom == at && card.Column == destination && BoardAreas.Same(newArea, card.Area);

        cell.Cards.Insert(at, card);

        if (stayed)
        {
            return;
        }

        card.Workstream = card.Workstream with { Column = destination, Area = newArea };

        Persist(card);
        Renumber(cell);
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
                card.Workstream.StatusDocumentPath, card.Workstream.LastSessionId, now, now));
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
                  ReopenTomorrow = card.Workstream.ReopenTomorrow,
                  JiraKeys = card.JiraKeys,
                  RemovedJiraKeys = card.Workstream.RemovedJiraKeys,
                  WindowGroup = card.Workstream.WindowGroup,
                  Area = card.Workstream.Area,
                  StatusDocumentPath = card.Workstream.StatusDocumentPath,
                  LastSessionId = card.Workstream.LastSessionId,
                  EveryStartPrompt = card.Workstream.EveryStartPrompt,
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
    }
}
