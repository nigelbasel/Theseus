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
