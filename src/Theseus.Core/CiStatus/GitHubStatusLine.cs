using System.Globalization;

namespace Theseus.Core.CiStatus;

/// <summary>
/// The status bar's reading of GitHub: when it was last read, or that its limit is used up, with the calls left in the
/// tooltip (pr-ci-status spec AC 16). Empty while no GitHub repository is polled.
/// </summary>
/// <param name="Text">What the status bar shows.</param>
/// <param name="Tooltip">What hovering it says.</param>
/// <param name="IsCaution">Whether it is shown in the caution colour: GitHub is refusing calls.</param>
public sealed record GitHubStatusLine(string Text, string Tooltip, bool IsCaution)
{
    /// <summary>Nothing to show.</summary>
    public static readonly GitHubStatusLine Hidden = new(string.Empty, string.Empty, false);

    /// <summary>The line for the book as it stands, polling this many repositories.</summary>
    public static GitHubStatusLine For(GitHubStatusBook book, int repositories, DateTimeOffset now, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(zone);

        if (repositories == 0)
        {
            return Hidden;
        }

        var limited = book.LimitedUntil(now);
        var text = limited is { } until
            ? $"⚠ GitHub limit until {CardStatuses.Time(until, now, zone)}"
            : book.LastReadAt is { } read ? $"GitHub read {CardStatuses.Time(read, now, zone)}" : "GitHub not read yet";

        var budget = book.Budget is { } known && known.ResetsAt > now
            ? $"{known.Remaining} of {known.Limit} calls left this hour; they reset at {CardStatuses.Time(known.ResetsAt, now, zone)}."
            : "Calls left: not known until GitHub next answers.";
        var minutes = GitHubStatusBook.Interval(repositories).TotalMinutes.ToString("0.#", CultureInfo.InvariantCulture);
        var polled = repositories == 1 ? "1 repository" : $"{repositories} repositories";

        var tooltip = string.Join("\n",
            "Pull request and CI status, read from GitHub without a token. GitHub allows 60 calls an hour without one, shared with everything on this network.",
            budget,
            $"{polled} polled every {minutes} minutes, two calls each.",
            $"Refresh reads at once while more than {GitHubStatusBook.Reserve} calls are left.");

        return new GitHubStatusLine(text, tooltip, limited is not null);
    }
}
