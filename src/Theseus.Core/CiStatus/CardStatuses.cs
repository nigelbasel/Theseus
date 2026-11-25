using System.Globalization;

namespace Theseus.Core.CiStatus;

/// <summary>
/// Words a card's pull request and CI status from what GitHub last said (pr-ci-status spec, objective 1.7). What
/// Theseus does not know it says it does not know, and nothing but GitHub's own answer is ever shown as passing. The
/// button never grows with the number of repositories (rubric R18): the list is in the drop-down.
/// </summary>
public static class CardStatuses
{
    /// <summary>The status of a card working in these repositories.</summary>
    /// <param name="repos">The card's repositories, as its branch line finds them.</param>
    /// <param name="readingOf">The latest reading of a GitHub repository, or null before one.</param>
    /// <param name="limitedUntil">Until when GitHub refuses calls without a token, or null.</param>
    /// <param name="now">The time, for saying whether a time is today.</param>
    /// <param name="zone">The zone times are shown in.</param>
    public static CardStatus For(IReadOnlyList<CardStatusRepo> repos, Func<GitHubRepoRef, RepoReading?> readingOf,
        DateTimeOffset? limitedUntil, DateTimeOffset now, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(repos);
        ArgumentNullException.ThrowIfNull(readingOf);
        ArgumentNullException.ThrowIfNull(zone);

        var shown = repos.Where(repo => !string.IsNullOrWhiteSpace(repo.Origin)).ToList();

        if (shown.Count == 0)
        {
            return CardStatus.None;
        }

        var several = shown.Count > 1;
        var parts = shown.Select(repo => Describe(repo, readingOf, limitedUntil, now, zone)).ToList();
        var rows = parts.SelectMany(part => part.Rows.Select(row => several ? row with { Text = $"{part.Name}: {row.Text}" } : row))
            .ToList();
        var worst = parts.MinBy(part => part.State)!;
        var prefix = several
            ? $"{shown.Count} repos · "
            : parts[0].PullRequest is { } pull ? $"PR {pull.Number}{(pull.IsDraft ? " draft" : string.Empty)} · " : string.Empty;

        return new CardStatus(prefix + worst.Word, worst.State, rows);
    }

