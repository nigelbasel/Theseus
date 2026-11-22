using Theseus.Core.Projects;
using Theseus.Core.Proposals;
using Theseus.Tests.Projects;

namespace Theseus.Tests.Proposals;

/// <summary>
/// The row Theseus writes on approving a proposal from a card in a project (1.1e AC 31, 32): under the objective it names,
/// with its branch and repos or none and read-only, and a proposal naming no objective not approvable.
/// </summary>
[TestFixture]
public class ProposalRowTests
{
    private static readonly ProjectFile File = ProjectFileReader.Read("bym",
        TestProjectsFolder.ProjectText("BYM", body: TestProjectsFolder.OneObjective("w7")
            + "\n### Objective 0.2: Search\n- [ ] Finds.\n\n**Pipeline:** feature\n| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n")).File!;

    private static ProposalRequest Request(string? objective, string? slug = "w9", string? branch = null, params string[] repos) =>
        new("Search reads", @"<brief>", false, null, "Reads", [], null) { Objective = objective, Slug = slug, Branch = branch, Repos = repos };

    [Test]
    public void TheRow_GoesUnderTheObjectiveNamed_ByNumberOrTitle_WithItsBranchAndRepos()
    {
        Assert.That(ProposalRow.For(File, Request("0.2", branch: "nigel/w9", repos: ["Theseus", "claude-hq"]), "w9-card"),
            Is.EqualTo(("0.2", new ProjectWorkstreamRow("w9-card", "w9", "nigel/w9", "Theseus, claude-hq", 0))));
        Assert.That(ProposalRow.For(File, Request("Search"), "w9-card")?.Objective, Is.EqualTo("0.2"));
    }

    [Test]
    public void NoBranchOrReposNamed_IsNoneAndReadOnly_NeverInvented()
    {
        Assert.That(ProposalRow.For(File, Request("0.1"), "w9-card")?.Row,
            Is.EqualTo(new ProjectWorkstreamRow("w9-card", "w9", ProposalRow.NoBranch, ProposalRow.ReadOnlyRepos, 0)));
        Assert.That((ProposalRow.NoBranch, ProposalRow.ReadOnlyRepos), Is.EqualTo(("none", "read-only")));
    }

    [Test]
    public void AProposalNamingNoObjective_CannotBeApproved_AndSaysSo()
    {
        Assert.That(ProposalRow.WhyNot(File, Request(null)), Does.StartWith("It names no objective"));
        Assert.That(ProposalRow.For(File, Request(null), "w9-card"), Is.Null);
        Assert.That(ProposalRow.WhyNot(File, Request("9.9")), Is.EqualTo("It names objective 9.9, which the project file does not have."));
        Assert.That(ProposalRow.WhyNot(File, Request("0.1", slug: "w7")), Is.EqualTo("Its records folder, w7, is already in the project file's workstreams."));
        Assert.That(ProposalRow.WhyNot(File, Request("0.1")), Is.Null);
    }

    [Test]
    public void TheRow_IsWrittenUnderThatObjective_AndTheEditRefusesAChangedFile()
    {
        var home = Path.Combine(Path.GetTempPath(), $"theseus-proposal-row-{Guid.NewGuid():N}");
        Directory.CreateDirectory(home);

        try
        {
            var text = TestProjectsFolder.ProjectText("BYM", body: TestProjectsFolder.OneObjective("w7")
                + "\n### Objective 0.2: Search\n- [ ] Finds.\n\n**Pipeline:** feature\n| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n");
            System.IO.File.WriteAllText(Path.Combine(home, ProjectCatalog.ProjectFileName), text);
            var reading = ProjectFileReader.Read("bym", text) with { Folder = home, Text = text };
            var placed = ProposalRow.For(reading.File!, Request("0.2"), "w9-card")!.Value;

            Assert.That(ProjectFileWriter.IsCurrent(reading), Is.True);

            var written = ProjectFileReader.Read("bym", ProjectFileWriter.Apply(reading, file => ProjectFileEditor.AddRow(file, placed.Objective, placed.Row))).File!;

            Assert.That(written.ObjectiveOf("w9")?.Number, Is.EqualTo("0.2"));
            Assert.That(written.Objectives[0].Workstreams.Select(row => row.Slug), Is.EqualTo(new[] { "w7" }), "never under the first objective");
            Assert.That(ProjectFileWriter.IsCurrent(reading), Is.False, "the file is now ahead of the reading");
            Assert.Throws<ProjectEditRefusedException>(() => ProjectFileWriter.Apply(reading, file => ProjectFileEditor.AddRow(file, placed.Objective, placed.Row)));
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    [Test]
    public void ARecordsFolderTakenAlready_IsNeverWrittenTwice_EvenWhenItIsTheCardsKey()
    {
        Assert.That(ProposalRow.For(File, Request("0.1", slug: null), "w7"), Is.Null);
    }

    [Test]
    public void AVerifiersRow_IsVerifyAndTheSlug_NumberedWhenTaken_BranchNoneReadOnly()
    {
        Assert.That(ProposalRow.ForVerifier(File, "w7", "v1"), Is.EqualTo(new ProjectWorkstreamRow("v1", "verify-w7", "none", "read-only", 0)));

        var taken = ProjectFileReader.Read("bym", TestProjectsFolder.ProjectText("BYM", body: TestProjectsFolder.OneObjective("w7", "verify-w7"))).File!;

        Assert.That(ProposalRow.ForVerifier(taken, "w7", "v2").Slug, Is.EqualTo("verify-w7-2"));
    }
}
