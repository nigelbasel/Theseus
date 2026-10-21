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
        "A link lets you read the documents it names. Only a hand-over moves scope, and only the scope it"
        + " names. These links authorise no Jira item, branch, worktree, push or PR: each of those is"
        + " Nigel's separate ask.";

    private const string Heading = "Related workstreams (from the Theseus board):";

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
    /// What changed in a resumed session's links since its last session started: links made and
    /// inputs accepted after then. A resume keeps its conversation, so the rest is already known.
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
        }

        return Compose(lines);
    }

    private static IEnumerable<string> Lines(string key, Link link, LinkStatus status)
    {
        if (link.Kind == LinkKind.Spawned && Same(link.To, key))
        {
            yield return $"You were spawned by {Name(link.From, status)}."
                         + (link.Brief is { } brief ? $" Your brief is {brief}." : string.Empty)
                         + (link.HandOver is { } scope ? $" Scope handed over to you: {scope}." : string.Empty);
        }
        else if (link.Kind == LinkKind.Spawned)
        {
            yield return link.HandOver is { } scope
                ? $"Scope handed over to {Name(link.To, status)} on {Day(link.CreatedUtc)}: {scope}. It is no longer yours."
                : $"You spawned {Name(link.To, status)}.";
        }
        else if (Same(link.From, key))
        {
            yield return $"{Name(link.To, status)} waits on your output"
                         + (link.Output is { } output ? $": write it to {output}" : string.Empty)
                         + ". Nigel accepts it on the board when it is ready.";
        }
        else if (link.SatisfiedUtc is { } accepted)
        {
            yield return $"You read the output of {Name(link.From, status)}, accepted by Nigel {Day(accepted)}"
                         + $"{Path(link.Output)}.";
        }
        else
        {
            yield return $"You wait on {Name(link.From, status)}{Path(link.Output)}. Nigel has not accepted it yet.";
        }
    }

    private static string Compose(IReadOnlyList<string> lines) =>
        lines.Count == 0
            ? string.Empty
            : string.Join(Environment.NewLine, [Heading, .. lines.Select(line => $"- {line}"), AuthorisesNothing]);

    private static string Name(string key, LinkStatus status) =>
        $"\"{status.Card(key)?.Title ?? key}\" ({key})";

    private static string Path(string? output) => output is null ? string.Empty : $": {output}";

    private static string Day(DateTimeOffset utc) =>
        utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static string Lower(string line) =>
        line.StartsWith("You ", StringComparison.Ordinal) ? "you " + line[4..] : line;

    private static bool Same(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
