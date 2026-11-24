namespace Theseus.Core.Projects;

/// <summary>
/// One ask of a criterion's waiting note: a note may list several, as <c>Waiting: owner: A; work: B; owner: C</c>, and each
/// is on its own whom (project-view-scale, 2026-11-24: a real project's owner lines carried their work asks with them).
/// </summary>
/// <param name="IsOwner">Whether it waits on the owner; otherwise on work.</param>
/// <param name="What">What it waits for, in plain words.</param>
public sealed record WaitingAsk(bool IsOwner, string What);
