namespace Theseus.Core.Projects;

/// <summary>
/// Reads the projects in the repositories listed in Settings (spec AC 1 and 2). Each project file is found on the
/// repository's default branch, which names the project's primary branch; the file is then read from the primary
/// branch itself, so it does not matter which branch the folder has checked out. A folder or file that cannot be read
/// is listed as unreadable with its reason, never left out.
/// </summary>
public static class ProjectCatalog
{
    /// <summary>The folder project files live in, in a project's home repo.</summary>
    public const string ProjectsFolder = "projects";

    /// <summary>Reads every listed folder. A folder listed twice is read once.</summary>
    public static IReadOnlyList<ProjectFileReading> Read(IReadOnlyList<string> folders, IGitReader git)
    {
        ArgumentNullException.ThrowIfNull(folders);
        ArgumentNullException.ThrowIfNull(git);

        return [.. folders
            .Select(folder => folder.Trim())
            .Where(folder => folder.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .SelectMany(folder => ReadFolder(new GitRepositoryReader(git, folder)))];
    }

    private static IEnumerable<ProjectFileReading> ReadFolder(GitRepositoryReader repository)
    {
        var folderName = Path.GetFileName(repository.Folder.TrimEnd('\\', '/'));

        if (repository.Problem() is { } problem)
        {
            return [ProjectFileReading.Unreadable(folderName, problem) with { Folder = repository.Folder }];
        }

        // The default branch first, as the spec says. The checked-out branch is the fallback for a project whose file
        // has not reached the default branch in this shape yet, which is every project until its primary is merged.
        var discovery = new[] { repository.DefaultBranch(), repository.CurrentBranch() }
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (discovery.Count == 0)
        {
            return [ProjectFileReading.Unreadable(folderName, "no default branch (origin's HEAD, main or master) and no branch checked out")
                with { Folder = repository.Folder }];
        }

        var names = discovery
            .SelectMany(branch => repository.Names(branch, ProjectsFolder))
            .Where(name => name.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (names.Count == 0)
        {
            return [ProjectFileReading.Unreadable(folderName, $"no project files in {ProjectsFolder}/ on {discovery[0]}")
                with { Folder = repository.Folder }];
        }

        return names.Select(name => ReadProject(repository, discovery, name) with { Folder = repository.Folder });
    }

    private static ProjectFileReading ReadProject(GitRepositoryReader repository, IReadOnlyList<string> discovery, string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        var path = $"{ProjectsFolder}/{fileName}";
        var copies = discovery
            .Select(branch => (Branch: branch, Text: repository.Text(branch, path)))
            .Where(copy => copy.Text is not null)
            .ToList();

        var named = copies
            .Select(copy => (copy.Branch, Header: ProjectFileReader.Header(copy.Text!)))
            .FirstOrDefault(copy => string.Equals(copy.Header.Kit, MethodKits.Current, StringComparison.OrdinalIgnoreCase)
                                    && copy.Header.PrimaryBranch is not null);

        if (named.Header.PrimaryBranch is not { } primary)
        {
            var (branch, text) = copies[0];
            var reading = ProjectFileReader.Read(name, text!);

            return reading.Status == ProjectReadingStatus.Unreadable
                ? reading with { Reason = $"{reading.Reason} (on {branch})" }
                : reading;
        }

        if (!repository.HasBranch(primary))
        {
            return ProjectFileReading.Unreadable(name, $"its primary branch {primary} is not on this machine (named on {named.Branch})");
        }

        if (repository.Text(primary, path) is not { } primaryText)
        {
            return ProjectFileReading.Unreadable(name, $"{path} is not on its primary branch {primary}");
        }

        var fromPrimary = ProjectFileReader.Read(name, primaryText);

        return fromPrimary.Status == ProjectReadingStatus.Unreadable
            ? fromPrimary with { Reason = $"{fromPrimary.Reason} (on {primary})" }
            : fromPrimary;
    }
}
