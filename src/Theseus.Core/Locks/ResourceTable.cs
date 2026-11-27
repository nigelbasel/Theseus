namespace Theseus.Core.Locks;

/// <summary>
/// What each resource's capacity is and what it clashes with. A resource the table does not know clashes with every
/// hold, so a name an agent invents is always safe and only ever slow, until it is ruled on.
/// </summary>
public sealed class ResourceTable
{
    public const string SqlContainers = "sql-containers";

    public const string CpuBuild = "cpu-build";

    public const string DockerBuild = "docker-build";

    private readonly IReadOnlyList<ResourceRule> _rules;

    public ResourceTable(IEnumerable<ResourceRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        _rules = [.. rules
            .Select(rule => rule with
            {
                Name = ResourceClaim.Name(rule.Name),
                Capacity = Math.Max(1, rule.Capacity),
                ClashesWith = [.. (rule.ClashesWith ?? []).Select(ResourceClaim.Name)],
            })
            .GroupBy(rule => rule.Name, StringComparer.Ordinal)
            .Select(group => group.Last())];
    }

    public IReadOnlyList<ResourceRule> Rules => _rules;

    public static IReadOnlyList<ResourceRule> SeedRules { get; } =
    [
        new(SqlContainers, 1, [], "SQL Server test containers starve each other."),
        new(CpuBuild, 1, [], "Whole-solution compiles; the number of slots is measured, never estimated."),
        new(DockerBuild, 1, [], "BuildKit builds and the package restore inside them."),
        new("compose:*", 1, [], "One compose project, by name."),
        new("port:*", 1, [], "A host port a stack publishes."),
        new(ResourceClaim.WorktreeKind + "*", 1, [], "A source tree's build output; two builds in one tree corrupt each other."),
        new(LockNames.DevStack, 1, [], LockNames.Describe(LockNames.DevStack)),
        new(LockNames.Llama, 1, [], LockNames.Describe(LockNames.Llama)),
    ];

    public static ResourceTable Default { get; } = Seeded();

    public static ResourceTable Seeded() => new(SeedRules);

    public ResourceTable With(ResourceRule rule) => new([.. _rules, rule]);

    public ResourceRule? Rule(string resource)
    {
        string name = ResourceClaim.Name(resource);

        return _rules.FirstOrDefault(rule => string.Equals(rule.Name, name, StringComparison.Ordinal))
               ?? _rules.FirstOrDefault(rule => rule.Covers(name));
    }

    public bool IsKnown(string resource) => Rule(resource) is not null;

    public int Capacity(string resource) => Rule(resource)?.Capacity ?? 1;

    /// <summary>Whether a request fits beside every hold: within each capacity, clashing with none of them.</summary>
    public bool Fits(IReadOnlyList<ResourceClaim> request, IEnumerable<IReadOnlyList<ResourceClaim>> holds)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(holds);

        List<ResourceClaim> held = [.. holds.SelectMany(hold => hold)];

        return request.All(claim => FitsBeside(claim, held)) && held.All(claim => !ClashesWithAny(claim, request));
    }

    /// <summary>The amount a claim takes, at most every slot the resource has.</summary>
    public int Amount(ResourceClaim claim)
    {
        ArgumentNullException.ThrowIfNull(claim);

        return Math.Min(Math.Max(1, claim.Amount), Capacity(claim.Resource));
    }

    private bool FitsBeside(ResourceClaim claim, IReadOnlyList<ResourceClaim> held)
    {
        if (held.Count == 0)
        {
            return true;
        }

        if (Rule(claim.Resource) is not { } rule)
        {
            return false;
        }

        int used = held.Where(other => string.Equals(other.Resource, claim.Resource, StringComparison.Ordinal)).Sum(Amount);

        return used + Amount(claim) <= rule.Capacity && !held.Any(other => rule.ClashesWithResource(other.Resource));
    }

    private bool ClashesWithAny(ResourceClaim held, IReadOnlyList<ResourceClaim> request)
    {
        if (request.Count == 0)
        {
            return false;
        }

        return Rule(held.Resource) is not { } rule || request.Any(claim => rule.ClashesWithResource(claim.Resource));
    }
}
