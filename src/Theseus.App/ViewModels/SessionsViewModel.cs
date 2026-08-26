using System.Collections.ObjectModel;
using Theseus.App.Services;
using Theseus.Core.Sessions;

namespace Theseus.App.ViewModels;

/// <summary>
/// Every Claude Code session running on this machine, whether or not Theseus started it. The
/// registry is the contract, so hand-started sessions appear here on equal terms with launched ones.
/// </summary>
public sealed class SessionsViewModel : ObservableObject
{
    private readonly ISessionRegistryReader _registry;
    private readonly IConsoleTitleReader _titles;
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

        Snapshot = snapshot;
        Sessions.Clear();

        foreach (var session in snapshot.Live
                     .Where(session => session.IsAdoptable)
                     .OrderByDescending(session => session.StartedUtc))
        {
            Sessions.Add(new LiveSessionViewModel(session, _titles.Read(session.Pid)));
        }

        Status = snapshot.CorpseCount == 0
            ? $"{Sessions.Count} live"
            : $"{Sessions.Count} live · {snapshot.CorpseCount} stale entries";
    }

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
