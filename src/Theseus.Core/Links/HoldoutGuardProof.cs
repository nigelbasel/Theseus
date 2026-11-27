using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Theseus.Core.Guards;

namespace Theseus.Core.Links;

/// <summary>
/// Whether an installed holdout guard works: run as the hook runs it, on a fixture folder made for the check, it must
/// refuse a session with no role a read of the holdout and allow a verifier the same read. Never touches a real holdout.
/// </summary>
public static class HoldoutGuardProof
{
    /// <summary>Node on the PATH, as the hook command finds it, or null.</summary>
    public static string? FindNode() =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory.Trim('"'), "node.exe"))
            .FirstOrDefault(File.Exists);

    /// <summary>Runs the guard at a path against a fixture folder.</summary>
    /// <param name="guardPath">The installed guard.</param>
    /// <param name="timeout">How long each run may take; null for 15 seconds. The hook itself allows 10.</param>
    public static GuardOutcome Run(string guardPath, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(guardPath);

        try
        {
            return Check(guardPath, timeout ?? TimeSpan.FromSeconds(15));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException
                                          or System.ComponentModel.Win32Exception)
        {
            return new GuardOutcome(false, $"Could not run the check: {exception.Message}");
        }
    }

    private static GuardOutcome Check(string guardPath, TimeSpan timeout)
    {
        if (FindNode() is not { } node)
        {
            return new GuardOutcome(false, "Node is not on the PATH, so the hook cannot run the guard.");
        }

        var fixture = Path.Combine(Path.GetTempPath(), "theseus-guard-check", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(fixture, "local", "Theseus"));
        var settings = Path.Combine(fixture, "settings.json");
        var holdout = Path.Combine(fixture, "hold");
        File.WriteAllText(settings, JsonSerializer.Serialize(new { holdoutFolder = holdout }), new UTF8Encoding(false));
        var scenario = Path.Combine(holdout, "scenarios", "check.md");

        try
        {
            var noRole = Refuses(node, guardPath, fixture, settings, scenario, role: null, timeout);
            if (noRole is null)
            {
                return new GuardOutcome(false, $"Checked on a fixture folder: the installed guard did not answer within {timeout.TotalSeconds:0} seconds.");
            }

            if (noRole == false)
            {
                return new GuardOutcome(false,
                    "Checked on a fixture folder: the installed guard did not refuse a read of the holdout by a session with no role, so it protects nothing.");
            }

            var verifier = Refuses(node, guardPath, fixture, settings, scenario, role: "verifier", timeout);
            if (verifier is null)
            {
                return new GuardOutcome(false, $"Checked on a fixture folder: the installed guard did not answer within {timeout.TotalSeconds:0} seconds.");
            }

            if (verifier == true)
            {
                return new GuardOutcome(false,
                    "Checked on a fixture folder: the installed guard refused a verifier's read of the holdout, so no verifier can run.");
            }

            return new GuardOutcome(true,
                "Checked on a fixture folder: it refused a read of the holdout by a session with no role, and allowed a verifier's.");
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

    // Whether the guard refused, or null when it did not answer in time.
    private static bool? Refuses(string node, string guardPath, string fixture, string settings, string scenario, string? role,
        TimeSpan timeout)
    {
        var start = new ProcessStartInfo(node)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add(guardPath);
        start.Environment["THESEUS_SETTINGS"] = settings;
        start.Environment["LOCALAPPDATA"] = Path.Combine(fixture, "local");
        start.Environment["CLAUDE_CONFIG_DIR"] = Path.Combine(fixture, ".claude");
        start.Environment.Remove("THESEUS_ROLE");
        start.Environment.Remove("THESEUS_HOLDOUT_AUTHOR");

        if (role is not null)
        {
            start.Environment["THESEUS_ROLE"] = role;
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Node did not start.");
        process.StandardInput.Write(JsonSerializer.Serialize(new
        {
            cwd = fixture,
            tool_name = "Read",
            tool_input = new { file_path = scenario },
        }));
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync();

        if (!process.WaitForExit(timeout))
        {
            process.Kill(entireProcessTree: true);

            return null;
        }

        return output.Result.Contains("\"deny\"", StringComparison.Ordinal);
    }
}
