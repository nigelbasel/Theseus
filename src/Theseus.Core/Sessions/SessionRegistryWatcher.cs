namespace Theseus.Core.Sessions;

/// <summary>
/// Watches Claude Code's session registry and reports a fresh snapshot when it settles.
///
/// Writes are debounced rather than acted on individually: a session's registry file is written
/// more than once as it starts, so the first write carries a name derived from the working directory
/// and the real one lands moments later. Reading on every raw event shows the wrong name and then
/// corrects itself, which reads as a flicker.
/// </summary>
public sealed class SessionRegistryWatcher : IDisposable
{
    private readonly ISessionRegistryReader _reader;
    private readonly FileSystemWatcher? _watcher;
    private readonly System.Timers.Timer _settle;

    /// <summary>Creates a watcher over the registry directory.</summary>
    /// <param name="directory">The registry directory to watch.</param>
    /// <param name="reader">How the snapshot is read once the directory settles.</param>
    /// <param name="debounce">How long to wait for further writes before reading.</param>
    public SessionRegistryWatcher(string directory, ISessionRegistryReader reader, TimeSpan? debounce = null)
    {
        _reader = reader;

        _settle = new System.Timers.Timer((debounce ?? TimeSpan.FromMilliseconds(400)).TotalMilliseconds)
        {
            AutoReset = false,
        };

        _settle.Elapsed += (_, _) => Changed?.Invoke(this, _reader.Read());

        if (!Directory.Exists(directory))
        {
            return;
        }

        _watcher = new FileSystemWatcher(directory, "*.json")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            IncludeSubdirectories = false,
        };

        _watcher.Created += OnChanged;
        _watcher.Changed += OnChanged;
        _watcher.Deleted += OnChanged;
        _watcher.Renamed += OnChanged;
        _watcher.EnableRaisingEvents = true;
    }

    /// <summary>Raised with a fresh snapshot once the registry stops changing.</summary>
    public event EventHandler<SessionRegistrySnapshot>? Changed;

    /// <summary>Whether the registry directory existed and is being watched.</summary>
    public bool IsWatching => _watcher is not null;

    /// <summary>Reads a snapshot now, without waiting for a change.</summary>
    public SessionRegistrySnapshot ReadNow() => _reader.Read();

    /// <inheritdoc />
    public void Dispose()
    {
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
        }

        _settle.Dispose();
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        _settle.Stop();
        _settle.Start();
    }
}
