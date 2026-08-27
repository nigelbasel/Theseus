using System.Diagnostics;
using Theseus.Core.Diagnostics;

namespace Theseus.Core.Terminal;

/// <inheritdoc />
public sealed class TerminalResolver : ITerminalResolver
{
    private const string ProcessName = "WindowsTerminal";

    private readonly string _configuredPath;

    /// <summary>Creates a resolver, optionally pinned to a path the user chose in settings.</summary>
    public TerminalResolver(string? configuredPath = null) => _configuredPath = configuredPath ?? string.Empty;

    /// <summary>The <c>wt.exe</c> app execution alias for the current user, whether or not it exists.</summary>
    public static string AliasPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Microsoft", "WindowsApps", "wt.exe");

    /// <inheritdoc />
    public TerminalResolution Resolve()
    {
        if (!string.IsNullOrWhiteSpace(_configuredPath) && File.Exists(_configuredPath))
        {
            return new TerminalResolution(_configuredPath, TerminalSource.RunningInstance, 0);
        }

        var alias = File.Exists(AliasPath) ? AliasPath : null;

        return TerminalSelector.Choose(RunningTerminalPaths(), alias, InstalledTerminals.Paths());
    }

    private static List<string> RunningTerminalPaths()
    {
        var paths = new List<string>();

        foreach (var process in Process.GetProcessesByName(ProcessName))
        {
            using (process)
            {
                if (ProcessImagePath.For(process.Id) is { } fileName)
                {
                    paths.Add(fileName);
                }
                else
                {
                    TheseusLog.Debug("Could not read the image path of terminal process {Pid}", process.Id);
                }
            }
        }

        return paths;
    }
}
