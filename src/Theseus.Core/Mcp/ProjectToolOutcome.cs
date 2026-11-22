namespace Theseus.Core.Mcp;

/// <summary>What a project tool tells the session: what it did, or why it refused.</summary>
/// <param name="Text">The words the session reads.</param>
/// <param name="IsError">Whether the call was refused.</param>
public sealed record ProjectToolOutcome(string Text, bool IsError = false)
{
    /// <summary>A refusal with its reason.</summary>
    public static ProjectToolOutcome Refused(string reason) => new(reason, true);
}
