namespace Theseus.Core.Instructions;

/// <summary>
/// Every instruction file a session started in a directory would read, in the order it reads them.
///
/// Nothing here overrides anything else: Claude Code concatenates every file it finds, broadest
/// first, so a file nearer the working directory is simply read later. What decides a conflict is
/// the model reading both, which is exactly why it is worth seeing them in order. The rules followed
/// are Claude Code's own documentation as of 2026-09-15 (code.claude.com/docs/en/memory):
///
/// - managed policy, then <c>~/.claude/CLAUDE.md</c>, then <c>~/.claude/rules</c>;
/// - then every directory from the filesystem root down to the working directory, each with its
///   <c>CLAUDE.md</c>, <c>.claude/CLAUDE.md</c> and, straight after, <c>CLAUDE.local.md</c>;
/// - then the working directory's <c>.claude/rules</c>, where a rule with <c>paths</c> waits for a
///   matching file;
/// - imports are expanded where they are written, up to four hops, never inside code;
/// - CLAUDE.md files below the working directory are read only when files beside them are;
/// - an added directory's are not read at all unless <c>CLAUDE_CODE_ADDITIONAL_DIRECTORIES_CLAUDE_MD=1</c>.
///
/// <c>claudeMdExcludes</c> settings are not applied, so an excluded file still shows.
/// </summary>
public static class InstructionScan
{
    /// <summary>How many imports deep Claude Code follows.</summary>
    public const int MaxImportDepth = 4;

    /// <summary>Set to 1 to make Claude Code read instruction files in added directories.</summary>
    public const string AdditionalDirectoriesVariable = "CLAUDE_CODE_ADDITIONAL_DIRECTORIES_CLAUDE_MD";

    /// <summary>How far below the working directory to look for files read on demand.</summary>
    public const int SubdirectoryDepth = 3;

    /// <summary>The files, in reading order.</summary>
    /// <param name="workingDirectory">Where the session starts.</param>
    /// <param name="additionalDirectories">The directories added to the session with <c>--add-dir</c>.</param>
    /// <param name="userClaudeDirectory">The user's <c>.claude</c> directory; its parent is <c>~</c>.</param>
    /// <param name="managedFile">The managed policy CLAUDE.md for this machine.</param>
    /// <param name="additionalDirectoriesLoad">Whether sessions are started with added directories' files enabled.</param>
    public static IReadOnlyList<InstructionFile> For(
        string workingDirectory,
        IReadOnlyList<string> additionalDirectories,
        string userClaudeDirectory,
        string managedFile,
        bool additionalDirectoriesLoad)
    {
        ArgumentNullException.ThrowIfNull(additionalDirectories);
        ArgumentNullException.ThrowIfNull(userClaudeDirectory);

        var home = Path.GetDirectoryName(Path.GetFullPath(userClaudeDirectory)) ?? userClaudeDirectory;
        var scan = new Collector(home);

        scan.Add(managedFile, InstructionScope.Managed, InstructionLoad.AtStart,
            "At start, for everyone on this machine. No setting can exclude it.");
        scan.Add(Path.Combine(userClaudeDirectory, "CLAUDE.md"), InstructionScope.User, InstructionLoad.AtStart,
            "At start, in every project.");
        scan.Rules(Path.Combine(userClaudeDirectory, "rules"), InstructionScope.UserRule, InstructionLoad.AtStart,
            "At start, in every project, before any project's own rules.");

        if (!string.IsNullOrWhiteSpace(workingDirectory) && Directory.Exists(workingDirectory))
        {
            var chain = DirectoryWalk.RootDownTo(workingDirectory);

            foreach (var directory in chain)
            {
                var isHere = ReferenceEquals(directory, chain[^1]);
                var scope = isHere ? InstructionScope.Project : InstructionScope.Ancestor;
                var when = isHere
                    ? "At start, after everything above it."
                    : "At start, before the files nearer the working directory.";

                scan.Add(Path.Combine(directory, "CLAUDE.md"), scope, InstructionLoad.AtStart, when);
                scan.Add(Path.Combine(directory, ".claude", "CLAUDE.md"), scope, InstructionLoad.AtStart, when);
                scan.Add(Path.Combine(directory, "CLAUDE.local.md"), InstructionScope.Local, InstructionLoad.AtStart,
                    "At start, straight after the CLAUDE.md beside it.");
            }

            scan.Rules(Path.Combine(workingDirectory, ".claude", "rules"), InstructionScope.ProjectRule,
                InstructionLoad.AtStart, "At start, alongside .claude/CLAUDE.md.");

            foreach (var (directory, relative) in DirectoryWalk.Below(workingDirectory, SubdirectoryDepth))
            {
                var when = $"When files under {relative} are read.";

                scan.Add(Path.Combine(directory, "CLAUDE.md"), InstructionScope.Subdirectory, InstructionLoad.OnDemand, when);
                scan.Add(Path.Combine(directory, "CLAUDE.local.md"), InstructionScope.Subdirectory, InstructionLoad.OnDemand, when);
            }
        }

        foreach (var directory in additionalDirectories.Where(directory => !string.IsNullOrWhiteSpace(directory)))
        {
            var load = additionalDirectoriesLoad ? InstructionLoad.AtStart : InstructionLoad.NotLoaded;
            var when = additionalDirectoriesLoad
                ? "At start, from a directory added to the session."
                : $"Not read. An added directory's instructions load only when {AdditionalDirectoriesVariable}=1.";

            scan.Add(Path.Combine(directory, "CLAUDE.md"), InstructionScope.AdditionalDirectory, load, when);
            scan.Add(Path.Combine(directory, ".claude", "CLAUDE.md"), InstructionScope.AdditionalDirectory, load, when);
            scan.Rules(Path.Combine(directory, ".claude", "rules"), InstructionScope.AdditionalDirectory, load, when);
            scan.Add(Path.Combine(directory, "CLAUDE.local.md"), InstructionScope.AdditionalDirectory, load, when);
        }

        return scan.Files;
    }

    private sealed class Collector(string home)
    {
        private readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);

        public List<InstructionFile> Files { get; } = [];

        public void Add(string path, InstructionScope scope, InstructionLoad load, string when, int depth = 0)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }

            var full = Path.GetFullPath(path);

            if (!_seen.Add(full))
            {
                return;
            }

            Files.Add(new InstructionFile(full, scope, load, depth, when));

            if (depth >= MaxImportDepth)
            {
                return;
            }

            foreach (var reference in ImportReferences.In(DirectoryWalk.Text(full)))
            {
                if (ImportReferences.Resolve(reference, full, home) is { } imported)
                {
                    Add(imported, InstructionScope.Import, load,
                        $"Wherever {Path.GetFileName(full)} is read, because it imports this.", depth + 1);
                }
            }
        }

        public void Rules(string directory, InstructionScope scope, InstructionLoad load, string when)
        {
            foreach (var rule in DirectoryWalk.Files(directory, "*.md", recursive: true))
            {
                var paths = Frontmatter.Read(DirectoryWalk.Text(rule)).GetValueOrDefault("paths");

                if (string.IsNullOrWhiteSpace(paths) || load == InstructionLoad.NotLoaded)
                {
                    Add(rule, scope, load, when);
                }
                else
                {
                    Add(rule, scope, InstructionLoad.OnDemand, $"When files matching {paths} are read.");
                }
            }
        }
    }
}
