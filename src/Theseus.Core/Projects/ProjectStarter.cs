using System.Reflection;

namespace Theseus.Core.Projects;

/// <summary>
/// Starts a project (1.1b AC 7 and 8): a new home folder in the projects folder holding the kit's starter
/// <c>project.md</c>, its title filled in, and an empty <c>work</c> folder. The starter ships inside Theseus, built from
/// the repo's <c>method/starter/</c>, so the copy is always the kit this Theseus reads.
/// </summary>
public static class ProjectStarter
{
    private const string StarterResource = "Theseus.Core.Starter.project.md";
    private const string TitlePlaceholder = "<Project title>";
    private const string ObjectivePlaceholder = "One paragraph: the outcome the whole project exists for.";
    private const string PrimaryPlaceholder = "<user>/<project>";

    /// <summary>The starter project file's text, as shipped.</summary>
    public static string StarterText()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(StarterResource)
                           ?? throw new InvalidOperationException($"The starter {StarterResource} is not in this build");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }

    /// <summary>Why no project can be started in the folder at all, or null when one can.</summary>
    public static string? FolderRefusal(string? projectsFolder)
    {
        if (string.IsNullOrWhiteSpace(projectsFolder))
        {
            return "Set the projects folder in Tools > Options first.";
        }

        return Directory.Exists(projectsFolder) ? null : $"The projects folder {projectsFolder} does not exist.";
    }

    /// <summary>Why a project of this name cannot be started in the folder, or null when it can.</summary>
    public static string? Refusal(string? projectsFolder, string? name)
    {
        if (FolderRefusal(projectsFolder) is { } refusal)
        {
            return refusal;
        }

        if (!ProjectMembership.IsSlug(name))
        {
            return "A project's name is its folder's name and its identity on the board: lower-case letters, digits and hyphens, such as my-project.";
        }

        if (ReservedNames.Contains(name!))
        {
            return $"{name} is a name Windows keeps for a device, so it cannot name a folder.";
        }

        var home = Path.Combine(projectsFolder!, name!);

        return Directory.Exists(home) || File.Exists(home)
            ? $"{name} already exists in the projects folder."
            : null;
    }

    /// <summary>Makes the project's home folder from the starter and returns it. Call <see cref="Refusal"/> first.</summary>
    public static string Start(string projectsFolder, string name) =>
        Start(projectsFolder, new NewProjectDetails(name, string.Empty, string.Empty, string.Empty));

    /// <summary>
    /// Makes the project's home folder from the starter with its title, objective and primary branch filled in, and
    /// returns it; what is left empty keeps the starter's prompt. Call <see cref="Refusal"/> first.
    /// </summary>
    public static string Start(string projectsFolder, NewProjectDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);

        var name = details.Name;

        if (Refusal(projectsFolder, name) is { } refusal)
        {
            throw new InvalidOperationException(refusal);
        }

        var home = Path.Combine(projectsFolder, name);
        var work = Path.Combine(home, WorkstreamArtefactReader.WorkFolder);

        Directory.CreateDirectory(work);

        try
        {
            // CreateNew, so a project made by someone else since the check is never overwritten.
            using var file = new FileStream(Path.Combine(home, ProjectCatalog.ProjectFileName), FileMode.CreateNew, FileAccess.Write);
            using var writer = new StreamWriter(file);
            writer.Write(Filled(details));
        }
        catch (IOException)
        {
            // A half-made home would refuse the retry as existing; only the empty folders made here are taken away.
            if (!Directory.EnumerateFileSystemEntries(work).Any() && Directory.EnumerateFileSystemEntries(home).Count() == 1)
            {
                Directory.Delete(work);
                Directory.Delete(home);
            }

            throw;
        }

        return home;
    }

    /// <summary>The starter's text with the details filled in, each on one line as the shape needs.</summary>
    public static string Filled(NewProjectDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);

        static string OneLine(string text) => string.Join(' ', text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)).Trim();

        var text = StarterText().Replace(TitlePlaceholder, OneLine(details.Title) is { Length: > 0 } title ? title : details.Name, StringComparison.Ordinal);

        if (OneLine(details.Objective) is { Length: > 0 } objective)
        {
            text = text.Replace(ObjectivePlaceholder, objective, StringComparison.Ordinal);
        }

        if (OneLine(details.PrimaryBranch) is { Length: > 0 } primary)
        {
            text = text.Replace(PrimaryPlaceholder, primary, StringComparison.Ordinal);
        }

        return text;
    }

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "con", "prn", "aux", "nul",
        "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9",
        "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9",
    };
}
