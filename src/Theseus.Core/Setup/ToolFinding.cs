namespace Theseus.Core.Setup;

/// <summary>What the setup found of one tool the method needs.</summary>
/// <param name="Name">The tool, such as "Node".</param>
/// <param name="IsOk">Whether it is there and new enough.</param>
/// <param name="Line">What to say about it, in one line.</param>
public sealed record ToolFinding(string Name, bool IsOk, string Line);
