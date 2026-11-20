using Theseus.Core.Diagnostics;

namespace Theseus.Core.Projects;

/// <summary>
/// Reads the projects off the caller's thread, one read at a time, when the projects folder in Settings changes or
/// <see cref="MarkStale"/> says a file in it did (<see cref="ProjectFolderWatcher"/>, 1.1b AC 3). Results come back
/// through <c>post</c>, on the caller's thread, and raise <see cref="Changed"/>. With no folder set nothing runs (AC 13).
/// </summary>
public sealed class BackgroundProjectReads
{
    private readonly Func<string, IReadOnlyList<ProjectFileReading>> _read;
    private readonly Action<Action> _post;
    private readonly RememberedProjects? _remembered;

    private string? _folder;
    private bool _hasRequested;
    private bool _stale;
    private bool _hasPending;
    private string? _pending;

    /// <summary>Creates the reader.</summary>
    /// <param name="read">Reads the projects folder; runs on a worker thread.</param>
    /// <param name="post">Runs an action on the caller's thread, such as the dispatcher's BeginInvoke.</param>
    /// <param name="remembered">Where the readable projects' names are kept between runs; null keeps none.</param>
    public BackgroundProjectReads(Func<string, IReadOnlyList<ProjectFileReading>> read, Action<Action> post,
        RememberedProjects? remembered = null)
    {
        _read = read;
        _post = post;
        _remembered = remembered;
    }

    /// <summary>The projects as last read.</summary>
    public IReadOnlyList<ProjectFileReading> Current { get; private set; } = [];

    /// <summary>Whether this run's first read has landed.</summary>
    public bool HasLanded { get; private set; }

    /// <summary>
    /// The projects that read: from the latest read once one has landed; until then, the ones remembered from the last
    /// run, so a start at launch is not held back and still opens in the project's window (1.1 PR 3 review, item 2).
    /// </summary>
    public IReadOnlySet<string> ReadableNames => HasLanded
        ? Current.Where(reading => reading.IsReadable).Select(reading => reading.Name).ToHashSet(StringComparer.OrdinalIgnoreCase)
        : _remembered?.Load() ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether a read is under way.</summary>
    public bool IsReading { get; private set; }

    /// <summary>Raised on the caller's thread when <see cref="Current"/> has been replaced.</summary>
    public event EventHandler? Changed;

    /// <summary>Says something in the projects folder changed, so the next <see cref="Request"/> reads it again.</summary>
    public void MarkStale() => _stale = true;

    /// <summary>Asks for the projects folder to be read, if it changed or is stale. Never waits for the read.</summary>
    public void Request(string? folder)
    {
        var key = string.IsNullOrWhiteSpace(folder) ? null : folder.Trim();

        if (IsReading)
        {
            // Asked during a read: decided again once it lands, since the files may have moved under it.
            _hasPending = true;
            _pending = key;

            return;
        }

        var due = !_hasRequested || _stale || !string.Equals(key, _folder, StringComparison.OrdinalIgnoreCase);

        if (!due)
        {
            return;
        }

        _hasRequested = true;
        _stale = false;
        _folder = key;

        if (key is null)
        {
            Landed([]);

            return;
        }

        IsReading = true;

        Task.Run(() => ReadSafely(key)).ContinueWith(task => _post(() =>
        {
            Landed(task.Result);
            IsReading = false;

            if (_hasPending)
            {
                _hasPending = false;
                Request(_pending);
            }
        }), TaskScheduler.Default);
    }

    private IReadOnlyList<ProjectFileReading> ReadSafely(string folder)
    {
        try
        {
            return _read(folder);
        }
        catch (Exception exception)
        {
            TheseusLog.Error(exception, "Reading the projects failed");

            return [ProjectFileReading.Unreadable(Path.GetFileName(folder.TrimEnd('\\', '/')), $"could not be read: {exception.Message}")
                with { Folder = folder }];
        }
    }

    // A read landed: what reads now is remembered for the next run's first seconds.
    private void Landed(IReadOnlyList<ProjectFileReading> readings)
    {
        HasLanded = true;
        _remembered?.Remember(readings.Where(reading => reading.IsReadable).Select(reading => reading.Name));
        Current = readings;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
