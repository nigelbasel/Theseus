namespace Theseus.Core.Mcp;

/// <summary>
/// What a session may ask of its owner's gate, and report about it (1.1e AC 28). Nothing here accepts a spec: the owner
/// does that on the board.
/// </summary>
public interface ISpecGates
{
    /// <summary>The calling workstream's gates and whether each is accepted, for the version on disk now.</summary>
    string Gates(string callerKey);

    /// <summary>
    /// Keeps the owner's words for him to confirm on the board; the reason it is refused, or null when it is kept.
    /// </summary>
    string? ReportAcceptance(string callerKey, string slug, string version, string ownerWords);
}
