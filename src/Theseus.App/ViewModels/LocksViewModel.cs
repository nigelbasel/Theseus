using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Threading;
using Theseus.Core.Diagnostics;
using Theseus.Core.Locks;

namespace Theseus.App.ViewModels;

/// <summary>
/// The machine's locks on the board (Nigel, 2026-10-06): a panel at the side of the board, opened from the
/// command bar like the live sessions panel, and a count in the status bar. Nigel reorders, cancels and
/// releases from here and nowhere else. Every change to the book goes off the UI thread, because a
/// change can clear up after a dead test run, which waits on Docker.
/// </summary>
public sealed class LocksViewModel : ObservableObject, IDisposable
{
    /// <summary>A hold longer than this is flagged on the board.</summary>
    public static readonly TimeSpan LongHold = TimeSpan.FromMinutes(90);

    private readonly LockBook _book;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Func<LockTicket, string?>? _colourOf;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _timer;
    private readonly ITestContainers? _containers;
    private readonly Func<LockTicket, DateTimeOffset, string?>? _idleOf;
    private readonly LockFolds _folds = new();
    private IReadOnlyList<TestContainer> _lastContainers = [];
    private int _checking;
    private bool _isOpen;

    /// <summary>Creates the view over a lock book, refreshed on every change and twice a minute for the times.</summary>
    /// <param name="colourOf">
    /// The tab colour of the workstream a ticket belongs to, so each entry carries the colour of the
    /// session it came from (Nigel, 2026-10-06), or null when no card matches.
    /// </param>
    /// <param name="containers">Docker's Testcontainers containers, for the unowned list; null leaves it empty.</param>
    /// <param name="idleOf">For a session hold whose session has idled half an hour or gone, how long; otherwise null.</param>
    public LocksViewModel(LockBook book, Func<DateTimeOffset> clock, Func<LockTicket, string?>? colourOf = null, ITestContainers? containers = null,
        Func<LockTicket, DateTimeOffset, string?>? idleOf = null)
    {
        _idleOf = idleOf;
        _book = book;
        _clock = clock;
        _colourOf = colourOf;
        _containers = containers;
        _dispatcher = Dispatcher.CurrentDispatcher;
        Rows = [];
        NeedsRulingFold = _folds.For("section:needs-ruling", open: false);
        RulingsFold = _folds.For("section:rulings", open: false);
        DeadlocksFold = _folds.For("section:deadlocks", open: false);
        UnownedFold = _folds.For("section:unowned", open: true);

        ToggleCommand = new RelayCommand(_ => IsOpen = !IsOpen);
        OpenCommand = new RelayCommand(_ => IsOpen = true);
        _book.Changed += OnChanged;
        _timer = new DispatcherTimer(TimeSpan.FromSeconds(30), DispatcherPriority.Background, (_, _) =>
        {
            Refresh();
            CheckContainers();
        }, _dispatcher);
        _timer.Start();
        Refresh();
        CheckContainers();
    }

    /// <summary>One row per lock, always in the same order.</summary>
    public ObservableCollection<LockRowViewModel> Rows { get; }

    /// <summary>Whether the locks panel is open at the side of the board.</summary>
    public bool IsOpen
    {
        get => _isOpen;
        set => Set(ref _isOpen, value);
    }

    /// <summary>
    /// Testcontainers containers no lock holder accounts for, oldest first, for Nigel to remove. Never
    /// removed automatically: a leftover cannot be told apart from a run in progress outside the lock.
    /// </summary>
    public ObservableCollection<UnownedContainerViewModel> Unowned { get; } = [];

    /// <summary>Requests refused because granting them would have closed a wait cycle, newest first.</summary>
    public ObservableCollection<string> StoppedDeadlocks { get; } = [];

    /// <summary>Rulings the arbiter or Nigel made on resources, each with Nigel's overturn.</summary>
    public ObservableCollection<LockRulingViewModel> Rulings { get; } = [];

    /// <summary>Resources asked for that nobody has ruled on yet; until then each is held alone.</summary>
    public string NeedsRuling { get; private set; } = string.Empty;

    /// <summary>Where the panel's folds are kept between runs (Nigel, 2026-11-26); without one, they last as long as the panel.</summary>
    public Theseus.Core.Settings.ISettingsStore? Settings
    {
        get => _folds.Settings;
        set => _folds.Settings = value;
    }

    /// <summary>Unfolds every lock, its queue, and every section around them.</summary>
    public RelayCommand ExpandAllCommand => _folds.ExpandAllCommand;

    /// <summary>Folds every lock and section, leaving a list of the lock names.</summary>
    public RelayCommand CollapseAllCommand => _folds.CollapseAllCommand;

