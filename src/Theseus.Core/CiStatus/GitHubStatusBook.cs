namespace Theseus.Core.CiStatus;

/// <summary>
/// The latest reading of each GitHub repository, and when each may be read again. GitHub allows 60 calls an hour
/// without a token, a 304 included, shared with the update check and anything else on the machine (measured
/// 2026-11-19): polling keeps to 48 of them, two calls per repository, and leaves the rest (pr-ci-status spec AC 14,
/// 15, D6).
/// </summary>
public sealed class GitHubStatusBook
{
    /// <summary>The shortest interval between reads of one repository.</summary>
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromMinutes(5);

    /// <summary>The calls an hour polling may use.</summary>
    public const int PollingCallsPerHour = 48;

    /// <summary>The calls each read of a repository makes.</summary>
    public const int CallsPerRead = 2;

    /// <summary>Calls kept for the update check and anything else: Refresh does not read below this.</summary>
    public const int Reserve = 12;

    private readonly Dictionary<string, RepoReading> _readings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _attempted = new(StringComparer.Ordinal);

    /// <summary>The budget GitHub last reported, or null before any answer.</summary>
    public GitHubBudget? Budget { get; private set; }

    /// <summary>When a repository was last read successfully, or null before.</summary>
    public DateTimeOffset? LastReadAt { get; private set; }

    /// <summary>How many repositories the last poll was asked to cover.</summary>
    public int Polled { get; private set; }

    /// <summary>How often each repository is read when <paramref name="repositories"/> are polled.</summary>
    public static TimeSpan Interval(int repositories)
    {
        var spread = TimeSpan.FromMinutes(60.0 * CallsPerRead * Math.Max(repositories, 1) / PollingCallsPerHour);

        return spread > MinimumInterval ? spread : MinimumInterval;
    }

    /// <summary>The latest reading of a repository, or null before it has been read.</summary>
    public RepoReading? Reading(GitHubRepoRef repo) =>
        _readings.GetValueOrDefault((repo ?? throw new ArgumentNullException(nameof(repo))).Key);

    /// <summary>Until when GitHub refuses calls without a token, or null when it does not now.</summary>
    public DateTimeOffset? LimitedUntil(DateTimeOffset now)
    {
        var fromReadings = _readings.Values
            .Where(reading => reading.Kind == RepoReadingKind.Limited && reading.LimitResetsAt > now)
            .Select(reading => reading.LimitResetsAt)
            .Max();

        if (fromReadings is { } until)
        {
            return until;
        }

        return Budget is { Remaining: < CallsPerRead } budget && budget.ResetsAt > now ? budget.ResetsAt : null;
    }

    /// <summary>
    /// The repositories to read now: each whose interval has passed, or every one on Refresh unless the calls left are
    /// down to the reserve. None while GitHub is refusing.
    /// </summary>
    public IReadOnlyList<GitHubRepoRef> Due(IReadOnlyList<GitHubRepoRef> repos, DateTimeOffset now, bool refresh)
    {
        ArgumentNullException.ThrowIfNull(repos);

        var distinct = repos.DistinctBy(repo => repo.Key).ToList();
        Polled = distinct.Count;

        if (LimitedUntil(now) is not null)
        {
            return [];
        }

        if (refresh)
        {
            var known = Budget is { } budget && budget.ResetsAt > now;

            return known && Budget!.Remaining <= Reserve ? [] : distinct;
        }

        var interval = Interval(distinct.Count);

        return [.. distinct.Where(repo => !_attempted.TryGetValue(repo.Key, out var at) || now - at >= interval)];
    }

    /// <summary>Records a read of a repository.</summary>
    public void Record(GitHubRepoRef repo, GitHubRead read, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(read);

        _readings[repo.Key] = read.Reading;
        _attempted[repo.Key] = now;

        if (read.Budget is { } budget)
        {
            Budget = budget;
        }

        if (read.Reading.Kind == RepoReadingKind.Read)
        {
            LastReadAt = read.Reading.At;
        }
    }
}
