namespace Theseus.Core.Running;

/// <summary>The session began: Claude Code's <c>system</c>/<c>init</c> line.</summary>
/// <param name="SessionId">The session it runs under.</param>
public sealed record RunInitEvent(string SessionId) : RunStreamEvent;
