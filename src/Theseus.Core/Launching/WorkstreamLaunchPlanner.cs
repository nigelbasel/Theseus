using Theseus.Core.Messaging;
using Theseus.Core.Terminal;

namespace Theseus.Core.Launching;

/// <summary>Builds the plan for one workstream launch.</summary>
public static class WorkstreamLaunchPlanner
{
    /// <summary>Turns a launch request into a registrable ticket and a terminal command.</summary>
    public static WorkstreamLaunchPlan Plan(WorkstreamLaunchRequest request, DateTimeOffset expiresUtc)
    {
        ArgumentNullException.ThrowIfNull(request);

        var workstream = request.Workstream;

        var spec = new LaunchSpec(
            request.SessionId,
            request.Mode,
            request.ClaudeExecutablePath,
            workstream.WorkingDirectory,
            workstream.Title,
            SessionPrompt.Compose(request.Preamble, workstream.Prompt),
            workstream.AddDirectories);

        var tab = new TerminalTabRequest(
            request.TerminalWindowName,
            workstream.Title,
            workstream.ColorHex,
            workstream.WorkingDirectory,
            request.LauncherPath,
            ["--pipe", request.PipeName, "--ticket", request.Ticket]);

        return new WorkstreamLaunchPlan(
            new LaunchTicket(request.Ticket, workstream.Key, spec, expiresUtc),
            request.TerminalExecutablePath,
            WtCommandBuilder.Build(tab));
    }
}
