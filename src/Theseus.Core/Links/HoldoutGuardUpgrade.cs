using System.Text.RegularExpressions;

namespace Theseus.Core.Links;

/// <summary>
/// Where an installed holdout guard is: the paths the Claude Code hooks run it from (review of 1.1e PR 2c, finding 8).
/// </summary>
public static partial class HoldoutGuardUpgrade
{
    /// <summary>The guard's file name, as the hook commands name it.</summary>
    public const string FileName = "holdout-guard.js";

    /// <summary>
    /// Where installed guards may be (review of 1.1e PR 2c, finding 8): every path to the guard the Claude Code settings'
    /// hook commands name, then <c>bin\holdout-guard.js</c> in the configuration folder.
    /// </summary>
    /// <param name="settingsJson">The Claude Code settings file's text, or null when there is none.</param>
    /// <param name="configFolder">The Claude Code configuration folder.</param>
    public static IReadOnlyList<string> Candidates(string? settingsJson, string configFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configFolder);

        var named = settingsJson is null
            ? []
            : GuardPath().Matches(settingsJson).Select(match => match.Value.Replace("\\\\", "\\", StringComparison.Ordinal)
                    .Replace("~", Path.GetDirectoryName(configFolder.TrimEnd('\\', '/')) ?? "~", StringComparison.Ordinal)
                    .Replace("$HOME", Path.GetDirectoryName(configFolder.TrimEnd('\\', '/')) ?? "$HOME", StringComparison.Ordinal))
                .ToList();

        return [.. named, Path.Combine(configFolder, "bin", FileName)];
    }

    // A path ending in the guard's file name inside a hook command: a drive path, a home-relative one, or a quoted one.
    [GeneratedRegex(@"(?:[A-Za-z]:|~|\$HOME)?[^\s""'<>|]*holdout-guard\.js", RegexOptions.IgnoreCase)]
    private static partial Regex GuardPath();
}
