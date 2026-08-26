using Theseus.Core.Messaging;

namespace Theseus.Core.Launching;

/// <summary>
/// A launch reduced to the two things that leave this process: the ticket to register, and the
/// terminal command to run. Building it is pure, so what a launch will do can be asserted without
/// opening a tab.
/// </summary>
/// <param name="Ticket">The ticket to register with the launch pipe before the terminal starts.</param>
/// <param name="TerminalExecutablePath">The terminal to start.</param>
/// <param name="TerminalArguments">Its argument vector, for <c>ProcessStartInfo.ArgumentList</c>.</param>
public sealed record WorkstreamLaunchPlan(
    LaunchTicket Ticket,
    string TerminalExecutablePath,
    IReadOnlyList<string> TerminalArguments);
