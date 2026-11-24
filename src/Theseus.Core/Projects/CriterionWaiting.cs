namespace Theseus.Core.Projects;

/// <summary>
/// Who an unticked criterion waits on, from its <c>Waiting: owner: …</c> or <c>Waiting: work: …</c> line (1.1f G37b). An
/// owner note puts the criterion in "Waits for you"; a work note shows on the objective in muted text.
/// </summary>
/// <param name="IsOwner">Whether it waits on the owner; otherwise on work.</param>
/// <param name="What">What it waits for, in plain words.</param>
public sealed record CriterionWaiting(bool IsOwner, string What)
{
    /// <summary>The line's prefix.</summary>
    public const string Label = "Waiting:";

    /// <summary>The line as the file holds it, without its indent.</summary>
    public string Line => $"{Label} {(IsOwner ? "owner" : "work")}: {What}";

    /// <summary>
    /// Each ask the note lists, split at "; owner:" and "; work:", in order; the first is on <see cref="IsOwner"/>'s side.
    /// </summary>
    public IReadOnlyList<WaitingAsk> Asks
    {
        get
        {
            var parts = AskBoundary.Split(What);
            var asks = new List<WaitingAsk> { new(IsOwner, parts[0].Trim()) };

            foreach (var part in parts.Skip(1))
            {
                var isOwner = part.StartsWith("owner:", StringComparison.OrdinalIgnoreCase);
                asks.Add(new WaitingAsk(isOwner, part[(isOwner ? "owner:" : "work:").Length..].Trim()));
            }

            return [.. asks.Where(ask => ask.What.Length > 0)];
        }
    }

    /// <summary>The owner's asks among <see cref="Asks"/>.</summary>
    public IReadOnlyList<WaitingAsk> OwnerAsks => [.. Asks.Where(ask => ask.IsOwner)];

    private static readonly System.Text.RegularExpressions.Regex AskBoundary =
        new(@";\s*(?=(?:owner|work):)", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>Reads a continuation line's text, or null when it is not a waiting note.</summary>
    public static CriterionWaiting? Parse(string trimmed)
    {
        ArgumentNullException.ThrowIfNull(trimmed);

        if (!trimmed.StartsWith(Label, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = trimmed[Label.Length..].TrimStart();

        foreach (var (who, isOwner) in new[] { ("owner:", true), ("work:", false) })
        {
            if (rest.StartsWith(who, StringComparison.OrdinalIgnoreCase) && rest[who.Length..].Trim() is { Length: > 0 } what)
            {
                return new CriterionWaiting(isOwner, what);
            }
        }

        return null;
    }
}
