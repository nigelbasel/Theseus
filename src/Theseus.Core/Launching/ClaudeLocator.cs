using Theseus.Core.Abstractions;
using Theseus.Core.Diagnostics;

namespace Theseus.Core.Launching;

/// <summary>
/// Finds <c>claude.exe</c>. A real executable is required, not a shell alias: the launcher shim
/// starts it with <c>CreateProcess</c>, which cannot run a batch file or a PowerShell function.
/// </summary>
public sealed class ClaudeLocator : IClaudeLocator
{
    private const string ExecutableName = "claude.exe";

    private readonly string _configuredPath;

    /// <summary>Creates a locator, optionally pinned to a path the user chose in settings.</summary>
    public ClaudeLocator(string? configuredPath = null) => _configuredPath = configuredPath ?? string.Empty;

    /// <summary>The usual per-user install location.</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", ExecutableName);

    /// <inheritdoc />
    public string? Locate()
    {
        if (!string.IsNullOrWhiteSpace(_configuredPath) && File.Exists(_configuredPath))
        {
            return _configuredPath;
        }

        if (File.Exists(DefaultPath))
        {
            return DefaultPath;
        }

        return OnPath();
    }

    private static string? OnPath()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(directory.Trim(), ExecutableName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch (ArgumentException exception)
            {
                TheseusLog.Debug("Skipped an unusable PATH entry while looking for Claude Code: {Reason}", exception.Message);
            }
        }

        return null;
    }
}
