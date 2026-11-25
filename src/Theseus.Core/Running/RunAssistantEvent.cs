namespace Theseus.Core.Running;

/// <summary>A message from the model: what the card shows as the run's latest line, and that call's tokens.</summary>
/// <param name="MessageId">The API message's id. Claude Code writes one line per content block, each repeating the message's usage, so tokens are counted once per id.</param>
/// <param name="Line">Its text or tool call as one short line, or null when it has neither.</param>
/// <param name="Usage">The call's tokens.</param>
public sealed record RunAssistantEvent(string? MessageId, string? Line, RunTokens Usage) : RunStreamEvent;
