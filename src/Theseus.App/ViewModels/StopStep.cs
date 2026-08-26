namespace Theseus.App.ViewModels;

/// <summary>One session waiting to be stopped, and what to say to it.</summary>
/// <param name="Title">The workstream's title, for saying which tab to look at.</param>
/// <param name="SessionName">The session's name, which is how its tab is found.</param>
/// <param name="Pid">The Claude Code process, which is how its tab is found.</param>
/// <param name="Text">The request, ready for the user to send.</param>
public sealed record StopStep(string Title, string SessionName, int Pid, string Text);
