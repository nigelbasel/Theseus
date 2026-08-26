using Theseus.Core.Diagnostics;

namespace Theseus.Core.Domain;

/// <summary>
/// Reads a repository's origin remote from its own files, without invoking git.
///
/// A worktree's <c>.git</c> is a file rather than a directory, and the configuration it points at
/// lives in the main repository via <c>commondir</c>. That matters here more than it looks: feature
/// work happens in worktrees, so the worktrees are exactly the directories whose pull requests are
/// worth finding.
/// </summary>
public sealed class GitRemoteReader : IGitRemoteReader
{
    /// <inheritdoc />
    public string? ReadOriginUrl(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        try
        {
            var configuration = FindConfiguration(directory);

            return configuration is null ? null : ParseOriginUrl(File.ReadAllLines(configuration));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Debug("Could not read the git remote under {Directory}: {Reason}",
                directory, exception.Message);

            return null;
        }
    }

    /// <summary>Reads the origin url out of the lines of a git config file.</summary>
    public static string? ParseOriginUrl(IEnumerable<string> configurationLines)
    {
        ArgumentNullException.ThrowIfNull(configurationLines);

        var inOrigin = false;

        foreach (var raw in configurationLines)
        {
            var line = raw.Trim();

            if (line.StartsWith('['))
            {
                inOrigin = line.Replace(" ", string.Empty)
                    .Equals("[remote\"origin\"]", StringComparison.OrdinalIgnoreCase);

                continue;
            }

            if (!inOrigin || !line.StartsWith("url", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator > 0)
            {
                return line[(separator + 1)..].Trim();
            }
        }

        return null;
    }

    private static string? FindConfiguration(string directory)
    {
        for (var current = new DirectoryInfo(directory); current is not null; current = current.Parent)
        {
            var marker = Path.Combine(current.FullName, ".git");

            if (Directory.Exists(marker))
            {
                var configuration = Path.Combine(marker, "config");

                return File.Exists(configuration) ? configuration : null;
            }

            if (File.Exists(marker))
            {
                return FromWorktree(marker);
            }
        }

        return null;
    }

    private static string? FromWorktree(string marker)
    {
        var pointer = File.ReadAllText(marker).Trim();

        if (!pointer.StartsWith("gitdir:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var gitDirectory = pointer["gitdir:".Length..].Trim();

        if (!Path.IsPathRooted(gitDirectory))
        {
            gitDirectory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(marker)!, gitDirectory));
        }

        var commonMarker = Path.Combine(gitDirectory, "commondir");

        var common = File.Exists(commonMarker)
            ? Path.GetFullPath(Path.Combine(gitDirectory, File.ReadAllText(commonMarker).Trim()))
            : gitDirectory;

        var configuration = Path.Combine(common, "config");

        return File.Exists(configuration) ? configuration : null;
    }
}
