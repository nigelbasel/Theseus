using System.Collections.ObjectModel;
using Theseus.App.Converters;
using Theseus.Core.Sessions;

namespace Theseus.App.ViewModels;

/// <summary>One Claude Code session that is running right now, whoever started it.</summary>
public sealed class LiveSessionViewModel : ObservableObject
{
    private LiveSession _session;
    private bool _isAdopted;

    /// <summary>Creates a row for a live session.</summary>
    public LiveSessionViewModel(LiveSession session, string? consoleTitle = null)
    {
        _session = session;
        ConsoleTitle = consoleTitle ?? string.Empty;
    }

    /// <summary>The title the session has given its own console, which is what its tab shows.</summary>
    public string ConsoleTitle { get; private set; }

    /// <summary>
    /// The workstreams this session can be adopted into. Each target carries its own command, so
    /// choosing one is a single click on a named workstream rather than a hidden selection step.
    /// </summary>
    public ObservableCollection<AdoptTarget> AdoptTargets { get; } = [];

    /// <summary>Whether this session is already the one a workstream follows.</summary>
    public bool IsAdopted
    {
        get => _isAdopted;
        set => Set(ref _isAdopted, value);
    }

    /// <summary>
    /// Brings the row up to date with a fresh read of the same session. The row is kept rather than
    /// replaced, so a refresh changes the words on screen and not the list around them.
    /// </summary>
    public void Update(LiveSession session, string? consoleTitle)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.Pid != _session.Pid
            || !string.Equals(session.SessionId, _session.SessionId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("A row can only be updated with a fresh read of its own session.", nameof(session));
        }

        _session = session;
        ConsoleTitle = consoleTitle ?? string.Empty;

        Raise(nameof(ConsoleTitle));
        Raise(nameof(Name));
        Raise(nameof(RegistryName));
        Raise(nameof(Status));
        Raise(nameof(StatusSinceUtc));
        Raise(nameof(WorkingDirectory));
        Raise(nameof(StatusLine));
    }

    /// <summary>The Claude Code process, which is where the walk to its terminal window starts.</summary>
    public int Pid => _session.Pid;

    /// <summary>The session identifier, used to tie the session to a workstream.</summary>
    public string SessionId => _session.SessionId;

    /// <summary>What to call this session, preferring what its own tab says over a derived name.</summary>
    public string Name
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(ConsoleTitle))
            {
                return ConsoleTitle;
            }

            return string.IsNullOrWhiteSpace(_session.Name) ? _session.SessionId[..8] : _session.Name;
        }
    }

    /// <summary>The name the session registry holds, which a launched session gets from Theseus.</summary>
    public string RegistryName =>
        string.IsNullOrWhiteSpace(_session.Name) ? _session.SessionId[..8] : _session.Name;

    /// <summary>What the session reports it is doing, as the registry has it.</summary>
    public string? Status => _session.Status;

    /// <summary>When it last changed to that, where the session recorded it.</summary>
    public DateTimeOffset? StatusSinceUtc => _session.StatusSinceUtc;

    /// <summary>Where the session is working.</summary>
    public string WorkingDirectory => _session.Cwd ?? string.Empty;

    /// <summary>What the session reports it is doing, and since when it has been running.</summary>
    public string StatusLine
    {
        get
        {
            var status = string.IsNullOrWhiteSpace(_session.Status) ? "running" : _session.Status;
            var elapsed = RelativeTimeConverter.Format(_session.StartedUtc, DateTimeOffset.UtcNow);

            return string.IsNullOrEmpty(elapsed) ? status : $"{status} · started {elapsed} ago";
        }
    }
}
