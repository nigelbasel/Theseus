namespace Theseus.Core.Locks;

/// <summary>
/// The lock names from before scopes, each meaning a fixed scope. <c>tests</c> is everything the old single lock
/// guarded, so a script or session that still asks for it behaves exactly as it did.
/// </summary>
public static class PredefinedScopes
{
    public static IReadOnlyList<ResourceClaim>? Find(string? name, ResourceTable table)
    {
        ArgumentNullException.ThrowIfNull(table);

        return LockNames.Find(name) switch
        {
            LockNames.Tests =>
            [
                new ResourceClaim(ResourceTable.CpuBuild, ResourceClaim.All),
                new ResourceClaim(ResourceTable.DockerBuild, 1),
                new ResourceClaim(ResourceTable.SqlContainers, 1),
            ],
            LockNames.DevStack => [new ResourceClaim(LockNames.DevStack, 1)],
            LockNames.Llama => [new ResourceClaim(LockNames.Llama, 1)],
            _ => null,
        };
    }
}
