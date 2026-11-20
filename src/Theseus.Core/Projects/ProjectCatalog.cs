using Theseus.Core.Diagnostics;

namespace Theseus.Core.Projects;

/// <summary>
/// Reads the projects in the repositories listed in Settings (spec AC 1 and 2), always from each project's primary
/// branch, so it does not matter which branch the folder has checked out. The primary is the one remembered from an
/// earlier read while that branch is still here; otherwise it is found on the default branch's copy of the file, then on
/// the checked-out branch's. A folder or file that cannot be read is listed as unreadable with its reason, never left out.
/// </summary>
public static class ProjectCatalog
{
    /// <summary>The folder project files live in, in a project's home repo.</summary>
    public const string ProjectsFolder = "projects";

    /// <summary>Reads every listed folder. A folder listed twice is read once.</summary>
    /// <param name="folders">The listed folders.</param>
    /// <param name="git">Runs git.</param>
    /// <param name="primaries">Where primaries found are remembered, or null to find them afresh each time.</param>
    public static IReadOnlyList<ProjectFileReading> Read(IReadOnlyList<string> folders, IGitReader git, ProjectPrimaryStore? primaries = null)
    {
        ArgumentNullException.ThrowIfNull(folders);
        ArgumentNullException.ThrowIfNull(git);

        return [.. folders
            .Select(folder => folder.Trim())
            .Where(folder => folder.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .SelectMany(folder => ReadFolderSafely(new GitRepositoryReader(git, folder), primaries))];
    }

    // Reading runs on every refresh of the board; nothing it meets in a repository may take the board down with it.
    private static IReadOnlyList<ProjectFileReading> ReadFolderSafely(GitRepositoryReader repository, ProjectPrimaryStore? primaries)
    {
        try
        {
            return [.. ReadFolder(repository, primaries)];
        }
        catch (Exception exception)
        {
            TheseusLog.Error(exception, "Reading the projects in {Folder} failed", repository.Folder);

            return [ProjectFileReading.Unreadable(FolderName(repository), $"could not be read: {exception.Message}")
                with { Folder = repository.Folder }];
        }
    }

    private static string FolderName(GitRepositoryReader repository) => Path.GetFileName(repository.Folder.TrimEnd('\\', '/'));

    private static IEnumerable<ProjectFileReading> ReadFolder(GitRepositoryReader repository, ProjectPrimaryStore? primaries)
    {
        var folderName = FolderName(repository);

        if (repository.Problem() is { } problem)
        {
            return [ProjectFileReading.Unreadable(folderName, problem) with { Folder = repository.Folder }];
        }

        var remembered = (primaries?.For(repository.Folder) ?? [])
            .Where(entry => repository.HasBranch(entry.Branch))
            .ToDictionary(entry => entry.Project, entry => entry.Branch, StringComparer.OrdinalIgnoreCase);

        // The default branch first, as the spec says. The checked-out branch is the fallback for a project whose file
        // has not reached the default branch in this shape yet, which is every project until its primary is merged.
        var discovery = new[] { repository.DefaultBranch(), repository.CurrentBranch() }
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (discovery.Count == 0 && remembered.Count == 0)
        {
            return [ProjectFileReading.Unreadable(folderName, "no default branch (origin's HEAD, main or master) and no branch checked out")
                with { Folder = repository.Folder }];
        }

        var names = discovery
            .Concat(remembered.Values)
            .SelectMany(branch => repository.Names(branch, ProjectsFolder))
            .Where(name => name.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (names.Count == 0)
        {
            return [ProjectFileReading.Unreadable(folderName, $"no project files in {ProjectsFolder}/ on {discovery.FirstOrDefault() ?? remembered.Values.First()}")
                with { Folder = repository.Folder }];
        }

        return names.Select(name => ReadProject(repository, discovery, remembered, primaries, name) with { Folder = repository.Folder });
    }

    private static ProjectFileReading ReadProject(GitRepositoryReader repository, IReadOnlyList<string> discovery,
        IReadOnlyDictionary<string, string> remembered, ProjectPrimaryStore? primaries, string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        var path = $"{ProjectsFolder}/{fileName}";

        if (remembered.TryGetValue(name, out var known) && repository.Text(known, path) is { } knownText)
        {
            var renamed = ProjectFileReader.Header(knownText).PrimaryBranch;

            if (renamed is null || string.Equals(renamed, known, StringComparison.Ordinal))
            {
                return FromPrimary(name, known, knownText);
            }

            // The primary's own file has moved the project to another branch: follow it when that branch is here.
            if (repository.HasBranch(renamed) && repository.Text(renamed, path) is { } movedText)
            {
                primaries?.Remember(repository.Folder, name, renamed);

                return FromPrimary(name, renamed, movedText);
            }
        }

        var copies = discovery
            .Select(branch => (Branch: branch, Text: repository.Text(branch, path)))
            .Where(copy => copy.Text is not null)
            .ToList();

        if (copies.Count == 0)
        {
            return ProjectFileReading.Unreadable(name, $"{path} could not be read on any branch");
        }

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

        primaries?.Remember(repository.Folder, name, primary);

        return FromPrimary(name, primary, primaryText);
    }

    private static ProjectFileReading FromPrimary(string name, string primary, string text)
    {
        var reading = ProjectFileReader.Read(name, text);

        return reading.Status == ProjectReadingStatus.Unreadable
            ? reading with { Reason = $"{reading.Reason} (on {primary})" }
            : reading;
    }
}
