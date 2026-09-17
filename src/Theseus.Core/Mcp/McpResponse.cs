namespace Theseus.Core.Mcp;

/// <summary>What to send back for one HTTP request to the session time server.</summary>
/// <param name="StatusCode">The HTTP status.</param>
/// <param name="Body">A JSON body, or null for none.</param>
public sealed record McpResponse(int StatusCode, string? Body);
