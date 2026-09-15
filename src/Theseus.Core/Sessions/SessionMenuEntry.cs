namespace Theseus.Core.Sessions;

/// <summary>One live session as the taskbar menu lists it.</summary>
/// <param name="SessionId">The session a click brings forward.</param>
/// <param name="Title">What the entry says.</param>
/// <param name="Description">Its tooltip: the directory the session is working in.</param>
public sealed record SessionMenuEntry(string SessionId, string Title, string Description);
