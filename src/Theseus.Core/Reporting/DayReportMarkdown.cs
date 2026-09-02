using System.Globalization;
using System.Text;

namespace Theseus.Core.Reporting;

/// <summary>
/// The day's report as markdown, for a file that is kept.
///
/// Written to be pasted as much as read: the issue keys sit beside the time so a worklog entry can be
/// made from one line, and the totals are per area because that is how the board is arranged and how
/// the work is thought about.
/// </summary>
public static class DayReportMarkdown
{
    /// <summary>The whole report.</summary>
    public static string For(DateOnly day, IReadOnlyList<DayReportEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var page = new StringBuilder();

        page.Append("# ").AppendLine(day.ToString("dddd d MMMM yyyy", CultureInfo.InvariantCulture));
        page.AppendLine();

        if (entries.Count == 0)
        {
            page.AppendLine("Nothing ran on this day.");

            return page.ToString();
        }

        page.Append("**").Append(Length(DayReport.Total(entries))).Append("** across ")
            .Append(entries.Count).Append(entries.Count == 1 ? " workstream" : " workstreams")
            .AppendLine(".");
        page.AppendLine();
        page.AppendLine("This is how long sessions were open, which is not the same as attention: a session left");
        page.AppendLine("running over lunch counts every minute of it. Read it as an upper bound.");

        if (entries.Any(entry => entry.StillRunning))
        {
            page.AppendLine();
            page.AppendLine("Some sessions were still running when this was written, so their figures are not final.");
        }

        foreach (var area in entries.GroupBy(entry => entry.Area, StringComparer.OrdinalIgnoreCase))
        {
            page.AppendLine();
            page.Append("## ").Append(string.IsNullOrWhiteSpace(area.Key) ? "Unfiled" : area.Key)
                .Append(" — ").AppendLine(Length(DayReport.Total([.. area])));
            page.AppendLine();

            foreach (var entry in area)
            {
                page.Append("- **").Append(entry.Title).Append("** — ");

                if (entry.Sessions == 0)
                {
                    page.Append("length unknown");
                }
                else
                {
                    page.Append(Length(entry.Worked)).Append(" over ").Append(entry.Sessions)
                        .Append(entry.Sessions == 1 ? " session" : " sessions");
                }

                if (entry.StillRunning)
                {
                    page.Append(", still running");
                }

                if (entry.HandedOverToday)
                {
                    page.Append(", handed over");
                }

                if (entry.UnmeasuredSessions > 0)
                {
                    page.Append(", plus ").Append(entry.UnmeasuredSessions)
                        .Append(entry.UnmeasuredSessions == 1 ? " session" : " sessions")
                        .Append(" Theseus never saw end");
                }

                page.AppendLine();

                if (entry.JiraKeys.Count > 0)
                {
                    page.Append("  - ").AppendLine(string.Join(", ", entry.JiraKeys));
                }
            }
        }

        var closeable = DayReport.SafeToClose(entries);

        if (closeable.Count > 0)
        {
            page.AppendLine();
            page.AppendLine("## Safe to close");
            page.AppendLine();

            foreach (var entry in closeable.OrderBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase))
            {
                page.Append("- ").AppendLine(entry.Title);
            }
        }

        return page.ToString();
    }

    /// <summary>
    /// A duration as somebody would say it. Rounded to the minute, because the run log records a
    /// launch and an exit rather than attention, and seconds would imply a precision it has not got.
    /// </summary>
    public static string Length(TimeSpan worked)
    {
        var minutes = (long)Math.Round(worked.TotalMinutes, MidpointRounding.AwayFromZero);

        return minutes switch
        {
            <= 0 => "under a minute",
            < 60 => $"{minutes}m",
            _ when minutes % 60 == 0 => $"{minutes / 60}h",
            _ => $"{minutes / 60}h {minutes % 60:00}m",
        };
    }
}
