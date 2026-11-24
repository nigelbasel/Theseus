using System.Text.RegularExpressions;

namespace Theseus.Core.Projects;

/// <summary>
/// Who an unticked criterion waits on, from its <c>Waiting:</c> line (1.1f G37b): <c>owner: …</c>, <c>work: …</c>, or
/// <c>on &lt;who&gt;: …</c> for someone else by name (project-view-scale, git-22 for Nigel, 2026-11-24). A line may list
/// several asks, as <c>Waiting: owner: A; work: B; on the gateway team: C</c>.
/// </summary>
/// <param name="IsOwner">Whether its first ask is the owner's.</param>
/// <param name="What">What follows its first whom, to the end of the line.</param>
public sealed record CriterionWaiting(bool IsOwner, string What)
{
    /// <summary>The line's prefix.</summary>
    public const string Label = "Waiting:";

    private static readonly Regex AskBoundary =
        new(@";\s*(?=(?:owner|work|on\s[^:;]{1,80}):)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex Someone = new(@"^on\s+(?<who>[^:;]{1,80}):\s*(?<what>.*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Whom its first ask names, for a line that starts <c>on &lt;who&gt;:</c>; null otherwise.</summary>
    public string? On { get; init; }

    /// <summary>The line as the file holds it, without its indent.</summary>
    public string Line => $"{Label} {(On is not null ? $"on {On}" : IsOwner ? "owner" : "work")}: {What}";

    /// <summary>Each ask the line lists, in order.</summary>
    public IReadOnlyList<WaitingAsk> Asks =>
        [.. AskBoundary.Split(Line[Label.Length..].Trim()).Select(ParseAsk).OfType<WaitingAsk>().Where(ask => ask.What.Length > 0)];

    /// <summary>The asks of the owner or of someone named, as against work.</summary>
    public IReadOnlyList<WaitingAsk> OwnerAsks => [.. Asks.Where(ask => ask.IsOwner || ask.On is not null)];

    /// <summary>The line's text after <see cref="Label"/> for these asks, or null for none: what a write puts back.</summary>
    public static string? Join(IEnumerable<WaitingAsk> asks)
    {
        ArgumentNullException.ThrowIfNull(asks);

        var parts = asks.Select(ask => $"{(ask.On is not null ? $"on {ask.On}" : ask.IsOwner ? "owner" : "work")}: {ask.What}").ToList();

        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    /// <summary>Reads a continuation line's text, or null when it is not a waiting note.</summary>
    public static CriterionWaiting? Parse(string trimmed)
    {
        ArgumentNullException.ThrowIfNull(trimmed);

        if (!trimmed.StartsWith(Label, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = trimmed[Label.Length..].TrimStart();

        if (Someone.Match(rest) is { Success: true } named && named.Groups["what"].Value.Trim() is { Length: > 0 } said)
        {
            return new CriterionWaiting(false, said) { On = named.Groups["who"].Value.Trim() };
        }

        foreach (var (who, isOwner) in new[] { ("owner:", true), ("work:", false) })
        {
            if (rest.StartsWith(who, StringComparison.OrdinalIgnoreCase) && rest[who.Length..].Trim() is { Length: > 0 } what)
            {
                return new CriterionWaiting(isOwner, what);
            }
        }

        return null;
    }

    private static WaitingAsk? ParseAsk(string part)
    {
        var text = part.Trim();

        if (Someone.Match(text) is { Success: true } named)
        {
            return new WaitingAsk(false, named.Groups["what"].Value.Trim()) { On = named.Groups["who"].Value.Trim() };
        }

        foreach (var (who, isOwner) in new[] { ("owner:", true), ("work:", false) })
        {
            if (text.StartsWith(who, StringComparison.OrdinalIgnoreCase))
            {
                return new WaitingAsk(isOwner, text[who.Length..].Trim());
            }
        }

        return null;
    }
}
