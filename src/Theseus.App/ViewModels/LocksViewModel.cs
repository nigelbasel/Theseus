using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Threading;
using Theseus.Core.Diagnostics;
using Theseus.Core.Locks;

namespace Theseus.App.ViewModels;

/// <summary>
/// The machine's locks on the board (Nigel, 2026-10-06): a strip under the command bar showing each
/// lock that is held or has a queue, and a count in the status bar. Nigel reorders, cancels and
/// releases from here and nowhere else. Every change to the book goes off the UI thread, because a
/// change can clear up after a dead test run, which waits on Docker.
/// </summary>
public sealed class LocksViewModel : ObservableObject, IDisposable
{
    /// <summary>A hold longer than this is flagged on the board.</summary>
    public static readonly TimeSpan LongHold = TimeSpan.FromMinutes(90);

    private readonly LockBook _book;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _timer;

    /// <summary>Creates the view over a lock book, refreshed on every change and twice a minute for the times.</summary>
    public LocksViewModel(LockBook book, Func<DateTimeOffset> clock)
    {
        _book = book;
        _clock = clock;
        _dispatcher = Dispatcher.CurrentDispatcher;
        Rows = new ObservableCollection<LockRowViewModel>(LockNames.All.Select(name =>
            new LockRowViewModel(name, Release, MoveUp, MoveDown, Cancel)));

        _book.Changed += OnChanged;
        _timer = new DispatcherTimer(TimeSpan.FromSeconds(30), DispatcherPriority.Background, (_, _) => Refresh(), _dispatcher);
        _timer.Start();
        Refresh();
    }

    /// <summary>One row per lock, always in the same order.</summary>
    public ObservableCollection<LockRowViewModel> Rows { get; }

    /// <summary>Whether any lock is held or has a queue, which is when the strip shows.</summary>
    public bool IsVisible => Rows.Any(row => row.IsActive);

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
            row.Update(tickets, now);
        }

        Raise(nameof(IsVisible));
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
