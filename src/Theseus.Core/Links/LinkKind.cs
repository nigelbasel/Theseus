namespace Theseus.Core.Links;

/// <summary>
/// The two ways one workstream relates to another (Nigel, 2026-10-21: more only when a real need
/// appears). Two peers working together are a note on their cards, not a kind.
/// </summary>
public enum LinkKind
{
    /// <summary>The child exists because of the parent. Lineage only; it never holds anything back.</summary>
    Spawned,

    /// <summary>The downstream should not start until the upstream's output has been accepted.</summary>
    Feeds,
}
