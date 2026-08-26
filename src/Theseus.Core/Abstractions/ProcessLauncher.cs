using System.Diagnostics;

namespace Theseus.Core.Abstractions;

/// <summary>
/// Starts a child process that shares the caller's console. No stream is redirected: redirection
/// would replace the child's console with a pipe and Claude Code would lose its interactive terminal.
/// </summary>
public sealed class ProcessLauncher : IProcessLauncher
{
    /// <inheritdoc />
    public IProcessHandle Start(ProcessStartRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var startInfo = new ProcessStartInfo
        {
            FileName = request.ExecutablePath,
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
        };

        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start '{request.ExecutablePath}'.");

        return new ProcessHandle(process);
    }
}
