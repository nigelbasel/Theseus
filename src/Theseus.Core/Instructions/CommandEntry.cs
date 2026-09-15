namespace Theseus.Core.Instructions;

/// <summary>One skill, command or agent a session could use.</summary>
/// <param name="Kind">Skill, command or agent.</param>
/// <param name="Name">What it is invoked as: <c>deploy</c>, <c>posthog:insights</c>, <c>apps/web:deploy</c>.</param>
/// <param name="Path">The file that defines it.</param>
/// <param name="Origin">Where it was found.</param>
/// <param name="Description">Its own description, from its front matter.</param>
public sealed record CommandEntry(
    CommandKind Kind,
    string Name,
    string Path,
    CommandOrigin Origin,
    string Description)
{
    /// <summary>The file that takes this one's name instead, when one does. Null when this one is used.</summary>
    public string? HiddenBy { get; init; }

    /// <summary>Whether it becomes available only once the session works in its directory.</summary>
    public bool LoadsOnDemand { get; init; }
}
