namespace Theseus.Core.Locks;

/// <summary>
/// One row of the resource table. A name ending in <c>:*</c> covers every resource of that kind, such as every
/// compose project. <see cref="RuledBy"/> is empty for a seeded row and names the arbiter or Nigel otherwise.
/// </summary>
public sealed record ResourceRule(
    string Name,
    int Capacity,
    IReadOnlyList<string> ClashesWith,
    string Why,
    string? RuledBy = null,
    DateTimeOffset? RuledUtc = null)
{
    public bool Covers(string resource) => Matches(Name, resource);

    public bool ClashesWithResource(string resource) =>
        ClashesWith.Any(entry => Matches(entry, resource));

    private static bool Matches(string pattern, string resource) =>
        pattern.EndsWith(":*", StringComparison.Ordinal)
            ? resource.StartsWith(pattern[..^1], StringComparison.Ordinal)
            : string.Equals(pattern, resource, StringComparison.Ordinal);
}
