using System.Diagnostics;
using System.Text.RegularExpressions;
using Theseus.Core.Diagnostics;

namespace Theseus.Core.Links;

/// <summary>
/// Where a project's holdout scenarios are (1.1e AC 6c, 6e; Nigel, 2026-11-22: "use convention to come up with a default
/// so only a user who really understands the implications would change it"), found in this order: a folder the owner
/// chose for the project in its Project properties; the holdout repository its project.md records, beside the home repo's
/// clone; a <c>&lt;repo&gt;-holdout</c> folder beside that clone; otherwise Theseus's own,
/// <c>%LOCALAPPDATA%\Theseus\holdouts\&lt;project folder name&gt;\</c>, created on first use as a local git repository.
/// There is no folder for every project: one reached projects nothing on screen named (options-made-clear).
/// </summary>
/// <param name="Folder">The folder.</param>
/// <param name="IsDefault">Whether it is Theseus's own, not one the owner chose or one beside the repo.</param>
public sealed partial record HoldoutLocation(string Folder, bool IsDefault)
{
    /// <summary>The default folder of a card in no project.</summary>
    public const string AdHocFolder = "ad-hoc";

    /// <summary>How it was found.</summary>
    public HoldoutSource Source { get; init; } = HoldoutSource.TheseusOwn;

    /// <summary>The folder every default holdout folder sits in, which the holdout guard covers whole.</summary>
    public static string DefaultRoot(string? localAppData = null) =>
        Path.Combine(localAppData ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Theseus", "holdouts");

    /// <summary>The holdout folder for a project's cards, in the order above.</summary>
    /// <param name="project">The project's folder name, or null for a card in no project.</param>
    /// <param name="chosenForProject">The folder the owner chose for this project, or null.</param>
    /// <param name="convention">The project's home clone and recorded holdout repository, or null for none.</param>
    /// <param name="localAppData">Where Theseus's own goes, for tests; null for the user's local application data.</param>
    public static HoldoutLocation Find(string? project, string? chosenForProject, HoldoutConvention? convention,
        string? localAppData = null)
    {
        var own = new HoldoutLocation(Path.Combine(DefaultRoot(localAppData), string.IsNullOrWhiteSpace(project) ? AdHocFolder : project), true);

        if (string.IsNullOrWhiteSpace(project))
        {
            return own;
        }

        if (FolderPath(chosenForProject) is { } forProject)
        {
            return new HoldoutLocation(forProject, false) { Source = HoldoutSource.Chosen };
        }

        // A recorded repository not found is missing, never Theseus's own: Verify would judge against an empty folder
        // (Nigel, accepting the spec, 2026-11-23).
        if (convention?.Recorded is not null)
        {
            return new HoldoutLocation(convention.RecordedFolder ?? string.Empty, false) { Source = HoldoutSource.Recorded };
        }

        return convention?.BesideRepo is { } beside
            ? new HoldoutLocation(beside, false) { Source = HoldoutSource.BesideRepo }
            : own;
    }

    /// <summary>
    /// The folder a chosen setting names, as the holdout guard reads it, or null: a full path with a drive or share, a
    /// Git Bash or WSL form standing for one, never a whole drive. Any other the guard counts as unset, so the board does
    /// not verify against it either (re-verify C1 and C2; review of verifier-runs, M1).
    /// </summary>
    private static string? FolderPath(string? typed)
    {
        if (string.IsNullOrWhiteSpace(typed))
        {
            return null;
        }

        var trimmed = typed.Trim();
        var written = GitBashPath().Match(trimmed) is { Success: true } bash
            ? $"{bash.Groups["drive"].Value.ToUpperInvariant()}:\\{bash.Groups["rest"].Value.Replace('/', '\\')}"
            : trimmed;

        if (!Path.IsPathFullyQualified(written))
        {
            return null;
        }

        var full = Path.GetFullPath(written);

        return string.Equals(Path.TrimEndingDirectorySeparator(full), Path.TrimEndingDirectorySeparator(Path.GetPathRoot(full) ?? string.Empty),
            StringComparison.OrdinalIgnoreCase)
            ? null
            : written;
    }

    [GeneratedRegex(@"^/(?:mnt/)?(?<drive>[a-zA-Z])(?:/(?<rest>.*))?$")]
    private static partial Regex GitBashPath();

    /// <summary>
    /// Whether a folder the owner chose, or the recorded repository's clone, is not there: the way on is to clone it or to
    /// choose a folder in Project properties.
    /// </summary>
    public bool IsMissing => Source is HoldoutSource.Chosen or HoldoutSource.Recorded
                             && (Folder.Length == 0 || !Directory.Exists(Folder));

    /// <summary>
    /// Whether it holds any scenario: a file outside the verifiers' results folder and the repository's own folder.
    /// </summary>
    /// <remarks>Asked whenever WPF asks whether Verify can run, so it stops at the first file and never walks .git.</remarks>
    public bool HasScenarios =>
        Directory.Exists(Folder)
        && (Directory.EnumerateFiles(Folder).Any(file => !Path.GetFileName(file).StartsWith(".git", StringComparison.OrdinalIgnoreCase))
            || Directory.EnumerateDirectories(Folder)
                .Where(directory => Path.GetFileName(directory) is var name
                                    && !string.Equals(name, VerifierCard.ResultsFolder, StringComparison.OrdinalIgnoreCase)
                                    && !name.StartsWith(".git", StringComparison.OrdinalIgnoreCase))
                .Any(directory => Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Any()));

    /// <summary>
    /// The work its scenarios check, from their Checks lines (A2): every file outside the results folder and the
    /// repository's own, not too large to be a scenario. Read once per refresh, not on every query.
    /// </summary>
    public IReadOnlySet<string> CheckedWork()
    {
        const long LargestScenario = 1024 * 1024;
        var named = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in ScenarioFiles().Where(file => file.Length <= LargestScenario))
        {
            // One locked or vanished file leaves the others' names standing (review of the auto-start).
            try
            {
                named.UnionWith(ScenarioChecks.NamesIn(File.ReadAllText(file.FullName)));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                TheseusLog.Warning("Could not read the holdout scenario {File} for its Checks line: {Error}", file.FullName, exception.Message);
            }
        }

        return named;
    }

