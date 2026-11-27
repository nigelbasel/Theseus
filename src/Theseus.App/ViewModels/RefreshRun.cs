namespace Theseus.App.ViewModels;

/// <summary>
/// Refresh, shown working (board-small-changes item 1, Nigel, 2026-11-26: "it doesnt appear to do anything while it does
/// the check"): busy, with "Refreshing…" in the status bar, from the press until the board is read again and the update
/// check answers, then what it found. A press while busy does nothing, so presses never queue. The CI poll Refresh also
/// starts is not waited for: nothing on screen waits on it.
/// </summary>
public sealed class RefreshRun : ObservableObject
{
    /// <summary>The status line while it runs.</summary>
    public const string Started = "Refreshing…";

    private readonly Action _refresh;
    private readonly Func<Task<string>> _check;
    private readonly Func<string> _notice;
    private readonly Action<string> _say;
    private readonly Func<Task> _letTheWindowDraw;
    private bool _isBusy;

    /// <summary>Creates it.</summary>
    /// <param name="refresh">Reads the board again, on the window's thread.</param>
    /// <param name="check">The update check: what it found, in a sentence.</param>
    /// <param name="notice">The status line as it is now.</param>
    /// <param name="say">Sets the status line.</param>
    /// <param name="letTheWindowDraw">Yields to the window so the busy cursor and status line show before the board is read.</param>
    public RefreshRun(Action refresh, Func<Task<string>> check, Func<string> notice, Action<string> say, Func<Task> letTheWindowDraw)
    {
        _refresh = refresh ?? throw new ArgumentNullException(nameof(refresh));
        _check = check ?? throw new ArgumentNullException(nameof(check));
        _notice = notice ?? throw new ArgumentNullException(nameof(notice));
        _say = say ?? throw new ArgumentNullException(nameof(say));
        _letTheWindowDraw = letTheWindowDraw ?? throw new ArgumentNullException(nameof(letTheWindowDraw));
        // Awaited in an async void handler, so a refresh that throws reaches the app's handler as it did before.
        Command = new RelayCommand(async _ => await RunAsync(), _ => !_isBusy);
    }

    /// <summary>Refresh (F5, the toolbar and View › Refresh), disabled while it runs.</summary>
    public RelayCommand Command { get; }

    /// <summary>Whether a refresh is running: the window shows the busy cursor.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (Set(ref _isBusy, value))
            {
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    /// <summary>Runs one refresh, unless one is running already.</summary>
    public async Task RunAsync()
    {
        if (_isBusy)
        {
            return;
        }

        IsBusy = true;
        _say(Started);

        try
        {
            await _letTheWindowDraw();
            _refresh();
            var found = $"Refreshed. {await _check()}";

            // A warning the refresh itself gave stays: the finding follows it rather than replacing it.
            var now = _notice();
            _say(string.IsNullOrWhiteSpace(now) || string.Equals(now, Started, StringComparison.Ordinal) ? found : $"{now} {found}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
