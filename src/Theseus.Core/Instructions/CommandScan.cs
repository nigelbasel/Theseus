namespace Theseus.Core.Instructions;

/// <summary>
/// Every skill, command and agent a session started in a directory could use, and which one wins
/// when two share a name.
///
/// Unlike instruction files, these do override one another. The rules are Claude Code's own
/// documentation as of 2026-09-15 (code.claude.com/docs/en/skills and /sub-agents):
///
/// - a personal skill beats a project skill of the same name, and any skill beats a command;
/// - a personal or project skill is named after its directory; its front matter only labels it;
/// - project skills are found in the working directory and every directory above it up to the
///   repository root - outside a repository, only the working directory is searched here, because
///   the documentation does not say;
/// - a skill in a <c>.claude</c> below the working directory is qualified by its directory, so it
///   clashes with nothing, and arrives only once files there are worked on;
/// - plugin skills, commands and agents are prefixed with the plugin's name, so they clash with
///   nothing either;
/// - for agents it is the other way round: a project agent beats a personal one.
/// </summary>
public static class CommandScan
{
    /// <summary>How far below the working directory to look for nested skills.</summary>
    public const int NestedDepth = 3;

    /// <summary>The skills, commands and agents, with every one that loses its name marked.</summary>
    public static IReadOnlyList<CommandEntry> For(
        string workingDirectory,
        IReadOnlyList<string> additionalDirectories,
        string userClaudeDirectory,
        IReadOnlyList<InstalledPlugin> plugins)
    {
        ArgumentNullException.ThrowIfNull(additionalDirectories);
        ArgumentNullException.ThrowIfNull(plugins);

        var found = new List<CommandEntry>();
        var personal = Path.GetFullPath(userClaudeDirectory);

        found.AddRange(Skills(Path.Combine(personal, "skills"), CommandOrigin.Personal, string.Empty));
        found.AddRange(Commands(Path.Combine(personal, "commands"), CommandOrigin.Personal, string.Empty));
        found.AddRange(Agents(Path.Combine(personal, "agents"), CommandOrigin.Personal, string.Empty));

        if (!string.IsNullOrWhiteSpace(workingDirectory) && Directory.Exists(workingDirectory))
        {
            foreach (var directory in UpToRepositoryRoot(workingDirectory))
            {
                var claude = Path.Combine(directory, ".claude");

                if (string.Equals(Path.GetFullPath(claude), personal, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                found.AddRange(Skills(Path.Combine(claude, "skills"), CommandOrigin.Project, string.Empty));
                found.AddRange(Commands(Path.Combine(claude, "commands"), CommandOrigin.Project, string.Empty));
                found.AddRange(Agents(Path.Combine(claude, "agents"), CommandOrigin.Project, string.Empty));
            }

            foreach (var (directory, relative) in DirectoryWalk.Below(workingDirectory, NestedDepth))
            {
                found.AddRange(Skills(Path.Combine(directory, ".claude", "skills"), CommandOrigin.Nested,
                    relative.Replace('\\', '/') + ":"));
            }
        }

        foreach (var directory in additionalDirectories.Where(directory => !string.IsNullOrWhiteSpace(directory)))
        {
            found.AddRange(Skills(Path.Combine(directory, ".claude", "skills"), CommandOrigin.AdditionalDirectory, string.Empty));
            found.AddRange(Agents(Path.Combine(directory, ".claude", "agents"), CommandOrigin.AdditionalDirectory, string.Empty));
        }

        foreach (var plugin in plugins)
        {
            var prefix = plugin.Name + ":";

            found.AddRange(Skills(Path.Combine(plugin.InstallPath, "skills"), CommandOrigin.Plugin, prefix));
            found.AddRange(Commands(Path.Combine(plugin.InstallPath, "commands"), CommandOrigin.Plugin, prefix));
            found.AddRange(Agents(Path.Combine(plugin.InstallPath, "agents"), CommandOrigin.Plugin, prefix));
        }

        Hide(found, entry => entry.Kind is CommandKind.Skill or CommandKind.Command
                             && entry.Origin is not (CommandOrigin.Plugin or CommandOrigin.Nested), SlashRank);
        Hide(found, entry => entry.Kind is CommandKind.Agent && entry.Origin is not CommandOrigin.Plugin, AgentRank);

        return found;
    }

    private static IEnumerable<CommandEntry> Skills(string directory, CommandOrigin origin, string prefix)
    {
        foreach (var skill in DirectoryWalk.Children(directory))
        {
            var file = Path.Combine(skill, "SKILL.md");

            if (!File.Exists(file))
            {
                continue;
            }

            var fields = Frontmatter.Read(DirectoryWalk.Text(file));
            var name = origin == CommandOrigin.Plugin && fields.GetValueOrDefault("name") is { Length: > 0 } named
                ? named
                : Path.GetFileName(skill);

            yield return new CommandEntry(CommandKind.Skill, prefix + name, file, origin,
                fields.GetValueOrDefault("description") ?? string.Empty)
            {
                LoadsOnDemand = origin == CommandOrigin.Nested,
            };
        }
    }

    private static IEnumerable<CommandEntry> Commands(string directory, CommandOrigin origin, string prefix) =>
        DirectoryWalk.Files(directory, "*.md", recursive: true).Select(file =>
            new CommandEntry(CommandKind.Command, prefix + Path.GetFileNameWithoutExtension(file), file, origin,
                Frontmatter.Read(DirectoryWalk.Text(file)).GetValueOrDefault("description") ?? string.Empty));

    private static IEnumerable<CommandEntry> Agents(string directory, CommandOrigin origin, string prefix) =>
        DirectoryWalk.Files(directory, "*.md", recursive: true).Select(file =>
        {
            var fields = Frontmatter.Read(DirectoryWalk.Text(file));
            var name = fields.GetValueOrDefault("name") is { Length: > 0 } named
                ? named
                : Path.GetFileNameWithoutExtension(file);

            return new CommandEntry(CommandKind.Agent, prefix + name, file, origin,
                fields.GetValueOrDefault("description") ?? string.Empty);
        });

    private static IReadOnlyList<string> UpToRepositoryRoot(string directory)
    {
        var chain = new List<string>();

        for (var current = new DirectoryInfo(directory); current is not null; current = current.Parent)
        {
            chain.Add(current.FullName);

            var marker = Path.Combine(current.FullName, ".git");

            if (Directory.Exists(marker) || File.Exists(marker))
            {
                return chain;
            }
        }

        return [chain[0]];
    }

    private static void Hide(List<CommandEntry> entries, Func<CommandEntry, bool> competes, Func<CommandEntry, int> rank)
    {
        var contests = entries
            .Select((entry, index) => (Entry: entry, Index: index))
            .Where(candidate => competes(candidate.Entry))
            .GroupBy(candidate => candidate.Entry.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var contest in contests)
        {
            var ordered = contest.OrderBy(candidate => rank(candidate.Entry)).ThenBy(candidate => candidate.Index).ToList();
            var winner = ordered[0].Entry;

            foreach (var (entry, index) in ordered.Skip(1))
            {
                entries[index] = entry with { HiddenBy = winner.Path };
            }
        }
    }

    private static int SlashRank(CommandEntry entry) => (entry.Kind, entry.Origin) switch
    {
        (CommandKind.Skill, CommandOrigin.Personal) => 0,
        (CommandKind.Skill, CommandOrigin.Project) => 1,
        (CommandKind.Skill, _) => 2,
        (CommandKind.Command, CommandOrigin.Personal) => 3,
        _ => 4,
    };

    private static int AgentRank(CommandEntry entry) => entry.Origin switch
    {
        CommandOrigin.Project => 0,
        CommandOrigin.AdditionalDirectory => 1,
        _ => 2,
    };
}
