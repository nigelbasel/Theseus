using Theseus.Core.Diagnostics;
using Theseus.Core.Domain;

namespace Theseus.Core.Stopping;

/// <summary>Decides where a workstream's handoff should be written.</summary>
public static class HandoffPath
{
    private static readonly string[] NamePatterns = ["HANDOFF*.md", "HANDOVER*.md", "SESSION-STATUS*.md", "NOTES.md"];

    /// <summary>The same names with nothing appended: notes not claimed by any one workstream.</summary>
    private static readonly string[] GenericNames = ["HANDOFF", "HANDOVER", "SESSION-STATUS", "NOTES"];

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

        return Discover(workstream) ?? Path.Combine(RootOf(workstream), Proposed(workstream));
    }

    /// <summary>
    /// What to call notes nobody has written yet: <c>HANDOFF-slug.md</c>, named for the workstream
    /// and for nothing else.
    ///
    /// Undated, deliberately. A date in the name makes the path a different string every morning, so
    /// the file a session was told to write on Tuesday is not the file the next one is sent to read
    /// on Wednesday - which reads, from inside that session, as no notes having been left at all.
    /// One workstream keeps one document and revises it; the dates belong in what it says.
    ///
    /// The workstream's own name is in there so that several sharing a directory stay distinct.
    /// </summary>
    private static string Proposed(Workstream workstream) => $"HANDOFF-{workstream.Key}.md";

    /// <summary>
    /// The handover document the sessions have actually been writing, if one is already on disk.
    /// Sessions leave their notes wherever the work lives rather than where Theseus would have put
    /// them, so the conventional name is the last resort and not the first guess.
    /// </summary>
    public static string? Discover(Workstream workstream)
    {
        ArgumentNullException.ThrowIfNull(workstream);

        var candidates = Roots(workstream)
            .SelectMany(SearchedDirectories)
            .SelectMany(CandidatesIn)
            .DistinctBy(file => file.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var named = candidates
            .Where(file => Names(file.Name, workstream))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ToList();

        if (named.Count > 0)
        {
            return named[0].FullName;
        }

        // A lone candidate is adopted only if its name belongs to nobody in particular.
        //
        // "One candidate is unambiguous" was the original rule and it is false: on a machine where
        // Theseus is new, the first workstream writes HANDOFF-first.md and that file is then the only
        // candidate every later workstream finds. Nigel hit exactly that on a second computer -
        // every workstream he created was handed the first one's notes. It never showed here because
        // this machine's working directory holds dozens of them, so the count is never one.
        //
        // A file already carrying somebody's name is somebody's. Only a bare HANDOFF.md, NOTES.md or
        // the like is a document this workstream can reasonably be said to have been keeping.
        return candidates.Count == 1 && !CarriesAName(candidates[0].Name)
            ? candidates[0].FullName
            : null;
    }

    /// <summary>
    /// Whether a file name distinguishes one workstream from another — <c>HANDOFF-gdpr-mcp.md</c>
    /// does, <c>HANDOFF.md</c> does not. Reached only once the name is known not to be this
    /// workstream's, so a name that belongs to anybody belongs to somebody else.
    /// </summary>
    private static bool CarriesAName(string fileName) =>
        !GenericNames.Contains(Path.GetFileNameWithoutExtension(fileName), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether a file is this workstream's notes rather than notes that merely lie nearby.
    ///
    /// Workstreams routinely share a working directory, so it holds one of these per workstream, and
    /// the most recently written of them belongs to whichever workstream is busiest rather than to
    /// the one asking. Handing a session another workstream's handoff sets it to work on the wrong
    /// thing, so where several notes could be meant, only one that names this workstream will do.
    /// A single candidate is unambiguous and needs no name.
    /// </summary>
    private static bool Names(string fileName, Workstream workstream) =>
        Mentions(fileName, workstream.Key) || Mentions(fileName, Slug(workstream.Title));

    private static string Slug(string? title) =>
        string.IsNullOrWhiteSpace(title)
            ? string.Empty
            : string.Concat(title.Select(character => char.IsLetterOrDigit(character) ? character : '-'));

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
        !string.IsNullOrWhiteSpace(key) && fileName.Contains(key, StringComparison.OrdinalIgnoreCase);
}
