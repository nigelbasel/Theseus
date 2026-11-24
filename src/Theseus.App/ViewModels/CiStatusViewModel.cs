using System.Windows.Threading;
using Theseus.Core.CiStatus;
using Theseus.Core.Diagnostics;

namespace Theseus.App.ViewModels;

/// <summary>
/// Polls GitHub for the pull requests and CI runs of the repositories on the board, once per repository however many
/// cards work in it, within the budget GitHub allows without a token (pr-ci-status spec AC 2, 14 to 16), and words
/// each card's status from the latest readings. Only reads: nothing is written to GitHub, and no token is held.
/// </summary>
public sealed class CiStatusViewModel : ObservableObject, IDisposable
{
    private readonly IGitHubStatusClient _client;
    private readonly GitHubStatusBook _book = new();
    private readonly Func<DateTimeOffset> _now;
    private readonly DispatcherTimer? _timer;
    private IReadOnlyList<GitHubRepoRef> _repos = [];
    private bool _polling;
    private GitHubStatusLine _line = GitHubStatusLine.Hidden;

    /// <summary>Creates the poller; <paramref name="tick"/> starts a timer that checks once a minute for repositories due.</summary>
    public CiStatusViewModel(IGitHubStatusClient client, Func<DateTimeOffset>? now = null, bool tick = true)
    {
        _client = client;
        _now = now ?? (() => DateTimeOffset.UtcNow);

        if (tick)
        {
            _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMinutes(1) };
            _timer.Tick += async (_, _) =>
            {
                try
                {
                    await PollAsync(refresh: false);
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    TheseusLog.Warning("The pull request and CI poll failed: {Reason}", exception.Message);
                }
            };
            _timer.Start();
        }
    }

    /// <summary>Raised on the UI thread when a poll has read something, so the cards are worded again.</summary>
    public event EventHandler? Changed;

    /// <summary>What the status bar shows: when GitHub was last read, or until when its limit is used up.</summary>
    public string Text => _line.Text;

    /// <summary>The calls left this hour, when they reset, and the schedule.</summary>
    public string Tooltip => _line.Tooltip;

    /// <summary>Whether GitHub is refusing calls, shown in the caution colour.</summary>
    public bool IsCaution => _line.IsCaution;

    /// <summary>
    /// The GitHub repositories the board's cards work in, set on every refresh. The first time a repository appears it
    /// is due at once.
    /// </summary>
    public void SetRepositories(IReadOnlyList<GitHubRepoRef> repos)
    {
        _repos = [.. repos.DistinctBy(repo => repo.Key)];
        UpdateLine();
    }

    /// <summary>A card's status, from the latest readings.</summary>
    public CardStatus StatusFor(IReadOnlyList<CardStatusRepo> repos) =>
        CardStatuses.For(repos, _book.Reading, _book.LimitedUntil(_now()), _now(), TimeZoneInfo.Local);

    /// <summary>Reads every repository that is due; on Refresh, every one unless the calls left are down to the reserve.</summary>
    public async Task PollAsync(bool refresh)
    {
        if (_polling)
        {
            return;
        }

        var due = _book.Due(_repos, _now(), refresh);

        if (due.Count == 0)
        {
            UpdateLine();

            return;
        }

        _polling = true;

        try
        {
            foreach (var repo in due)
            {
                GitHubRead read;

                // Nothing may escape: this runs from a timer tick, where an exception would end the app (review should-fix 4).
                try
                {
                    read = await _client.ReadAsync(repo, CancellationToken.None);
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    TheseusLog.Information("Reading {Repo} from GitHub failed: {Reason}", repo.FullName, exception.Message);
                    read = new GitHubRead(RepoReading.Unknown(RepoReadingKind.Failed, _now()), null);
                }

                _book.Record(repo, read, _now());

                if (read.Reading.Kind == RepoReadingKind.Limited)
                {
                    break;
                }
            }
        }
        finally
        {
            _polling = false;
        }

        UpdateLine();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateLine()
    {
        _line = GitHubStatusLine.For(_book, _repos.Count, _now(), TimeZoneInfo.Local);
        Raise(nameof(Text));
        Raise(nameof(Tooltip));
        Raise(nameof(IsCaution));
    }

    /// <summary>Stops the timer, so no tick reads after the client is gone.</summary>
    public void Dispose() => _timer?.Stop();
}
