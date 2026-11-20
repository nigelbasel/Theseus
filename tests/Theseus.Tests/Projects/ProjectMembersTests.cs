using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// Which card is in which project comes from the project files alone (1.1c AC 1 and 2): a row's Workstream names a card,
/// its last row gives its current slug, a pinned or unreadable project names no one, and a card two projects name goes
/// to the first by name with both reported.
/// </summary>
[TestFixture]
public class ProjectMembersTests
{
    private static ProjectFileReading Project(string name, params (string Card, string Slug)[] rows) =>
        ProjectFileReader.Read(name,
            TestProjectsFolder.ProjectText(name, body:
                "## Phase 0: Start\n\n### Objective 0.1: Something\n- [ ] A criterion.\n\n**Pipeline:** feature\n"
                + "| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n"
                + string.Concat(rows.Select(row => $"| {row.Card} | {row.Slug} | owner/{row.Slug} | a-repo |\n"))));

    [Test]
    public void ACardTheTableNames_IsInTheProject_UnderItsLastRowsSlug()
    {
        var members = ProjectMembers.From([Project("theseus", ("git-22", "first-slug"), ("other-card", "other-slug"), ("git-22", "last-slug"))]);

        Assert.That(members.Of("git-22"), Is.EqualTo(new ProjectMember("theseus", "last-slug")));
        Assert.That(members.Of("GIT-22"), Is.EqualTo(new ProjectMember("theseus", "last-slug")), "card keys compare without case");
        Assert.That(members.Of("other-card"), Is.EqualTo(new ProjectMember("theseus", "other-slug")));
        Assert.That(members.Of("not-named"), Is.Null);
        Assert.That(members.Clashes, Is.Empty);
    }

    [Test]
    public void APinnedOrUnreadableProject_NamesNoOne()
    {
        var members = ProjectMembers.From(
        [
            ProjectFileReading.PinnedTo("old"),
            ProjectFileReading.Unreadable("broken", "line 1: no **Kit:** line under the title"),
        ]);

        Assert.That(members.ByCard, Is.Empty);
    }

    [Test]
    public void ACardTwoProjectsName_IsInTheFirstByName_AndBothAreReported()
    {
        var members = ProjectMembers.From([Project("b-project", ("shared", "b-slug")), Project("a-project", ("shared", "a-slug"))]);

        Assert.That(members.Of("shared"), Is.EqualTo(new ProjectMember("a-project", "a-slug")));
        Assert.That(members.Clashes, Is.EqualTo(new[] { "a-project", "b-project" }));
    }
}