    public FoldState NeedsRulingFold { get; }

    public FoldState RulingsFold { get; }

    public FoldState DeadlocksFold { get; }

    /// <summary>Open unless the owner folds it: containers nobody owns wait on him.</summary>
    public FoldState UnownedFold { get; }

    /// <summary>Opens or closes the locks panel.</summary>
    public RelayCommand ToggleCommand { get; }

    /// <summary>Opens the locks panel, from the Chains tab's header.</summary>
    public RelayCommand OpenCommand { get; }

    /// <summary>Holders and waiters across every lock, for the command bar button.</summary>
    public int Busy { get; private set; }

    /// <summary>The command bar button's label: "Locks", with how many are holding or waiting.</summary>
    public string ButtonText =>
        (Busy == 0 ? "Locks" : $"Locks {Busy}") + (Unowned.Count == 0 ? string.Empty : $" · {Unowned.Count} unowned");

    /// <summary>The status bar's line: each busy lock, or that all are free.</summary>
    public string Summary
    {
        get
        {
            var busy = Rows.Select(row => row.Summary).Where(text => text.Length > 0).ToList();

            return busy.Count == 0 ? "Locks: all free" : string.Join("  ·  ", busy);
        }
    }

    /// <summary>Whether any hold is longer than <see cref="LongHold"/>.</summary>
    public bool HasLongHold => Rows.Any(row => row.IsLong || row.HasAmberHold);

    /// <summary>Whether the toolbar's count should turn amber: a long hold, or a container nobody owns.</summary>
    public bool IsUrgent => HasLongHold || Unowned.Count > 0;

    /// <summary>Brings every row into line with the lock book.</summary>
    public void Refresh()
    {
        var tickets = _book.Snapshot();
        var now = _clock();
        var table = _book.Table;

        SyncRows(tickets, table);
        List<StoppedDeadlock> deadlocks = [.. _book.StoppedDeadlocks().Reverse().Take(10)];

        foreach (var row in Rows)
        {
            row.Update(tickets, now, _colourOf, table, _idleOf is null ? null : ticket => _idleOf(ticket, now),
                deadlocks.Count(stopped => stopped.Resources.Contains(row.Name, StringComparer.Ordinal)));
        }

        Busy = tickets.Count;
        Replace(StoppedDeadlocks, [.. deadlocks
            .Select(stopped => $"{stopped.WhenUtc.ToLocalTime():HH:mm}  {stopped.Detail}")]);
        SyncRulings(table);
        NeedsRuling = string.Join(", ", _book.NeedsRuling());
        Raise(nameof(NeedsRuling));

        UpdateUnowned(UnownedContainers.From(_lastContainers, tickets), tickets, now);

        Raise(nameof(Busy));
        Raise(nameof(ButtonText));
        Raise(nameof(Summary));
        Raise(nameof(HasLongHold));
        Raise(nameof(IsUrgent));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _timer.Stop();
        _book.Changed -= OnChanged;
    }

    /// <summary>A span as the strip writes it: 45s, 12m, 1h05.</summary>
    public static string Say(TimeSpan span) => span switch
    {
        { TotalMinutes: < 1 } => $"{Math.Max(0, (int)span.TotalSeconds)}s",
        { TotalHours: < 1 } => $"{(int)span.TotalMinutes}m",
        _ => $"{(int)span.TotalHours}h{span.Minutes.ToString("00", CultureInfo.InvariantCulture)}",
    };

    private void OnChanged(object? sender, EventArgs e) => _dispatcher.BeginInvoke(Refresh);

    // A row for every resource the table names and every one a request names, in the table's order, so a resource
    // keeps its place on the panel while it is in use.
    private void SyncRows(IReadOnlyList<LockTicket> tickets, ResourceTable table)
    {
        List<string> names = [.. table.Rules.Where(rule => !rule.Name.EndsWith(":*", StringComparison.Ordinal)).Select(rule => rule.Name)
            .Concat(tickets.SelectMany(ticket => ticket.Claims).Select(claim => claim.Resource))
            .Distinct(StringComparer.Ordinal)];

        for (var index = Rows.Count - 1; index >= 0; index--)
        {
            if (!names.Contains(Rows[index].Name))
            {
                Rows.RemoveAt(index);
            }
        }

        for (var index = 0; index < names.Count; index++)
        {
            var existing = Rows.FirstOrDefault(row => row.Name == names[index]);

            if (existing is null)
            {
                Rows.Insert(index, new LockRowViewModel(names[index], Release, MoveUp, MoveDown, Cancel, _folds));
            }
            else if (Rows.IndexOf(existing) != index)
            {
                Rows.Move(Rows.IndexOf(existing), index);
            }
        }
    }

