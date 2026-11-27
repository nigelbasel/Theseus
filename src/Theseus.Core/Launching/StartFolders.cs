using System.Text;
using Theseus.Core.Domain;
using Theseus.Core.Trust;

namespace Theseus.Core.Launching;

/// <summary>
/// Where a card with worktrees starts (objective 1.3f, Nigel 2026-11-27): in its workstream folder,
/// <c>&lt;worktrees&gt;\&lt;slug&gt;</c>, the plain folder that holds its worktrees, with its records folder added. Claude Code
/// asks about an added folder unless it is trusted itself, and a worktree is keyed by its main clone, so adding worktrees
/// made every card ask; a plain folder under the trusted worktrees folder is never asked about, nor are the repositories
/// below it. A worktree is therefore never added. A resume stays in the folder its transcript was recorded under, because
/// Claude Code finds a session by the folder it started in.
/// </summary>
public static class StartFolders
{
    /// <summary>Arranges a launch's folders.</summary>
    /// <param name="workstream">The card as it would launch, its worktrees among its added folders.</param>
    /// <param name="worktreesFolder">The worktrees folder; empty leaves the card as it is.</param>
    /// <param name="resumedUnder">The name of the transcripts folder the resumed session is in, or null for a fresh start.</param>
    /// <param name="claudeProjectsFolder">Claude Code's projects folder, where transcripts and auto-memory are kept.</param>
    public static StartArrangement Arrange(Workstream workstream, string worktreesFolder, string? resumedUnder, string claudeProjectsFolder)
    {
        ArgumentNullException.ThrowIfNull(workstream);

        if (string.IsNullOrWhiteSpace(worktreesFolder) || string.IsNullOrWhiteSpace(workstream.WorkingDirectory)
            || WorkstreamFolder(workstream, worktreesFolder) is not { } start)
        {
            return new StartArrangement(workstream, null);
        }

        var worktrees = workstream.AddDirectories.Where(folder => Within(Full(folder), start)).Select(Full).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var others = workstream.AddDirectories.Where(folder => !Within(Full(folder), start)).ToList();
        // A resume starts in the workstream folder only if it was recorded there; any other, such as a session started
        // before this rule, stays in the card's own folder and adds the workstream folder, so its conversation is found.
        if (resumedUnder is not null && !string.Equals(resumedUnder, ProjectFolderName(start), StringComparison.OrdinalIgnoreCase))
        {
            return new StartArrangement(
                workstream with { AddDirectories = [start, .. others], StartInstruction = Instructed(workstream, Told(workstream.WorkingDirectory, start, worktrees)) },
                null);
        }

        var added = new[] { workstream.WorkingDirectory }.Concat(others)
            .Where(folder => !string.IsNullOrWhiteSpace(folder))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new StartArrangement(
            workstream with { WorkingDirectory = start, AddDirectories = added, StartInstruction = Instructed(workstream, Told(start, start, worktrees)) },
            MemoryOf(workstream.WorkingDirectory, claudeProjectsFolder));
    }

    /// <summary>
    /// The name of the folder Claude Code keeps a start folder's transcripts in, under its projects folder: every character
    /// that is not a letter or a digit becomes a hyphen.
    /// </summary>
    public static string ProjectFolderName(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        var full = Full(folder);
        var name = new StringBuilder(full.Length);

        foreach (var character in full)
        {
            name.Append(char.IsAsciiLetterOrDigit(character) ? character : '-');
        }

        return name.ToString();
    }

    // The single workstream folder all the card's worktrees are in, when it is a plain folder that exists.
    private static string? WorkstreamFolder(Workstream workstream, string worktreesFolder)
    {
        var root = Full(worktreesFolder);
        var folders = workstream.AddDirectories
            .Select(Full)
            .Select(folder => Path.GetDirectoryName(folder) is { } parent && string.Equals(Path.GetDirectoryName(parent), root, StringComparison.OrdinalIgnoreCase) ? parent : null)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (folders is not [var start] || !Directory.Exists(start) || RepositoryRoot.WorkingTreeOf(start) is not null
            || Within(Full(workstream.WorkingDirectory), start))
        {
            return null;
        }

        return start;
    }

    private static string Instructed(Workstream workstream, string told) =>
        string.IsNullOrWhiteSpace(workstream.StartInstruction) ? told : $"{workstream.StartInstruction}{Environment.NewLine}{Environment.NewLine}{told}";

    private static string Told(string startsIn, string holder, IReadOnlyList<string> worktrees)
    {
        var text = new StringBuilder();
        text.AppendLine(string.Equals(startsIn, holder, StringComparison.OrdinalIgnoreCase)
            ? $"This session starts in {holder}, the folder that holds your repositories:"
            : $"This session starts in {startsIn}; your repositories are in {holder}:");

        foreach (var worktree in worktrees)
        {
            text.AppendLine($"- {worktree}");
        }

        text.Append("Their instructions are not loaded for you from here: before working in a repository, read its CLAUDE.md and AGENTS.md.");

        return text.ToString();
    }

    // Claude Code keys auto-memory by the repository a folder is in, else the folder; a name it would shorten is left alone.
    private static string? MemoryOf(string recordsFolder, string claudeProjectsFolder)
    {
        if (string.IsNullOrWhiteSpace(recordsFolder) || string.IsNullOrWhiteSpace(claudeProjectsFolder))
        {
            return null;
        }

        var name = ProjectFolderName(RepositoryRoot.MainOf(recordsFolder) ?? recordsFolder);

        return name.Length > 200 ? null : Path.Combine(claudeProjectsFolder, name, "memory");
    }

    private static string Full(string path)
    {
        var full = Path.GetFullPath(path.Replace('/', Path.DirectorySeparatorChar));

        return string.Equals(Path.GetPathRoot(full), full, StringComparison.OrdinalIgnoreCase) ? full : Path.TrimEndingDirectorySeparator(full);
    }

    private static bool Within(string path, string folder) =>
        string.Equals(path, folder, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
