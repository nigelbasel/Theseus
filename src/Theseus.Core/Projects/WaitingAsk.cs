using System.Text.RegularExpressions;

namespace Theseus.Core.Projects;

/// <summary>
/// One ask of a criterion's waiting note: a note may list several, each on its own whom (project-view-scale, 2026-11-24).
/// </summary>
/// <param name="IsOwner">Whether it is written as the owner's (<c>owner:</c>).</param>
/// <param name="What">What it waits for, in plain words.</param>
public sealed record WaitingAsk(bool IsOwner, string What)
{
    /// <summary>The longest a whom may be, as the file's grammar reads it.</summary>
    public const int LongestWho = 80;

    // An older line's "Who: what" is read as a whom only when Who is short, as a name or team is: "Decide: A or B" or "confirm
    // the URL: https://..." stay the owner's asks (review of the follow-on, finding 5).
    private const int MostWordsInAWho = 4;

    /// <summary>Whom it names, for an ask written <c>on &lt;who&gt;: …</c>; null otherwise.</summary>
    public string? On { get; init; }

    /// <summary>Whether it waits on work.</summary>
    public bool IsWork => !IsOwner && On is null;

    /// <summary>
    /// Who it is on, when not the owner and not work: its <c>on &lt;who&gt;:</c>, or, for an <c>owner:</c> line written
    /// before that form, the "Who" of "Who: what" unless it names the owner. Null when it is the owner's, or work's.
    /// </summary>
    /// <param name="ownerNames">The owner's names, matched as whole words for lines written before the <c>on</c> form.</param>
    public string? Who(IReadOnlyCollection<string> ownerNames)
    {
        ArgumentNullException.ThrowIfNull(ownerNames);

        if (On is not null)
        {
            return NamesTheOwner(On, ownerNames) ? null : On;
        }

        if (!IsOwner)
        {
            return null;
        }

        var colon = What.IndexOf(": ", StringComparison.Ordinal);

        if (colon <= 0 || colon > LongestWho)
        {
            return null;
        }

        var who = What[..colon].Trim();

        // A wait naming the owner and others, "the product team with <owner>", is his.
        return who.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > MostWordsInAWho || NamesTheOwner(who, ownerNames) ? null : who;
    }

    /// <summary>Whether a whom may be written in the file: one to <see cref="LongestWho"/> characters, no colon or semicolon.</summary>
    public static bool IsWritableWho(string? who) =>
        who?.Trim() is { Length: > 0 and <= LongestWho } trimmed && trimmed.IndexOfAny([':', ';']) < 0;

    private static bool NamesTheOwner(string who, IReadOnlyCollection<string> ownerNames) =>
        Regex.IsMatch(who, "^(owner|you|me)$", RegexOptions.IgnoreCase)
        || ownerNames.Any(name => name.Length > 0 && Regex.IsMatch(who, $@"\b{Regex.Escape(name)}\b", RegexOptions.IgnoreCase));

    /// <summary>Whether it waits on the owner.</summary>
    public bool IsOnOwner(IReadOnlyCollection<string> ownerNames) => IsOwner && Who(ownerNames) is null;

    /// <summary>What it asks, without the "Who: " of an older line naming someone else.</summary>
    public string Asked(IReadOnlyCollection<string> ownerNames) =>
        On is null && Who(ownerNames) is { } who ? What[(who.Length + 2)..].Trim() : What;
}
