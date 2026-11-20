using Theseus.Core.Diagnostics;

namespace Theseus.Core.Projects;

/// <summary>
/// Reads the projects off the caller's thread, because each read is several git processes and the board refreshes on
/// the UI thread. One read at a time; the same folders are read again only after <c>interval</c>, other folders at once
/// (after any read under way). Results come back through <c>post</c>, on the caller's thread, and raise
/// <see cref="Changed"/>. With no folders listed nothing runs (AC 22).
/// A read lands in two steps: the catalogue first, which is what decides which cards leave the grid, then each slug's
/// artefacts, which take longer (PR 2 review, item 2). Before the first catalogue lands nothing is read, so no card is
/// held out of the grid (AC 13). A project carries over one failed read (<see cref="ReadingContinuity"/>).
/// </summary>
public sealed class BackgroundProjectReads
{
    private readonly Func<IReadOnlyList<string>, IReadOnlyList<ProjectFileReading>> _read;
    private readonly Action<Action> _post;
    private readonly TimeSpan _interval;
    private readonly Func<DateTimeOffset> _now;
    private readonly Func<IReadOnlyList<ProjectFileReading>, IReadOnlyList<ProjectFileReading>>? _attach;
    private readonly Dictionary<string, int> _failures = new(StringComparer.OrdinalIgnoreCase);
    private readonly RememberedProjects? _remembered;

    private string? _key;
    private DateTimeOffset _readAt;
    private IReadOnlyList<string>? _pending;

    /// <summary>Creates the reader.</summary>
    /// <param name="read">Reads the listed folders; runs on a worker thread.</param>
    /// <param name="post">Runs an action on the caller's thread, such as the dispatcher's BeginInvoke.</param>
    /// <param name="interval">How long a read of the same folders stands.</param>
    /// <param name="now">The clock.</param>
    /// <param name="attach">Reads the slugs' artefacts onto the readings, as a second step; null for none.</param>
    /// <param name="remembered">Where the readable projects' names are kept between runs; null keeps none.</param>
    public BackgroundProjectReads(Func<IReadOnlyList<string>, IReadOnlyList<ProjectFileReading>> read, Action<Action> post,
        TimeSpan interval, Func<DateTimeOffset>? now = null,
        Func<IReadOnlyList<ProjectFileReading>, IReadOnlyList<ProjectFileReading>>? attach = null,
        RememberedProjects? remembered = null)
    {
        _remembered = remembered;
        _attach = attach;
        _read = read;
        _post = post;
        _interval = interval;
        _now = now ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>The projects as last read.</summary>
    public IReadOnlyList<ProjectFileReading> Current { get; private set; } = [];

    /// <summary>Whether this run's first catalogue has landed.</summary>
    public bool HasLanded { get; private set; }

    /// <summary>
    /// The projects that read: from the latest catalogue once one has landed; until then, the ones remembered from the
    /// last run, so a start at launch is not held back and still opens in the project's window (PR 3 review, item 2).
    /// </summary>
    public IReadOnlySet<string> ReadableNames => HasLanded
        ? Current.Where(reading => reading.IsReadable).Select(reading => reading.Name).ToHashSet(StringComparer.OrdinalIgnoreCase)
        : _remembered?.Load() ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
            _failures.Clear();
            Landed([]);

            return;
        }

        IsReading = true;

        Task.Run(() => ReadSafely(folders)).ContinueWith(task =>
        {
            var catalogue = task.Result;

            if (_attach is null)
            {
                _post(() =>
                {
                    Landed(ReadingContinuity.Merge(Current, catalogue, _failures));
                    Finished();
                });

                return;
            }

            _post(() => Landed(ReadingContinuity.Merge(Current, catalogue, _failures)));

            var attached = AttachSafely(catalogue);

            _post(() =>
            {
                Replace(ReadingContinuity.WithArtefacts(Current, attached));
                Finished();
            });
        }, TaskScheduler.Default);
    }

    private void Finished()
    {
        IsReading = false;

        if (_pending is { } next)
        {
            _pending = null;
            Request(next);
        }
    }

    private IReadOnlyList<ProjectFileReading> AttachSafely(IReadOnlyList<ProjectFileReading> catalogue)
    {
        try
        {
            return _attach!(catalogue);
        }
        catch (Exception exception)
        {
            TheseusLog.Error(exception, "Reading the projects' artefacts failed");

            return catalogue;
        }
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

    // A catalogue landed: what reads now is remembered for the next run's first seconds.
    private void Landed(IReadOnlyList<ProjectFileReading> readings)
    {
        HasLanded = true;
        _remembered?.Remember(readings.Where(reading => reading.IsReadable).Select(reading => reading.Name));
        Replace(readings);
    }

    private void Replace(IReadOnlyList<ProjectFileReading> readings)
    {
        Current = readings;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
