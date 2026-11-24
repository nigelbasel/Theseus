using System.Text;

namespace Theseus.Tests.Projects;

/// <summary>
/// A project the size of a real one written before any work (project-view-scale, 2026-11-24): eight phases, 35 objectives,
/// no workstreams, and owner notes repeated across criteria and joined with work notes on one line.
/// </summary>
internal static class ScaleFixtures
{
    public const string Large = "large";

    public const int Objectives = 35;

    public const string SharedAsk = "Owner names the agent who runs the holdouts";

    /// <summary>The ask each objective's third criterion makes of the owner alone.</summary>
    public static string OwnAsk(int index) => $"Owner sets the threshold of objective {Number(index)}";

    /// <summary>The objective number of the index'th objective, from 0: five per phase, phases from 0.</summary>
    public static string Number(int index) => $"{index / 5}.{(index % 5) + 1}";

    /// <summary>Writes the large project and reads it as the catalog does.</summary>
    public static Theseus.Core.Projects.ProjectFileReading WriteLarge(TestProjectsFolder folder)
    {
        var body = new StringBuilder();

        for (var index = 0; index < Objectives; index++)
        {
            if (index % 5 == 0)
            {
                body.Append($"## Phase {index / 5}: Phase {index / 5}\n\n");
            }

            body.Append($"### Objective {Number(index)}: Objective {Number(index)}\n")
                .Append("- [ ] A criterion waiting on two things.\n")
                .Append($"      Waiting: owner: {SharedAsk}; work: an audit store (1.3)\n")
                .Append("- [ ] A criterion waiting on the same person for the same thing.\n")
                .Append($"      Waiting: owner: {SharedAsk}.\n")
                .Append("- [ ] A criterion of its own.\n")
                .Append($"      Waiting: owner: {OwnAsk(index)}\n")
                .Append("- [ ] A criterion waiting on work.\n")
                .Append("      Waiting: work: the routes built\n\n")
                .Append("**Pipeline:** feature\n")
                .Append("| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n\n");
        }

        folder.Write($"{Large}/project.md", TestProjectsFolder.ProjectText("Large", "owner/large", body: body.ToString()));

        return ProjectViewFixtures.Read(folder, Large);
    }
}
