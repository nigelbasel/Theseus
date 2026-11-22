using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Theseus.Core.Diagnostics;

namespace Theseus.Core.Links;

/// <summary>
/// Where a project's holdout scenarios are (1.1e AC 6c, 6e; Nigel, 2026-11-22: "use convention to come up with a default
/// so only a user who really understands the implications would change it"), found in this order: a folder the owner
/// chose for the project in Options, or the old single setting for every project; a <c>&lt;repo&gt;-holdout</c> folder
/// beside the clone of the project's home code repo; otherwise Theseus's own,
/// <c>%LOCALAPPDATA%\Theseus\holdouts\&lt;project folder name&gt;\</c>, created on first use as a local git repository.
/// </summary>
/// <param name="Folder">The folder.</param>
/// <param name="IsDefault">Whether it is Theseus's own, not one the owner chose or one beside the repo.</param>
public sealed record HoldoutLocation(string Folder, bool IsDefault)
{
    /// <summary>The default folder of a card in no project.</summary>
    public const string AdHocFolder = "ad-hoc";

    /// <summary>How it was found.</summary>
    public HoldoutSource Source { get; init; } = HoldoutSource.TheseusOwn;

    /// <summary>The folder every default holdout folder sits in, which the holdout guard covers whole.</summary>
    public static string DefaultRoot(string? localAppData = null) =>
        Path.Combine(localAppData ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Theseus", "holdouts");

    /// <summary>The holdout folder for a project's cards with only the old single setting (1.1e AC 6c).</summary>
    public static HoldoutLocation For(string? chosen, string? project, string? localAppData = null) =>
        Find(project, null, chosen, null, localAppData);

    /// <summary>The holdout folder for a project's cards, in the order of 1.1e AC 6e.</summary>
    /// <param name="project">The project's folder name, or null for a card in no project.</param>
    /// <param name="chosenForProject">The folder the owner chose for this project in Options, or null.</param>
    /// <param name="chosenForAll">The old single setting, which counts for every project when set.</param>
    /// <param name="besideRepo">A <c>&lt;repo&gt;-holdout</c> folder found beside the home repo's clone, or null.</param>
    /// <param name="localAppData">Where Theseus's own goes, for tests; null for the user's local application data.</param>
    public static HoldoutLocation Find(string? project, string? chosenForProject, string? chosenForAll, string? besideRepo,
        string? localAppData = null)
    {
        if (IsFolderPath(chosenForProject))
        {
            return new HoldoutLocation(chosenForProject.Trim(), false) { Source = HoldoutSource.Chosen };
        }

        if (IsFolderPath(chosenForAll))
        {
            return new HoldoutLocation(chosenForAll.Trim(), false) { Source = HoldoutSource.Chosen };
        }

        if (!string.IsNullOrWhiteSpace(project) && !string.IsNullOrWhiteSpace(besideRepo))
        {
            return new HoldoutLocation(besideRepo, false) { Source = HoldoutSource.BesideRepo };
        }

        return new HoldoutLocation(Path.Combine(DefaultRoot(localAppData), string.IsNullOrWhiteSpace(project) ? AdHocFolder : project), true);
    }

    /// <summary>
    /// Whether a chosen folder names a folder the holdout guard guards: a full path with a drive or share, not a whole
    /// drive. Any other the guard counts as unset, so the board does not verify against it (re-verify C1 and C2).
    /// </summary>
    private static bool IsFolderPath([NotNullWhen(true)] string? typed)
    {
        if (string.IsNullOrWhiteSpace(typed) || !Path.IsPathFullyQualified(typed.Trim()))
        {
            return false;
        }

        var full = Path.GetFullPath(typed.Trim());

        return !string.Equals(Path.TrimEndingDirectorySeparator(full), Path.TrimEndingDirectorySeparator(Path.GetPathRoot(full) ?? string.Empty),
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Whether a folder the owner chose is not there: the way on is to set it again.</summary>
    public bool IsMissing => Source == HoldoutSource.Chosen && !Directory.Exists(Folder);

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
