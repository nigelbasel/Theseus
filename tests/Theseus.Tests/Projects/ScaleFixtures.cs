using System.Text;
using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// A project the size of a real one written before any work (project-view-scale, 2026-11-24): seven phases of five
/// objectives, 35 in all, no workstreams, and waiting notes in every form a lead writes: the owner's asks repeated across
/// criteria and joined with work asks, an ask on someone named in the <c>on</c> form, an older line naming another team,
/// and an older line naming the owner with others.
/// </summary>
internal static class ScaleFixtures
{
    public const string Large = "large";

    public const int Objectives = 35;

    public const string SharedAsk = "Owner names the agent who runs the holdouts";

    public const string Team = "the gateway team";

    public const string LegacyTeam = "Gateway owners";

    public const string Budget = "Product team with Owner: a latency budget";

    /// <summary>The owner's names: the account, its full name and its first name.</summary>
    public static IReadOnlyList<string> Names { get; } = OwnerNames.From("owner-account", "Owner Person");

    /// <summary>The waits for you in phase 0, the phase under way: the shared ask, five objectives' own, and the budget.</summary>
    public const int Yours = 7;

    /// <summary>The ask each objective's third criterion makes of the owner alone.</summary>
    public static string OwnAsk(int index) => $"Owner sets the threshold of objective {Number(index)}";

    /// <summary>The objective number of the index'th objective, from 0: five per phase, phases from 0.</summary>
    public static string Number(int index) => $"{index / 5}.{(index % 5) + 1}";

    /// <summary>Writes the large project and reads it as the catalog does.</summary>
    public static ProjectFileReading WriteLarge(TestProjectsFolder folder) =>
        ProjectViewFixtures.Read(Write(folder), Large);

    /// <summary>The large project's text.</summary>
    public static string Text()
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
                .Append("      Waiting: work: the routes built\n");

            switch (index % 5)
            {
                case 0:
                    body.Append("- [ ] A criterion waiting on a team, by name.\n").Append($"      Waiting: on {Team}: an answer\n");
                    break;
                case 1:
                    body.Append("- [ ] A criterion an older line puts on a team.\n").Append($"      Waiting: owner: {LegacyTeam}: the EC answers\n");
                    break;
                case 2:
                    body.Append("- [ ] A criterion an older line puts on a team with the owner.\n").Append($"      Waiting: owner: {Budget}\n");
                    break;
            }

            body.Append("\n**Pipeline:** feature\n").Append("| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n\n");
        }

        return TestProjectsFolder.ProjectText("Large", "owner/large", body: body.ToString());
    }

    private static TestProjectsFolder Write(TestProjectsFolder folder)
    {
        folder.Write($"{Large}/project.md", Text());

        return folder;
    }
}
