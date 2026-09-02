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

    /// <summary>Creates the board over its stores.</summary>
    public BoardViewModel(IPlanStore plans, IBoardStore board, ISettingsStore settings)
    {
        _plans = plans;
        _board = board;
        _settings = settings;
        _showArchived = settings.Read().ShowArchived;

        Rebuild(Lanes(), Areas());

        RefreshCommand = new RelayCommand(_ => Refresh());
    }

    /// <summary>The stages, as configured: the columns of the grid, and its headings.</summary>
    public ObservableCollection<string> Stages { get; } = [];

    /// <summary>The areas, as configured: the rows of the grid, each holding a cell per stage.</summary>
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

        var (lanes, areas) = SyncGrid();

        // Which cards were ticked, before the rebuild throws every card away and makes new ones.
        // A refresh runs on a timer and after every start, stop and archive, so without this a
        // selection would quietly empty itself between choosing the cards and pressing the button.
        var chosen = new HashSet<string>(
            Cards.Where(card => card.IsSelected).Select(card => card.Key),
            StringComparer.OrdinalIgnoreCase);

        // Folded cards carry across the same way, and for the same reason: a refresh that quietly
        // unfolded the board would undo the view a moment after it was asked for.
        var folded = new HashSet<string>(
            Cards.Where(card => card.IsCollapsed).Select(card => card.Key),
            StringComparer.OrdinalIgnoreCase);

        foreach (var cell in Cells)
        {
            cell.Cards.Clear();
        }

        foreach (var workstream in workstreams)
        {
            var lane = BoardLanes.Resolve(workstream.Column, lanes);
            var area = BoardAreas.Resolve(workstream.Area, areas);

            Cells.First(cell => cell.Column == lane && cell.Area == area)
                .Cards.Add(new WorkstreamCardViewModel(workstream with { Column = lane, Area = area })
                {
                    IsSelected = chosen.Contains(workstream.Key),
                    IsCollapsed = folded.Contains(workstream.Key),
                });
        }

        foreach (var row in Rows)
        {
            row.RaiseCounts();
        }

        Status = unreadable == 0
            ? $"{workstreams.Count} workstreams"
            : $"{workstreams.Count} workstreams · {unreadable} plan file(s) unreadable";
    }

    /// <summary>The configured stages, cleaned up.</summary>
    private IReadOnlyList<string> Lanes() => BoardLanes.Sanitise(_settings.Read().Lanes);

    /// <summary>The configured areas, cleaned up.</summary>
    private IReadOnlyList<string> Areas() => BoardAreas.Sanitise(_settings.Read().Areas);

    /// <summary>
    /// Brings the grid into line with the settings, rebuilding it only when it has actually changed.
    /// A rebuild replaces every row and cell, so doing it on every refresh would throw away each
    /// row's collapsed state, its Start command and its scroll position for nothing.
    /// </summary>
    private (IReadOnlyList<string> Lanes, IReadOnlyList<string> Areas) SyncGrid()
    {
        var lanes = Lanes();
        var areas = Areas();

        var same = Stages.SequenceEqual(lanes, StringComparer.Ordinal)
                   && Rows.Select(row => row.Area).SequenceEqual(areas, StringComparer.Ordinal);

        if (!same)
        {
            Rebuild(lanes, areas);
            LanesChanged?.Invoke(this, EventArgs.Empty);
        }

        return (lanes, areas);
    }

    private void Rebuild(IReadOnlyList<string> lanes, IReadOnlyList<string> areas)
    {
        var collapsed = _settings.Read().CollapsedAreas;

        Stages.Clear();
        Rows.Clear();

        foreach (var lane in lanes)
        {
            Stages.Add(lane);
        }

        foreach (var area in areas)
        {
            var row = new BoardRowViewModel(area)
            {
                IsCollapsed = collapsed.Any(name => BoardAreas.Same(area, name)),
            };

            foreach (var lane in lanes)
            {
                row.Cells.Add(new ColumnViewModel(lane, area));
            }

            Rows.Add(row);
        }
    }

    /// <summary>Folds a row away, or opens it, and remembers which way it was left.</summary>
    public void ToggleRow(BoardRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        row.IsCollapsed = !row.IsCollapsed;

        _settings.Write(_settings.Read() with
        {
            CollapsedAreas = [.. Rows.Where(other => other.IsCollapsed).Select(other => other.Area)],
        });
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

        if (card is null)
        {
            return;
        }

        var row = area ?? card.Workstream.Area ?? card.Area;
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
        var stayed = cameFrom == at && card.Column == destination && BoardAreas.Same(cell.Area, card.Area);

        cell.Cards.Insert(at, card);

        if (stayed)
        {
            return;
        }

        card.Workstream = card.Workstream with { Column = destination, Area = cell.Area };

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
                  ReopenTomorrow = card.Workstream.ReopenTomorrow,
                  JiraKeys = card.JiraKeys,
                  RemovedJiraKeys = card.Workstream.RemovedJiraKeys,
                  WindowGroup = card.Workstream.WindowGroup,
                  Area = card.Workstream.Area,
                  StatusDocumentPath = card.Workstream.StatusDocumentPath,
                  LastSessionId = card.Workstream.LastSessionId,
                  UpdatedUtc = now,
              }
            : new WorkstreamSidecar(card.Key, card.Column, card.Workstream.IsBlocked, IsArchived: false,
                ColorIsManual: true, card.Workstream.ReopenTomorrow, card.JiraKeys,
                card.Workstream.StatusDocumentPath, card.Workstream.LastSessionId, now, now)
              {
                  RemovedJiraKeys = card.Workstream.RemovedJiraKeys,
                  WindowGroup = card.Workstream.WindowGroup,
                  Area = card.Workstream.Area,
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
