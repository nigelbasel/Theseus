using System.Diagnostics;
using System.Collections.ObjectModel;
using Theseus.App.Services;
using Theseus.Core.Sessions;
using Theseus.Core.Diagnostics;

namespace Theseus.App.ViewModels;

/// <summary>
/// Every Claude Code session running on this machine, whether or not Theseus started it. The
/// registry is the contract, so hand-started sessions appear here on equal terms with launched ones.
/// </summary>
public sealed class SessionsViewModel : ObservableObject
{
    private static readonly TimeSpan SlowTitle = TimeSpan.FromSeconds(1);

    private readonly ISessionRegistryReader _registry;
    private readonly IConsoleTitleReader _titles;
    private int _corpses = -1;
    private string _status = string.Empty;

    /// <summary>Creates the panel over the session registry.</summary>
    public SessionsViewModel(ISessionRegistryReader registry, IConsoleTitleReader titles)
    {
        _registry = registry;
        _titles = titles;
        RefreshCommand = new RelayCommand(_ => Refresh());
        FocusCommand = new RelayCommand(Focus, parameter => parameter is LiveSessionViewModel);
    }

    /// <summary>The live sessions, newest first.</summary>
    public ObservableCollection<LiveSessionViewModel> Sessions { get; } = [];

    /// <summary>Re-reads the registry.</summary>
    public RelayCommand RefreshCommand { get; }

    /// <summary>Brings a session's terminal window to the front.</summary>
    public RelayCommand FocusCommand { get; }

    /// <summary>What the panel header says.</summary>
    public string Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    /// <summary>The registry as it was at the last refresh.</summary>
    public SessionRegistrySnapshot Snapshot { get; private set; } = SessionRegistrySnapshot.Empty;

    /// <summary>Reads the registry and rebuilds the list.</summary>
    public void Refresh()
    {
        var snapshot = _registry.Read();

        // Entries whose process has gone. Counted on every read and, until 2026-09-07, thrown away —
        // which is why "three sessions were resumed into second tabs" took an hour to explain rather
        // than a minute. Logged when the number changes, not on every poll.
        if (snapshot.CorpseCount != _corpses)
        {
            _corpses = snapshot.CorpseCount;

            TheseusLog.Information(
                "The session registry holds {Corpses} entry(s) whose process has gone, and {Live} live",
                snapshot.CorpseCount, snapshot.Live.Count);
        }

        Snapshot = snapshot;

        // Rows are kept and brought up to date, not cleared and rebuilt. Clearing and refilling the
        // list on every refresh raised a change for every row each time, and something was listening
        // to each one: the taskbar jump list was committed on every change, a blocking call into the
        // shell that froze the board on 2026-09-15. A row is matched on its process as well as its
        // session, because a forked transcript puts two live processes on one session id.
        var rows = new List<LiveSessionViewModel>();

        foreach (var session in snapshot.Live
                     .Where(session => session.IsAdoptable)
                     .OrderByDescending(session => session.StartedUtc))
        {
            var watch = Stopwatch.StartNew();
            var title = _titles.Read(session.Pid);

            if (watch.Elapsed > SlowTitle)
            {
                TheseusLog.Information("Reading the console title of process {Pid} took {Seconds:0.0}s",
                    session.Pid, watch.Elapsed.TotalSeconds);
            }

            var row = Sessions.FirstOrDefault(existing => existing.Pid == session.Pid
                && string.Equals(existing.SessionId, session.SessionId, StringComparison.OrdinalIgnoreCase));

            if (row is null)
            {
                row = new LiveSessionViewModel(session, title);
            }
            else
            {
                row.Update(session, title);
            }

            rows.Add(row);
        }

        for (var index = Sessions.Count - 1; index >= 0; index--)
        {
            if (!rows.Contains(Sessions[index]))
            {
                Sessions.RemoveAt(index);
            }
        }

        for (var index = 0; index < rows.Count; index++)
        {
            var current = Sessions.IndexOf(rows[index]);

            if (current == index)
            {
                continue;
            }

            if (current >= 0)
            {
                Sessions.Move(current, index);
            }
            else
            {
                Sessions.Insert(index, rows[index]);
            }
        }

        Status = snapshot.CorpseCount == 0
            ? $"{Sessions.Count} live"
            : $"{Sessions.Count} live · {snapshot.CorpseCount} stale entries";

        Refreshed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Raised once when a refresh has finished, rather than once for every row it touched. Anything
    /// expensive that depends on the whole list - the taskbar jump list - belongs here.
    /// </summary>
    public event EventHandler? Refreshed;

    /// <summary>Finds a live session by its identifier.</summary>
    public LiveSessionViewModel? Find(string? sessionId) =>
        string.IsNullOrWhiteSpace(sessionId)
            ? null
            : Sessions.FirstOrDefault(s => string.Equals(s.SessionId, sessionId, StringComparison.OrdinalIgnoreCase));

    private void Focus(object? parameter)
    {
        if (parameter is not LiveSessionViewModel session)
        {
            return;
        }

        var outcome = TerminalFocus.FocusSession(session.Pid, session.Name);

        Status = outcome == FocusOutcome.TabSelected
            ? $"Focused {session.Name}."
            : $"Raised the terminal — could not tell which tab {session.Name} is in.";
    }
}
