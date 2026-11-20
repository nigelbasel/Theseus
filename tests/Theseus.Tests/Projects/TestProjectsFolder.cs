namespace Theseus.Tests.Projects;

/// <summary>A projects folder in the temp folder, for testing what Theseus reads from project homes as plain files.</summary>
internal sealed class TestProjectsFolder : IDisposable
{
    private TestProjectsFolder(string folder) => Folder = folder;

    /// <summary>The projects folder.</summary>
    public string Folder { get; }

    /// <summary>Creates an empty projects folder.</summary>
    public static TestProjectsFolder Create()
    {
        var folder = Path.Combine(Path.GetTempPath(), "theseus-projects-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        return new TestProjectsFolder(folder);
    }

    /// <summary>The text of a project file in the current shape.</summary>
    public static string ProjectText(string title, string primary = "owner/project", string kit = "method-v0.2", string body = "",
        string header = "") =>
        $"# {title}\n\n**Kit:** {kit}\n**Primary branch:** {primary}\n{header}\n{body}";

    /// <summary>A project body with one feature objective serving the given slugs.</summary>
    public static string OneObjective(params string[] slugs) =>
        "## Phase 0: Start\n\n### Objective 0.1: Something\n- [ ] A criterion.\n\n**Pipeline:** feature\n"
        + "| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n"
        + string.Concat(slugs.Select(slug => $"| A workstream | {slug} | owner/{slug} | a-repo |\n"));

    /// <summary>The folder of a project home.</summary>
    public string Home(string project) => Path.Combine(Folder, project);

    /// <summary>Writes a file under the projects folder, making its folders.</summary>
    public string Write(string path, string text)
    {
        var full = Path.Combine(Folder, path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);

        return full;
    }

    public void Dispose()
    {
        if (Directory.Exists(Folder))
        {
            Directory.Delete(Folder, recursive: true);
        }
    }
}
