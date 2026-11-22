using System.Text.RegularExpressions;

namespace Theseus.Core.Links;

/// <summary>
/// Whether installing this Theseus's holdout guard would drop protection without a word (review of 1.1e PR 2b; git-22,
/// 2026-11-21): an older installed guard names its own holdout in the file, while this one reads the folder from the
/// settings and, with none set, protects only the default folder. Until a folder is set the board says so, at start.
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

    /// <summary>What every guard that protects the holdout folders Theseus finds per project contains (1.1e AC 6e).</summary>
    public const string FoldersMarker = HoldoutFolderFile.FileName;

    /// <summary>
    /// The warning, or null when there is nothing to lose: no guard is installed, or it reads the holdout folders Theseus
    /// writes (1.1e AC 6e: the warning is needed only when the installed guard does not read them).
    /// </summary>
    /// <param name="installedGuard">The installed guard's text, or null when none is installed.</param>
    public static string? Warning(string? installedGuard) =>
        installedGuard is not null && !installedGuard.Contains(FoldersMarker, StringComparison.Ordinal)
            ? "The installed holdout guard does not read holdout-folders.json, so it does not protect the holdout folders "
              + "Theseus now finds for each project (beside a project's repo, or its own). Install the guard from this version "
              + "to protect them; Tools > Options lists each folder."
            : null;

    // A path ending in the guard's file name inside a hook command: a drive path, a home-relative one, or a quoted one.
    [GeneratedRegex(@"(?:[A-Za-z]:|~|\$HOME)?[^\s""'<>|]*holdout-guard\.js", RegexOptions.IgnoreCase)]
    private static partial Regex GuardPath();
}