    /// <summary>
    /// What changes when its scenarios do: their count, and the newest write among them. Cheap beside reading them, so the
    /// Checks lines are read again only when this changes (review of the auto-start).
    /// </summary>
    public string ScenariosStamp()
    {
        var files = ScenarioFiles().ToList();

        return $"{files.Count}:{(files.Count == 0 ? 0 : files.Max(file => file.LastWriteTimeUtc.Ticks))}";
    }

    private IEnumerable<FileInfo> ScenarioFiles()
    {
        if (!Directory.Exists(Folder))
        {
            return [];
        }

        var top = Directory.EnumerateFiles(Folder).Where(file => !Path.GetFileName(file).StartsWith(".git", StringComparison.OrdinalIgnoreCase));
        var below = Directory.EnumerateDirectories(Folder)
            .Where(directory => Path.GetFileName(directory) is var name
                                && !string.Equals(name, VerifierCard.ResultsFolder, StringComparison.OrdinalIgnoreCase)
                                && !name.StartsWith(".git", StringComparison.OrdinalIgnoreCase))
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories));

        return top.Concat(below).Select(file => new FileInfo(file));
    }

    /// <summary>
    /// Creates Theseus's own folder on first use and makes it a local git repository with a first commit, so scenarios keep
    /// a history without a remote (1.1e AC 6e). A folder the owner chose, or one beside the repo, is never created or
    /// changed. Whether it is a git repository now.
    /// </summary>
    public bool Ensure()
    {
        if (!IsDefault)
        {
            return Directory.Exists(Path.Combine(Folder, ".git"));
        }

        Directory.CreateDirectory(Folder);

        // Made means it has a first commit, not merely a .git folder, so a commit that failed once is tried again (review of
        // PR 7, finding 11); signing and hooks of the owner's are not asked for an empty first commit.
        if (Git("rev-parse", "--verify", "--quiet", "HEAD"))
        {
            return true;
        }

        return (Directory.Exists(Path.Combine(Folder, ".git")) || Git("init", "--quiet"))
               && Git("-c", "user.name=Theseus", "-c", "user.email=theseus@localhost", "-c", "commit.gpgsign=false", "-c", "core.hooksPath=",
                   "commit", "--quiet", "--allow-empty", "-m", "Holdout scenarios");
    }

    /// <summary>
    /// Keep these in a private repository… (1.1e AC 6e): adds the owner's remote to Theseus's own folder, made first if need
    /// be. Why it could not, or null.
    /// </summary>
    public string? AddRemote(string address)
    {
        if (!IsDefault)
        {
            return "This folder is not Theseus's own; add a remote to it yourself.";
        }

        if (string.IsNullOrWhiteSpace(address) || address.Trim().StartsWith('-'))
        {
            return "Give the repository's address, such as https://host/owner/repo.git.";
        }

        if (!Ensure())
        {
            return $"{Folder} could not be made a git repository: is git installed?";
        }

        return Git("remote", "add", "origin", address.Trim()) || Git("remote", "set-url", "origin", address.Trim())
            ? null
            : $"The remote could not be added to {Folder}.";
    }

    private bool Git(params string[] arguments)
    {
        try
        {
            var start = new ProcessStartInfo("git")
            {
                WorkingDirectory = Folder,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            foreach (var argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }

            using var git = Process.Start(start);

            if (git is null)
            {
                return false;
            }

            if (!git.WaitForExit(30_000))
            {
                git.Kill(entireProcessTree: true);

                return false;
            }

            return git.ExitCode == 0;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            TheseusLog.Warning(exception, "The holdout folder {Folder} could not be made a git repository", Folder);

            return false;
        }
    }
}