    private static Part Describe(CardStatusRepo repo, Func<GitHubRepoRef, RepoReading?> readingOf, DateTimeOffset? limitedUntil,
        DateTimeOffset now, TimeZoneInfo zone)
    {
        if (GitHubRepoRef.FromRemote(repo.Origin) is not { } github)
        {
            var host = RemoteHost.Of(repo.Origin) ?? "Its remote";

            return Unknown(repo, CiState.NotRead, "CI not read",
                $"{host} is not read: Theseus reads only GitHub",
                $"{repo.Name} is on {host}. Theseus reads pull requests and CI from GitHub only, so its status is not known here.");
        }

        if (string.IsNullOrWhiteSpace(repo.Branch) || repo.Branch.StartsWith("detached at ", StringComparison.Ordinal))
        {
            return Unknown(repo, CiState.Unknown, "CI unknown", "Branch not readable, so its pull request and CI are unknown",
                $"{repo.Name}'s branch could not be read, or it is detached, so no pull request or run can be matched to it.");
        }

        var reading = readingOf(github);

        // While GitHub refuses calls, no reading can be renewed, so an earlier one is not shown as current (review must-fix 1).
        if (limitedUntil is not null || reading?.Kind == RepoReadingKind.Limited)
        {
            var resets = limitedUntil ?? reading?.LimitResetsAt;
            var when = resets is { } at ? $" until {Time(at, now, zone)}" : string.Empty;

            return Unknown(repo, CiState.Unknown, "CI unknown", $"GitHub's limit for calls without a token is used up{when}",
                $"GitHub allows 60 calls an hour without a token, and they are used up{when}. Theseus reads again then; until it does, {github.FullName}'s status is unknown.");
        }

        if (reading is null)
        {
            return Unknown(repo, CiState.Unknown, "CI unknown", "Not read from GitHub yet",
                $"{github.FullName} has not been read from GitHub yet. Its status is unknown until it is.");
        }

        if (reading.Kind == RepoReadingKind.NotShown)
        {
            return Unknown(repo, CiState.Unknown, "CI unknown", $"GitHub does not show {github.FullName} without a token",
                $"GitHub answered not found at {Time(reading.At, now, zone)}: the repository is private or missing, and without a token the two look the same.");
        }

        if (reading.Kind == RepoReadingKind.Failed)
        {
            return Unknown(repo, CiState.Unknown, "CI unknown", $"GitHub could not be read at {Time(reading.At, now, zone)}",
                $"The last read of {github.FullName} failed, so its status is unknown. Theseus tries again on its next poll.");
        }

        var branch = repo.Branch;
        var readAt = $"Read from GitHub at {Time(reading.At, now, zone)}.";

        // A pull request whose head repository is gone or another one (a fork) is not this card's branch (review nit).
        var pull = reading.PullRequests
            .Where(candidate => string.Equals(candidate.HeadBranch, branch, StringComparison.Ordinal)
                                && string.Equals(candidate.HeadRepository, github.FullName, StringComparison.OrdinalIgnoreCase))
            .MaxBy(candidate => candidate.Number);
        var pullRow = pull is null
            ? new CardStatusRow($"No open pull request from {branch}", null, null, $"{readAt} GitHub lists no open pull request from this branch.")
            : new CardStatusRow($"PR {pull.Number} open, {(pull.IsDraft ? "draft" : "ready")}", pull.Title, pull.Url,
                $"{pull.Title}\n{readAt} Opens it on GitHub.");

        var run = reading.Runs.Where(candidate => string.Equals(candidate.Branch, branch, StringComparison.Ordinal))
            .MaxBy(candidate => candidate.CreatedAt);

        if (run is null)
        {
            return new Part(repo.Name, CiState.NoneFound, "No CI run", pull,
            [
                pullRow,
                new CardStatusRow($"No CI run on {branch} among the newest {GitHubStatusClient.RunsPerRead}", null, null,
                    $"{readAt} None of the repository's newest {GitHubStatusClient.RunsPerRead} runs is on this branch, so its CI is not known."),
            ]);
        }

        var (state, word) = Classify(run);
        var time = state == CiState.Running
            ? $"since {Time(run.StartedAt ?? run.CreatedAt, now, zone)}"
            : Time(run.UpdatedAt, now, zone);
        var button = state switch
        {
            CiState.Passed => "✓ CI passed",
            CiState.Failed => "✗ CI failed",
            _ => $"CI {word}",
        };

        return new Part(repo.Name, state, button, pull,
        [
            pullRow,
            new CardStatusRow($"CI {word} {time}", null, run.Url, $"{readAt} Opens the run on GitHub."),
        ]);
    }

    private static (CiState State, string Word) Classify(CiRun run)
    {
        if (!string.Equals(run.Status, "completed", StringComparison.Ordinal))
        {
            return (CiState.Running, string.Equals(run.Status, "in_progress", StringComparison.Ordinal) ? "running" : run.Status.Replace('_', ' '));
        }

        return run.Conclusion switch
        {
            "success" => (CiState.Passed, "passed"),
            "failure" or "startup_failure" => (CiState.Failed, "failed"),
            "timed_out" => (CiState.Failed, "timed out"),
            null => (CiState.Unknown, "ended without a conclusion"),
            var other => (CiState.Ended, other.Replace('_', ' ')),
        };
    }

    private static Part Unknown(CardStatusRepo repo, CiState state, string button, string text, string tooltip) =>
        new(repo.Name, state, button, null, [new CardStatusRow(text, null, null, tooltip)]);

    /// <summary>A time as the card shows it: "21:43" today, "23 Nov 21:43" otherwise, in the given zone.</summary>
    public static string Time(DateTimeOffset at, DateTimeOffset now, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        var local = TimeZoneInfo.ConvertTime(at, zone);
        var today = TimeZoneInfo.ConvertTime(now, zone);

        return local.Date == today.Date
            ? local.ToString("HH:mm", CultureInfo.InvariantCulture)
            : local.ToString("d MMM HH:mm", CultureInfo.InvariantCulture);
    }

    private sealed record Part(string Name, CiState State, string Word, OpenPullRequest? PullRequest, IReadOnlyList<CardStatusRow> Rows);
}
