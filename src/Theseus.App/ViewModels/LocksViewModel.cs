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
    private bool _isOpen;

    /// <summary>Creates the view over a lock book, refreshed on every change and twice a minute for the times.</summary>
    /// <param name="colourOf">
    /// The tab colour of the workstream a ticket belongs to, so each entry carries the colour of the
    /// session it came from (Nigel, 2026-10-06), or null when no card matches.
    /// </param>
    public LocksViewModel(LockBook book, Func<DateTimeOffset> clock, Func<LockTicket, string?>? colourOf = null)
    {
        _book = book;
        _clock = clock;
        _colourOf = colourOf;
        _dispatcher = Dispatcher.CurrentDispatcher;
        Rows = new ObservableCollection<LockRowViewModel>(LockNames.All.Select(name =>
            new LockRowViewModel(name, Release, MoveUp, MoveDown, Cancel)));

        ToggleCommand = new RelayCommand(_ => IsOpen = !IsOpen);
        _book.Changed += OnChanged;
        _timer = new DispatcherTimer(TimeSpan.FromSeconds(30), DispatcherPriority.Background, (_, _) => Refresh(), _dispatcher);
        _timer.Start();
        Refresh();
    }

    /// <summary>One row per lock, always in the same order.</summary>
    public ObservableCollection<LockRowViewModel> Rows { get; }

    /// <summary>Whether the locks panel is open at the side of the board.</summary>
    public bool IsOpen
    {
        get => _isOpen;
        set => Set(ref _isOpen, value);
    }

    /// <summary>Opens or closes the locks panel.</summary>
    public RelayCommand ToggleCommand { get; }

    /// <summary>Holders and waiters across every lock, for the command bar button.</summary>
    public int Busy => Rows.Sum(row => (row.IsHeld ? 1 : 0) + row.Waiting.Count);

    /// <summary>The command bar button's label: "Locks", with how many are holding or waiting.</summary>
    public string ButtonText => Busy == 0 ? "Locks" : $"Locks {Busy}";

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
    public bool HasLongHold => Rows.Any(row => row.IsLong);

    /// <summary>Brings every row into line with the lock book.</summary>
    public void Refresh()
    {
        var tickets = _book.Snapshot();
        var now = _clock();

        foreach (var row in Rows)
        {
            row.Update(tickets, now, _colourOf);
        }

        Raise(nameof(Busy));
        Raise(nameof(ButtonText));
        Raise(nameof(Summary));
        Raise(nameof(HasLongHold));
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

    private void Release(string name) => OffTheUiThread(() => _book.ForceRelease(name), $"release the {name} lock");

    private void MoveUp(LockWaiterViewModel waiter) =>
        OffTheUiThread(() => _book.Reorder(waiter.Token, waiter.Position - 2), $"move {waiter.Who} up");

    private void MoveDown(LockWaiterViewModel waiter) =>
        OffTheUiThread(() => _book.Reorder(waiter.Token, waiter.Position), $"move {waiter.Who} down");

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
