namespace Theseus.Core.Stopping;

/// <summary>One running session to be asked to stop, and where its handoff should go.</summary>
/// <param name="SessionName">The name the session is addressed by.</param>
/// <param name="HandoffPath">The file the session should write its handoff to.</param>
public sealed record StopTarget(string SessionName, string HandoffPath);
