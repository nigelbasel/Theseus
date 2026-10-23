namespace Theseus.Core.Links;

/// <summary>
/// How one workstream relates to another (Nigel, 2026-10-23): a flow of stages, each handing over to
/// the next, where a parent may have several children and a child may report back. Two peers working
/// together are a note on their cards, not a kind.
/// </summary>
public enum LinkKind
{
    /// <summary>
    /// The parent hands its work over to the child, which carries it into the next stage. A child has
    /// one parent. When the link names a document, the child waits until Nigel accepts it.
    /// </summary>
    HandsOver,

    /// <summary>
    /// The child reports its own output back to the parent that handed over to it. It never holds the
    /// parent back: the parent only runs again when Nigel starts it.
    /// </summary>
    ReportsBack,
}
