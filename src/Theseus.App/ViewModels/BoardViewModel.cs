using System.Collections.ObjectModel;
using Theseus.Core.Abstractions;
using Theseus.Core.Domain;
using Theseus.Core.Settings;

namespace Theseus.App.ViewModels;

/// <summary>The board: lanes of cards, rebuilt from the plans on disk.</summary>
public sealed class BoardViewModel : ObservableObject
{
    private readonly IPlanStore _plans;
    private readonly IBoardStore _board;
    private readonly ISettingsStore _settings;
    private string _status = string.Empty;
    private WorkstreamCardViewModel? _selectedCard;

    /// <summary>Creates the board over its stores.</summary>
    public BoardViewModel(IPlanStore plans, IBoardStore board, ISettingsStore settings)
    {
        _plans = plans;
        _board = board;
        _settings = settings;

        Columns = [.. Lanes().Select(lane => new ColumnViewModel(lane))];

        RefreshCommand = new RelayCommand(_ => Refresh());
    }

    /// <summary>The lanes, as configured. Rebuilt when the configured lanes change.</summary>
    public ObservableCollection<ColumnViewModel> Columns { get; }

    /// <summary>Raised when the lanes themselves have changed, not merely the cards in them.</summary>
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
            .Where(workstream => !workstream.IsArchived)
            .ToList();

        var lanes = SyncLanes();

        foreach (var column in Columns)
        {
            column.Cards.Clear();
        }

        foreach (var workstream in workstreams)
        {
            var lane = BoardLanes.Resolve(workstream.Column, lanes);

            Columns.First(column => column.Column == lane)
                .Cards.Add(new WorkstreamCardViewModel(workstream with { Column = lane }));
        }

        Status = unreadable == 0
            ? $"{workstreams.Count} workstreams"
            : $"{workstreams.Count} workstreams · {unreadable} plan file(s) unreadable";
    }

    /// <summary>The configured lanes, cleaned up.</summary>
    private IReadOnlyList<string> Lanes() => BoardLanes.Sanitise(_settings.Read().Lanes);

    /// <summary>
    /// Brings the lanes into line with the settings, rebuilding them only when they have actually
    /// changed. A rebuild replaces every lane object, so doing it on every refresh would throw away
    /// each lane's Start command and scroll position for nothing.
    /// </summary>
    private IReadOnlyList<string> SyncLanes()
    {
        var lanes = Lanes();

        if (Columns.Select(column => column.Column).SequenceEqual(lanes, StringComparer.Ordinal))
        {
            return lanes;
        }

        Columns.Clear();

        foreach (var lane in lanes)
        {
            Columns.Add(new ColumnViewModel(lane));
        }

        LanesChanged?.Invoke(this, EventArgs.Empty);

        return lanes;
    }

    /// <summary>Moves a card to another lane and persists the change.</summary>
    /// <param name="index">
    /// Where in the lane to put it, or null for the end. Position is not decoration: a lane's order
    /// is the order its tabs open in when the lane or the board is started, so dragging a card up
    /// the lane brings its session forward in the morning.
    /// </param>
    public void Move(string key, string destination, int? index = null)
    {
        var card = Card(key);
        var lane = Columns.FirstOrDefault(column => column.Column == destination);

        if (card is null || lane is null)
        {
            return;
        }

        var cameFrom = lane.Cards.IndexOf(card) is var found && found >= 0 ? found : (int?)null;

        foreach (var column in Columns)
        {
            column.Cards.Remove(card);
        }

        var at = LaneInsertion.At(index ?? lane.Cards.Count + 1, cameFrom, lane.Cards.Count);

        if (cameFrom == at && card.Column == destination)
        {
            lane.Cards.Insert(at, card);

            return;
        }

        card.Workstream = card.Workstream with { Column = destination };
        lane.Cards.Insert(at, card);

        Persist(card);
        Renumber(lane);
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
        foreach (var other in Columns.SelectMany(column => column.Cards))
        {
            other.IsSelected = ReferenceEquals(other, card);
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

    /// <summary>Finds a card by its workstream key.</summary>
    public WorkstreamCardViewModel? Card(string key) =>
        Columns.SelectMany(column => column.Cards)
            .FirstOrDefault(card => string.Equals(card.Key, key, StringComparison.OrdinalIgnoreCase));

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
                  StatusDocumentPath = card.Workstream.StatusDocumentPath,
                  LastSessionId = card.Workstream.LastSessionId,
                  UpdatedUtc = now,
              }
            : new WorkstreamSidecar(card.Key, card.Column, card.Workstream.IsBlocked, IsArchived: false,
                ColorIsManual: true, card.Workstream.ReopenTomorrow, card.JiraKeys,
                card.Workstream.StatusDocumentPath, card.Workstream.LastSessionId, now, now);

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
