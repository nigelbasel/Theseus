using Theseus.Core.Diagnostics;
using Theseus.Core.Domain;

namespace Theseus.Core.Stopping;

/// <summary>Decides where a workstream's handoff should be written.</summary>
public static class HandoffPath
{
    private static readonly string[] NamePatterns = ["HANDOFF*.md", "HANDOVER*.md", "SESSION-STATUS*.md", "NOTES.md"];

    private static readonly string[] UninterestingDirectories =
        ["node_modules", "bin", "obj", "dist", "packages", ".git", ".vs"];

    /// <summary>
    /// The document the workstream chose, the one its sessions have been keeping, or a conventional
    /// name beside the working directory. A stop request has to name a specific file: told merely to
    /// "write a handoff", each session would choose its own place and the next morning would be a search.
    /// </summary>
    public static string For(Workstream workstream)
    {
        ArgumentNullException.ThrowIfNull(workstream);

        if (!string.IsNullOrWhiteSpace(workstream.StatusDocumentPath))
        {
            return workstream.StatusDocumentPath;
        }

        return Discover(workstream) ?? Path.Combine(RootOf(workstream), $"HANDOFF-{workstream.Key}.md");
    }

    /// <summary>
    /// The handover document the sessions have actually been writing, if one is already on disk.
    /// Sessions leave their notes wherever the work lives rather than where Theseus would have put
    /// them, so the conventional name is the last resort and not the first guess.
    /// </summary>
    public static string? Discover(Workstream workstream)
    {
        ArgumentNullException.ThrowIfNull(workstream);

        return Roots(workstream)
            .SelectMany(SearchedDirectories)
            .SelectMany(CandidatesIn)
            .DistinctBy(file => file.FullName, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(file => Mentions(file.Name, workstream.Key))
            .ThenByDescending(file => file.LastWriteTimeUtc)
            .Select(file => file.FullName)
            .FirstOrDefault();
    }

    private static IEnumerable<string> Roots(Workstream workstream) =>
        new[] { RootOf(workstream) }.Concat(workstream.AddDirectories)
            .Where(directory => !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory));

    private static string RootOf(Workstream workstream) =>
        string.IsNullOrWhiteSpace(workstream.WorkingDirectory)
            ? Directory.GetCurrentDirectory()
            : workstream.WorkingDirectory;

    private static IEnumerable<string> SearchedDirectories(string root)
    {
        yield return root;

        foreach (var child in Children(root))
        {
            yield return child;
        }
    }

    private static IReadOnlyList<string> Children(string root)
    {
        try
        {
            return [.. Directory.EnumerateDirectories(root).Where(IsWorthSearching)];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Warning(exception, "Could not list the directories under {Root}", root);
            return [];
        }
    }

    private static bool IsWorthSearching(string directory)
    {
        var name = Path.GetFileName(directory);

        return !name.StartsWith('.') && !UninterestingDirectories.Contains(name, StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<FileInfo> CandidatesIn(string directory)
    {
        try
        {
            return
            [
                .. NamePatterns
                    .SelectMany(pattern => Directory.EnumerateFiles(directory, pattern))
                    .Select(path => new FileInfo(path))
            ];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Warning(exception, "Could not look for handover notes in {Directory}", directory);
            return [];
        }
    }

    private static bool Mentions(string fileName, string key) =>
        fileName.Contains(key, StringComparison.OrdinalIgnoreCase);
}
