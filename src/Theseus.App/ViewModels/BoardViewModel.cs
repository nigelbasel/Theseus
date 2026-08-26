using System.Collections.ObjectModel;
using Theseus.Core.Abstractions;
using Theseus.Core.Domain;

namespace Theseus.App.ViewModels;

/// <summary>The board: four columns of cards, rebuilt from the plans on disk.</summary>
public sealed class BoardViewModel : ObservableObject
{
    private readonly IPlanStore _plans;
    private readonly IBoardStore _board;
    private string _status = string.Empty;
    private WorkstreamCardViewModel? _selectedCard;

    /// <summary>Creates the board over its two stores.</summary>
    public BoardViewModel(IPlanStore plans, IBoardStore board)
    {
        _plans = plans;
        _board = board;

        Columns =
        [
            new ColumnViewModel(BoardColumn.Today, "Today"),
            new ColumnViewModel(BoardColumn.InFlight, "In flight"),
            new ColumnViewModel(BoardColumn.Parked, "Parked"),
            new ColumnViewModel(BoardColumn.Done, "Done"),
        ];

        RefreshCommand = new RelayCommand(_ => Refresh());
    }

    /// <summary>The four columns, fixed in this version.</summary>
    public ObservableCollection<ColumnViewModel> Columns { get; }

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

        foreach (var column in Columns)
        {
            column.Cards.Clear();

            foreach (var workstream in workstreams.Where(w => w.Column == column.Column))
            {
                column.Cards.Add(new WorkstreamCardViewModel(workstream));
            }
        }

        Status = unreadable == 0
            ? $"{workstreams.Count} workstreams"
            : $"{workstreams.Count} workstreams · {unreadable} plan file(s) unreadable";
    }

    /// <summary>Moves a card to another column and persists the change.</summary>
    public void Move(string key, BoardColumn destination)
    {
        var card = Card(key);

        if (card is null || card.Column == destination)
        {
            return;
        }

        foreach (var column in Columns)
        {
            column.Cards.Remove(card);
        }

        card.Workstream = card.Workstream with { Column = destination };
        Columns.First(column => column.Column == destination).Cards.Add(card);

        Persist(card);
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
