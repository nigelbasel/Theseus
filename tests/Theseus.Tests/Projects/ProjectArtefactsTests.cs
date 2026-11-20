using Theseus.Core.Links;
using Theseus.Core.Projects;
using static Theseus.Tests.Projects.ProjectCatalogTests;

namespace Theseus.Tests.Projects;

/// <summary>
/// The background read also reads every slug's artefacts, so the project view needs no git on the UI thread (PR 2);
/// and a verifier's job ends when its report back is accepted (AC 10).
/// </summary>
[TestFixture]
public class ProjectArtefactsTests
{
    private const string Body = "## Phase 0: Seed\n\n### Objective 0.1: First\n- [ ] One.\n\n**Pipeline:** feature\n"
                                + "| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n"
                                + "| The work | a-slug | owner/a-slug | Repo |\n| Later work | b-slug | owner/b-slug | Repo |\n";

    [Test]
    public void AReadableProject_GetsEachSlugsArtefacts_IncludingTheSpecText()
    {
        using var repository = TestRepository.Create();
        repository.Commit("projects/a-project.md", ProjectText("A project", body: Body));
        repository.Branch("owner/project");
        repository.Branch("owner/a-slug");
        repository.Commit("work/a-slug/spec.md", "# The spec\n");
        repository.Checkout("owner/project");

        var reading = ProjectArtefacts.Attach(ProjectCatalog.Read([repository.Folder], new GitCli()).Single(), new GitCli());

        Assert.That(reading.Artefacts["a-slug"].SpecText, Is.EqualTo("# The spec\n"));
        Assert.That(reading.Artefacts["a-slug"].Source.Branch, Is.EqualTo("owner/a-slug"));
        Assert.That(reading.Artefacts["b-slug"].Source.IsOnMachine, Is.False);
    }

    [Test]
    public void AnUnreadableReading_IsReturnedAsItIs()
    {
        var unreadable = ProjectFileReading.Unreadable("broken", "line 1: no **Kit:** line under the title");

        Assert.That(ProjectArtefacts.Attach(unreadable, new GitCli()), Is.SameAs(unreadable));
    }

    [Test]
    public void AVerifier_HasEnded_OnlyOnceItsReportBackIsAccepted()
    {
        var made = new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);
        var waiting = new Link("1", LinkKind.ReportsBack, "checker", "work", made, Link.MadeOnTheBoard, "report.md");
        var accepted = waiting with { SatisfiedUtc = made.AddHours(1) };
        var handOver = new Link("2", LinkKind.HandsOver, "work", "checker", made, Link.MadeOnTheBoard, SatisfiedUtc: made);

        Assert.That(VerifierEnding.HasEnded([waiting, handOver], "checker"), Is.False);
        Assert.That(VerifierEnding.HasEnded([accepted], "checker"), Is.True);
        Assert.That(VerifierEnding.HasEnded([accepted with { RemovedUtc = made.AddHours(2) }], "checker"), Is.False);
    }
}
