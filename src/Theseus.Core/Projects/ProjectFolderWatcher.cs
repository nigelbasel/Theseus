using Theseus.Core.Diagnostics;

namespace Theseus.Core.Projects;

/// <summary>
/// Watches the projects folder, subfolders included, and calls back once a burst of changes has gone quiet, or at the
/// latest <c>longest</c> after its first change, so a session writing several records costs one read and a file written
/// steadily cannot hold reads off (1.1b AC 3). A lost-events error is reported as a change; any other error (the folder
/// deleted under it) ends the watcher, so the next <see cref="Watch"/> starts again. The callback runs on a worker thread.
/// </summary>
public sealed class ProjectFolderWatcher : IDisposable
{
    private readonly TimeSpan _quiet;
    private readonly TimeSpan _longest;
    private readonly Func<DateTimeOffset> _now;
    private readonly Timer _timer;
    private readonly object _gate = new();

    private FileSystemWatcher? _watcher;
    private string? _folder;
    private string? _unwatchable;
    private DateTimeOffset? _firstChange;

    /// <summary>Creates a watcher that watches nothing until <see cref="Watch"/>.</summary>
    /// <param name="changed">Called when changes have gone quiet.</param>
    /// <param name="quiet">How long after the last change it is called.</param>
    /// <param name="longest">How long after the first change of a burst it is called at the latest.</param>
    /// <param name="now">The clock.</param>
    public ProjectFolderWatcher(Action changed, TimeSpan quiet, TimeSpan longest, Func<DateTimeOffset>? now = null)
    {
        ArgumentNullException.ThrowIfNull(changed);

        _quiet = quiet;
        _longest = longest;
        _now = now ?? (() => DateTimeOffset.UtcNow);
        _timer = new Timer(_ =>
        {
            lock (_gate)
            {
                _firstChange = null;
            }

            changed();
        }, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    /// Watches a folder, replacing any folder watched before; null or empty watches nothing. Says whether it started
    /// watching just now (the caller then reads the folder, which it has not seen while unwatched), is still watching,
    /// or cannot watch: the folder is not there, or is no longer there under the watcher. Asking again later tries again.
    /// </summary>
    public ProjectFolderWatch Watch(string? folder)
    {
        var key = ProjectsFolderPath.Normalize(folder) is { Length: > 0 } normal ? normal : null;

        lock (_gate)
        {
            if (_watcher is not null && string.Equals(key, _folder, StringComparison.OrdinalIgnoreCase))
            {
                // A folder moved away raises no event inside it, since the watcher follows its handle.
                if (Directory.Exists(key))
                {
                    return ProjectFolderWatch.Watching;
                }

                Stop();

                return ProjectFolderWatch.NotWatching;
            }

            Stop();
            _folder = key;

            if (key is null || !Directory.Exists(key))
            {
                return ProjectFolderWatch.NotWatching;
            }

            FileSystemWatcher? watcher = null;

            try
            {
                watcher = new FileSystemWatcher(key)
                {
                    IncludeSubdirectories = true,
                    InternalBufferSize = 64 * 1024,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
                };

                watcher.Changed += (_, _) => Poke();
                watcher.Created += (_, _) => Poke();
                watcher.Deleted += (_, _) => Poke();
                watcher.Renamed += (_, _) => Poke();
                var started = watcher;
                watcher.Error += (_, args) => Failed(started, key, args.GetException());
                watcher.EnableRaisingEvents = true;
                _watcher = watcher;
                _unwatchable = null;

                return ProjectFolderWatch.Started;
            }
            catch (Exception exception) when (exception is IOException or ArgumentException or UnauthorizedAccessException)
            {
                watcher?.Dispose();

                // Asked again on every board refresh, so said once per folder, not every time.
                if (!string.Equals(_unwatchable, key, StringComparison.OrdinalIgnoreCase))
                {
                    _unwatchable = key;
                    TheseusLog.Warning(exception, "Could not watch the projects folder {Folder}; it is read on every refresh instead", key);
                }

                return ProjectFolderWatch.NotWatching;
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            Stop();
        }

        _timer.Dispose();
    }

    private void Failed(FileSystemWatcher watcher, string folder, Exception exception)
    {
        if (exception is not InternalBufferOverflowException)
        {
            TheseusLog.Warning(exception, "Watching the projects folder {Folder} stopped; it is watched again on the next refresh", folder);

            lock (_gate)
            {
                if (ReferenceEquals(_watcher, watcher))
                {
                    Stop();
                }
            }
        }
        else
        {
            TheseusLog.Warning(exception, "Watching the projects folder {Folder} may have missed changes; reading it again", folder);
        }

        Poke();
    }

    private void Poke()
    {
        lock (_gate)
        {
            var now = _now();
            _firstChange ??= now;

            var latest = _firstChange.Value + _longest - now;
            var wait = latest < _quiet ? latest : _quiet;

            _timer.Change(wait < TimeSpan.Zero ? TimeSpan.Zero : wait, Timeout.InfiniteTimeSpan);
        }
    }

    private void Stop()
    {
        _watcher?.Dispose();
        _watcher = null;
    }
}
