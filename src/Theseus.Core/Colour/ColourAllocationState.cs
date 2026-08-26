namespace Theseus.Core.Colour;

/// <summary>
/// Everything the allocator needs to choose the next tab colour. Colours in use may include
/// manual overrides that are not palette entries; they still push new allocations away.
/// </summary>
/// <param name="InUse">Colours currently held by live workstreams.</param>
/// <param name="RecentlyFreed">Colours released by archived workstreams, in any order.</param>
public sealed record ColourAllocationState(
    IReadOnlyList<string> InUse,
    IReadOnlyList<TabColourRelease> RecentlyFreed)
{
    /// <summary>The state of a board with no workstreams on it.</summary>
    public static ColourAllocationState Empty { get; } = new([], []);
}
