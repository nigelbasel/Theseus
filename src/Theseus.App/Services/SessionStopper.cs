using System.Diagnostics;
using Theseus.Core.Abstractions;
using Theseus.Core.Diagnostics;
using Theseus.Core.Stopping;

namespace Theseus.App.Services;

/// <summary>
/// Asks running sessions to write a handoff and exit.
///
/// Theseus cannot message a session itself, so it starts one short-lived headless session and has
/// that carry the request. Every hop is a supported interface: no synthesised keystrokes, and no
/// undocumented pipe protocol that a Claude Code release could change underneath it.
///
/// Nothing is ever killed. A session that will not stop is reported as still running, because the
/// value of stopping is the handoff it writes on the way out, and a killed process writes nothing.
/// </summary>
public sealed class SessionStopper
{
    private readonly IClaudeLocator _claude;

    /// <summary>Creates the stopper.</summary>
    public SessionStopper(IClaudeLocator claude) => _claude = claude;

    /// <summary>Delivers a stop request to each target.</summary>
    /// <returns>How many sessions were asked, or null when the request could not be sent.</returns>
    public async Task<int?> RequestAsync(
        string? stopPrompt,
        IReadOnlyList<StopTarget> targets,
        CancellationToken cancellationToken)
    {
        if (StopInstruction.Compose(stopPrompt, targets) is not { } instruction)
        {
            return 0;
        }

        if (_claude.Locate() is not { } claudePath)
        {
            TheseusLog.Warning("Cannot ask any session to stop: Claude Code was not found");

            return null;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = claudePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        startInfo.ArgumentList.Add("-p");
        startInfo.ArgumentList.Add(instruction);

        TheseusLog.Information("Asking {Count} session(s) to write a handoff and exit", targets.Count);

        try
        {
            using var courier = Process.Start(startInfo);

            if (courier is null)
            {
                return null;
            }

            var output = await courier.StandardOutput.ReadToEndAsync(cancellationToken);
            await courier.WaitForExitAsync(cancellationToken);

            if (courier.ExitCode != 0)
            {
                TheseusLog.Warning("The stop request exited {Code}: {Output}", courier.ExitCode, output.Trim());

                return null;
            }

            return targets.Count;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            TheseusLog.Error(exception, "The stop request could not be sent");

            return null;
        }
    }
}
