using Theseus.Core.Domain;

namespace Theseus.App.ViewModels;

/// <summary>One session to be stopped, and everything the route to it needs.</summary>
/// <param name="Title">The workstream's title, for saying which session is meant.</param>
/// <param name="WorkstreamKey">The workstream asking, recorded with the request.</param>
/// <param name="SessionId">The session the request is addressed to, which is how a hook finds it.</param>
/// <param name="SessionName">The session's name, which is how its tab is found.</param>
/// <param name="Pid">The Claude Code process, which is how its tab is found.</param>
/// <param name="Lifecycle">What the session is doing, which decides how the request reaches it.</param>
/// <param name="Text">The request itself, with its handoff path already filled in.</param>
/// <param name="HandoffPath">The document it asks for, kept apart so it can be looked for later.</param>
public sealed record StopStep(
    string Title,
    string WorkstreamKey,
    string SessionId,
    string SessionName,
    int Pid,
    SessionLifecycle Lifecycle,
    string Text,
    string HandoffPath);
