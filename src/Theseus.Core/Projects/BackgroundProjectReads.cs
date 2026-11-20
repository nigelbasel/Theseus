using Theseus.Core.Diagnostics;

namespace Theseus.Core.Projects;

/// <summary>
/// Reads the projects off the caller's thread, because each read is several git processes and the board refreshes on
/// the UI thread. One read at a time; the same folders are read again only after <c>interval</c>, other folders at once
/// (after any read under way). Results come back through <c>post</c>, on the caller's thread, and raise
/// <see cref="Changed"/>. With no folders listed nothing runs (AC 22).
/// </summary>
public sealed class BackgroundProjectReads
{
    private readonly Func<IReadOnlyList<string>, IReadOnlyList<ProjectFileReading>> _read;
    private readonly Action<Action> _post;
    private readonly TimeSpan _interval;
    private readonly Func<DateTimeOffset> _now;

    private string? _key;
    private DateTimeOffset _readAt;
    private IReadOnlyList<string>? _pending;

    /// <summary>Creates the reader.</summary>
    /// <param name="read">Reads the listed folders; runs on a worker thread.</param>
    /// <param name="post">Runs an action on the caller's thread, such as the dispatcher's BeginInvoke.</param>
    /// <param name="interval">How long a read of the same folders stands.</param>
    /// <param name="now">The clock.</param>
    public BackgroundProjectReads(Func<IReadOnlyList<string>, IReadOnlyList<ProjectFileReading>> read, Action<Action> post,
        TimeSpan interval, Func<DateTimeOffset>? now = null)
    {
        _read = read;
        _post = post;
        _interval = interval;
        _now = now ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>The projects as last read.</summary>
    public IReadOnlyList<ProjectFileReading> Current { get; private set; } = [];

    /// <summary>Whether a read is under way.</summary>
    public bool IsReading { get; private set; }

    /// <summary>Raised on the caller's thread when <see cref="Current"/> has been replaced.</summary>
    public event EventHandler? Changed;

    /// <summary>Asks for the folders to be read, if they are due. Never waits for git.</summary>
    public void Request(IReadOnlyList<string> folders)
    {
        ArgumentNullException.ThrowIfNull(folders);

        var key = string.Join("\n", folders);

        if (IsReading)
        {
            // Back to the folders being read: what was asked for in between is no longer wanted.
            _pending = string.Equals(key, _key, StringComparison.OrdinalIgnoreCase) ? null : folders;

            return;
        }

        var due = !string.Equals(key, _key, StringComparison.OrdinalIgnoreCase) || _now() - _readAt > _interval;

        if (!due)
        {
            return;
        }

        _key = key;
        _readAt = _now();

        if (folders.Count == 0)
        {
            Replace([]);

            return;
        }

        IsReading = true;

        Task.Run(() => ReadSafely(folders)).ContinueWith(task => _post(() =>
        {
            IsReading = false;
            Replace(task.Result);

            if (_pending is { } next)
            {
                _pending = null;
                Request(next);
            }
        }), TaskScheduler.Default);
    }

    private IReadOnlyList<ProjectFileReading> ReadSafely(IReadOnlyList<string> folders)
    {
        try
        {
            return _read(folders);
        }
        catch (Exception exception)
        {
            TheseusLog.Error(exception, "Reading the projects failed");

            return [.. folders.Select(folder =>
                ProjectFileReading.Unreadable(Path.GetFileName(folder.TrimEnd('\\', '/')), $"could not be read: {exception.Message}")
                    with { Folder = folder })];
        }
    }

    private void Replace(IReadOnlyList<ProjectFileReading> readings)
    {
        Current = readings;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