    private void SyncRulings(ResourceTable table)
    {
        var ruled = table.Rules.Where(rule => rule.RuledBy is not null).ToList();

        if (ruled.Count == Rulings.Count && ruled.Select(rule => rule.Name).SequenceEqual(Rulings.Select(ruling => ruling.Name)))
        {
            return;
        }

        Rulings.Clear();

        foreach (var rule in ruled)
        {
            var clashes = rule.ClashesWith.Count == 0 ? "nothing" : string.Join(", ", rule.ClashesWith);
            Rulings.Add(new LockRulingViewModel(rule.Name,
                $"{rule.Name}: {rule.Capacity} slot(s), clashes with {clashes}. {rule.Why} ({rule.RuledBy}{(rule.RuledUtc is { } when ? $", {when.ToLocalTime():d MMM HH:mm}" : string.Empty)})",
                Overturn));
        }
    }

    private static void Replace(ObservableCollection<string> target, IReadOnlyList<string> items)
    {
        if (target.SequenceEqual(items))
        {
            return;
        }

        target.Clear();

        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private void Overturn(string name) => OffTheUiThread(() => _book.Overturn(name), $"overturn the ruling on {name}");

    private void UpdateUnowned(IReadOnlyList<TestContainer> unowned, IReadOnlyList<LockTicket> tickets, DateTimeOffset now)
    {
        for (var index = Unowned.Count - 1; index >= 0; index--)
        {
            if (!unowned.Any(container => container.Id == Unowned[index].Id))
            {
                Unowned.RemoveAt(index);
            }
        }

        for (var index = 0; index < unowned.Count; index++)
        {
            var container = unowned[index];
            var existing = Unowned.FirstOrDefault(entry => entry.Id == container.Id);

            if (existing is null)
            {
                existing = new UnownedContainerViewModel(container.Id, container.Name, container.Image, Remove);
                Unowned.Insert(index, existing);
            }
            else if (Unowned.IndexOf(existing) != index)
            {
                Unowned.Move(Unowned.IndexOf(existing), index);
            }

            existing.Age = Say(now - container.CreatedUtc);
            existing.Status = container.Status;
            existing.StartedDuring = UnownedContainers.StartedDuring(container, tickets) is { Count: > 0 } during
                ? $"Started while these held no SQL containers: {string.Join(", ", during)}. One of them started containers it did not declare."
                : string.Empty;
        }
    }

    private void CheckContainers()
    {
        if (_containers is null || Interlocked.Exchange(ref _checking, 1) == 1)
        {
            return;
        }

        Task.Run(() =>
        {
            try
            {
                var listed = _containers.List();
                _dispatcher.BeginInvoke(() =>
                {
                    _lastContainers = listed;
                    Refresh();
                });
            }
            catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or TimeoutException)
            {
                TheseusLog.Debug("Could not list Testcontainers containers: {Reason}", exception.Message);
            }
            finally
            {
                Interlocked.Exchange(ref _checking, 0);
            }
        });
    }

    private void Remove(UnownedContainerViewModel container)
    {
        if (_containers is null)
        {
            return;
        }

        Task.Run(() =>
        {
            try
            {
                _containers.Remove(container.Id);
                TheseusLog.Warning("Nigel removed unowned container {Name} ({Id}, {Image}) from the locks panel", container.Name, container.Id, container.Image);
            }
            catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or TimeoutException)
            {
                TheseusLog.Warning(exception, "Could not remove container {Id}", container.Id);
            }
        }).ContinueWith(_ => _dispatcher.BeginInvoke(CheckContainers));
    }

    private void Release(string token) => OffTheUiThread(() => _book.ForceReleaseTicket(token), "release a hold");

    private void MoveUp(LockWaiterViewModel waiter) =>
        OffTheUiThread(() => waiter.Previous is { } previous && _book.MoveNextTo(waiter.Token, previous, after: false), $"move {waiter.Who} up");

    private void MoveDown(LockWaiterViewModel waiter) =>
        OffTheUiThread(() => waiter.Next is { } next && _book.MoveNextTo(waiter.Token, next, after: true), $"move {waiter.Who} down");

    private void Cancel(LockWaiterViewModel waiter) =>
        OffTheUiThread(() => _book.Cancel(waiter.Token), $"cancel {waiter.Who}'s request");

    private static void OffTheUiThread(Func<bool> change, string what) =>
        Task.Run(() =>
        {
            if (!change())
            {
                TheseusLog.Information("Could not {What}: it had already changed", what);
            }
        });
}
