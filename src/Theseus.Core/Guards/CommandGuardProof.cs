using System.Diagnostics;
using System.Text.Json;
using Theseus.Core.Links;

namespace Theseus.Core.Guards;

/// <summary>
/// Whether an installed guard that reads shell commands works: run as the hook runs it, in a fixture folder made for the
/// check, it must refuse one command and pass another. Never runs either command.
/// </summary>
public static class CommandGuardProof
{
    /// <summary>Runs the guard at a path on a check.</summary>
    /// <param name="guardPath">The installed guard.</param>
    /// <param name="check">What it must refuse and pass.</param>
    /// <param name="timeout">How long each run may take; null for 15 seconds. The hook itself allows 10.</param>
    public static GuardOutcome Run(string guardPath, CommandGuardCheck check, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(guardPath);
        ArgumentNullException.ThrowIfNull(check);

        try
        {
            return Check(guardPath, check, timeout ?? TimeSpan.FromSeconds(15));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException
                                          or System.ComponentModel.Win32Exception)
        {
            return new GuardOutcome(false, $"Could not run the check: {exception.Message}");
        }
    }

    private static GuardOutcome Check(string guardPath, CommandGuardCheck check, TimeSpan timeout)
    {
        if (HoldoutGuardProof.FindNode() is not { } node)
        {
            return new GuardOutcome(false, "Node is not on the PATH, so the hook cannot run the guard.");
        }

        var fixture = Path.Combine(Path.GetTempPath(), "theseus-guard-check", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);

        try
        {
            var refused = Decision(node, guardPath, fixture, check.Refused, check.Environment, timeout);
            if (refused is null)
            {
                return NoAnswer(timeout);
            }

            if (refused != "deny")
            {
                return new GuardOutcome(false, $"Checked on a fixture folder: the installed guard did not refuse {check.RefusedSaid}, so it protects nothing.");
            }

            var passed = Decision(node, guardPath, fixture, check.Passed, check.Environment, timeout);
            if (passed is null)
            {
                return NoAnswer(timeout);
            }

            if (passed != "allow")
            {
                return new GuardOutcome(false, $"Checked on a fixture folder: the installed guard did not pass {check.PassedSaid}, so it would stop ordinary work.");
            }

            return new GuardOutcome(true, $"Checked on a fixture folder: it refused {check.RefusedSaid} and passed {check.PassedSaid}.");
        }
        finally
        {
            try
            {
                Directory.Delete(fixture, recursive: true);
            }
            catch (IOException)
            {
                // A fixture left in the temp folder harms nothing.
            }
        }
    }

    private static GuardOutcome NoAnswer(TimeSpan timeout) =>
        new(false, $"Checked on a fixture folder: the installed guard did not answer within {timeout.TotalSeconds:0} seconds.");

    // What the guard decided for a Bash call of the command: deny, ask, or allow when it said neither; null when it did
    // not answer in time.
    private static string? Decision(string node, string guardPath, string fixture, string command,
        IReadOnlyDictionary<string, string> environment, TimeSpan timeout)
    {
        var start = new ProcessStartInfo(node)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = fixture,
        };
        start.ArgumentList.Add(guardPath);

        foreach (var (name, value) in environment)
        {
            start.Environment[name] = value;
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Node did not start.");
        process.StandardInput.Write(JsonSerializer.Serialize(new
        {
            cwd = fixture,
            tool_name = "Bash",
            tool_input = new { command },
        }));
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync();

        if (!process.WaitForExit(timeout))
        {
            process.Kill(entireProcessTree: true);

            return null;
        }

        return output.Result.Contains("\"deny\"", StringComparison.Ordinal) ? "deny"
            : output.Result.Contains("\"ask\"", StringComparison.Ordinal) ? "ask"
            : "allow";
    }
}
