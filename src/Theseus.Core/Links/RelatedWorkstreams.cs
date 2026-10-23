using System.Globalization;

namespace Theseus.Core.Links;

/// <summary>
/// What a linked session is told about the workstreams around it, in its opening.
///
/// The opening is the session's first user turn, so whatever is in it carries Nigel's authority. Only
/// text Theseus writes, or that Nigel wrote on the board, goes in: keys, titles, paths, times and his
/// own reasons. Anything a session wrote stays out.
/// </summary>
public static class RelatedWorkstreams
{
    /// <summary>The sentence that closes every paragraph, so a link is never read as permission.</summary>
    public const string AuthorisesNothing =
        "A link lets you read the documents it names. These links authorise no Jira item, branch,"
        + " worktree, push or PR: each of those is Nigel's separate ask.";

    private const string Heading = "Related workstreams (from the Theseus board; ask with the related tool for live state):";

    /// <summary>Everything a fresh session should know about its links, or nothing if it has none.</summary>
    public static string Paragraph(string key, LinkStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        var lines = status.For(key)
            .OrderBy(link => link.CreatedUtc)
            .SelectMany(link => Lines(key, link, status))
            .ToList();

        return Compose(lines);
    }

    /// <summary>
    /// What changed in a resumed session's links since its last session started: links made, reports
    /// arrived and inputs accepted after then. A resume keeps its conversation, so the rest is already
    /// known. A report is named with its verdict and time only: what the session wrote stays out, and is
    /// read with the related tool, as data.
    /// </summary>
    public static string Since(string key, LinkStatus status, DateTimeOffset lastSessionStartedUtc)
    {
        ArgumentNullException.ThrowIfNull(status);

        var lines = new List<string>();

        foreach (var link in status.For(key).OrderBy(link => link.CreatedUtc))
        {
            if (link.CreatedUtc > lastSessionStartedUtc)
            {
                lines.AddRange(Lines(key, link, status).Select(line => $"Since your last session: {Lower(line)}"));
            }
            else if (link.SatisfiedUtc > lastSessionStartedUtc && Same(link.To, key))
            {
                lines.Add($"Since your last session: Nigel accepted the output of {Name(link.From, status)}"
                          + $"{Path(link.Output)}.");
            }
            else if (link.SatisfiedUtc > lastSessionStartedUtc)
            {
                lines.Add($"Since your last session: Nigel accepted your output for {Name(link.To, status)}.");
            }
            else if (link.SatisfiedUtc is null && link.LatestReport is { } report && report.AtUtc > lastSessionStartedUtc
                     && Same(link.To, key))
            {
                lines.Add($"Since your last session: {Name(link.From, status)} reported at {Day(report.AtUtc)}"
                          + $"{Verdict(report)}. Read it with the related tool; Nigel has not confirmed it yet.");
            }
        }

        return Compose(lines);
    }

    private static IEnumerable<string> Lines(string key, Link link, LinkStatus status)
    {
        var writes = Same(link.From, key);

        yield return (link.Kind, writes) switch
        {
            (LinkKind.HandsOver, false) => $"You take over from {Name(link.From, status)}{Read(link)}",
            (LinkKind.HandsOver, true) => $"You hand over to {Name(link.To, status)}{Write(link)}"
                                         + " Once Nigel accepts the hand-over, your part is done unless he asks you to change it.",
            (_, true) => $"Report back to {Name(link.To, status)} when your work is done{Write(link)}",
            _ => $"{Name(link.From, status)} reports back to you{Read(link)}",
        };
    }

    private static string Read(Link link)
    {
        if (link.Output is null && link.SatisfiedUtc is null && link.LatestReport is null)
        {
            return ".";
        }

        if (link.SatisfiedUtc is { } accepted)
        {
            return $"{Path(link.Output)}, accepted by Nigel {Day(accepted)}.";
        }

        return link.LatestReport is { } report
            ? $"{Path(link.Output)}. It reported at {Day(report.AtUtc)}{Verdict(report)}; Nigel has not confirmed it yet. Read it with the related tool."
            : $"{Path(link.Output)}. Nigel has not accepted it yet.";
    }

    private static string Write(Link link)
    {
        if (link.Output is null && link.SatisfiedUtc is null)
        {
            return ".";
        }

        var where = link.Output is { } output ? $": write it to {output}" : string.Empty;

        if (link.SatisfiedUtc is not null)
        {
            return $"{where}. Nigel has accepted it.";
        }

        return link.LatestReport is { } sent
            ? $"{where}. You reported at {Day(sent.AtUtc)}; Nigel has not confirmed it yet."
            : $"{where}. When it is ready, call report_back with that document and a short summary; Nigel confirms it on the board.";
    }

    private static string Compose(IReadOnlyList<string> lines) =>
        lines.Count == 0
            ? string.Empty
            : string.Join(Environment.NewLine, [Heading, .. lines.Select(line => $"- {line}"), AuthorisesNothing]);

    private static string Name(string key, LinkStatus status) =>
        $"\"{status.Card(key)?.Title ?? key}\" ({key})";

    private static string Verdict(LinkReport report) =>
        report.Verdict is { } verdict ? $", verdict {verdict}" : string.Empty;

    private static string Path(string? output) => output is null ? string.Empty : $": {output}";

    private static string Day(DateTimeOffset utc) =>
        utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static string Lower(string line) =>
        line.StartsWith("You ", StringComparison.Ordinal) ? "you " + line[4..] : line;

    private static bool Same(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
