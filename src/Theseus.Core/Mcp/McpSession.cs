namespace Theseus.Core.Mcp;

/// <summary>A session Theseus launched, as the session time server knows it.</summary>
/// <param name="SessionId">The Claude Code session identifier.</param>
/// <param name="WorkstreamKey">The workstream it belongs to.</param>
/// <param name="WorkstreamTitle">That workstream's title.</param>
public sealed record McpSession(string SessionId, string WorkstreamKey, string WorkstreamTitle);
