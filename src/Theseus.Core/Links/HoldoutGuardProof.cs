using System.Diagnostics;
using System.Text;
using System.Text.Json;

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
    public static HoldoutGuardOutcome Run(string guardPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(guardPath);

        if (FindNode() is not { } node)
        {
            return new HoldoutGuardOutcome(false, "Node is not on the PATH, so the hook cannot run the guard.");
        }

        var fixture = Path.Combine(Path.GetTempPath(), "theseus-guard-check", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(fixture, "local", "Theseus"));
        var settings = Path.Combine(fixture, "settings.json");
        var holdout = Path.Combine(fixture, "hold");
        File.WriteAllText(settings, JsonSerializer.Serialize(new { holdoutFolder = holdout }), new UTF8Encoding(false));
        var scenario = Path.Combine(holdout, "scenarios", "check.md");

        try
        {
            if (!Refuses(node, guardPath, fixture, settings, scenario, role: null))
            {
                return new HoldoutGuardOutcome(false,
                    "Checked on a fixture folder: the installed guard did not refuse a read of the holdout by a session with no role, so it protects nothing.");
            }

            if (Refuses(node, guardPath, fixture, settings, scenario, role: "verifier"))
            {
                return new HoldoutGuardOutcome(false,
                    "Checked on a fixture folder: the installed guard refused a verifier's read of the holdout, so no verifier can run.");
            }

            return new HoldoutGuardOutcome(true,
                "Checked on a fixture folder: it refused a read of the holdout by a session with no role, and allowed a verifier's.");
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
        }
    }

    private static bool Refuses(string node, string guardPath, string fixture, string settings, string scenario, string? role)
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
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(30_000);

        return output.Contains("\"deny\"", StringComparison.Ordinal);
    }
}
