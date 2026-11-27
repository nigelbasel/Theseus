namespace Theseus.Core.Locks;

/// <summary>
/// An amount of one resource a request holds or waits for: one slot unless the resource has several.
/// </summary>
public sealed record ResourceClaim(string Resource, int Amount)
{
    public const string WorktreeKind = "worktree:";

    /// <summary>Every slot the resource has, however many that is when it is granted.</summary>
    public const int All = int.MaxValue;

    /// <summary>
    /// Lower-cases names, writes a worktree's path one way however it was given, and merges repeats keeping the
    /// larger amount, so two requests naming the same thing differently are seen to clash.
    /// </summary>
    public static IReadOnlyList<ResourceClaim> Normalise(IEnumerable<ResourceClaim> claims)
    {
        ArgumentNullException.ThrowIfNull(claims);

        return [.. claims
            .Where(claim => !string.IsNullOrWhiteSpace(claim.Resource))
            .Select(claim => new ResourceClaim(Name(claim.Resource), Math.Max(1, claim.Amount)))
            .GroupBy(claim => claim.Resource, StringComparer.Ordinal)
            .Select(group => new ResourceClaim(group.Key, group.Max(claim => claim.Amount)))
            .OrderBy(claim => claim.Resource, StringComparer.Ordinal)];
    }

    public static string Name(string resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        string name = resource.Trim().ToLowerInvariant();

        if (!name.StartsWith(WorktreeKind, StringComparison.Ordinal))
        {
            return name;
        }

        string path = name[WorktreeKind.Length..].Replace('\\', '/').TrimEnd('/');

        return WorktreeKind + path;
    }

    public override string ToString() => Amount switch
    {
        1 => Resource,
        All => $"{Resource}=all",
        _ => $"{Resource}={Amount}",
    };
}
