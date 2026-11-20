using Theseus.App.ViewModels;
using Theseus.Core.Projects;
using static Theseus.Tests.Projects.ProjectCards;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// The Project menu on a card and the menu bar (project-view AC 4): it offers the slugs a readable project lists, and
/// it is not there at all while no project is readable (AC 22).
/// </summary>
[TestFixture]
public class ProjectMenuTests
{
    private static readonly ProjectFileReading Readable = ProjectFileReader.Read("a_project", """
        # A project

        **Kit:** method-v0.2
        **Primary branch:** owner/p

        ## Phase 0: Seed

        ### Objective 0.1: First
        - [ ] One.

        **Pipeline:** feature
        | Workstream | Slug | Branch | Repos |
        |---|---|---|---|
        | The work | first-slug | owner/first-slug | Repo |
        """);

    private readonly List<(string? Project, string? Slug)> _chosen = [];

    private IReadOnlyList<ProjectChoice> Menu(IReadOnlyList<ProjectFileReading> readings, Theseus.Core.Domain.Workstream? card = null) =>
        ProjectMenu.For(readings, card, (project, slug) => _chosen.Add((project, slug)));

    [Test]
    public void WithNoProjectReadable_ThereIsNoMenu()
    {
        Assert.That(Menu([]), Is.Empty);
        Assert.That(Menu([ProjectFileReading.Unreadable("broken", "line 1: no **Kit:** line under the title"),
            ProjectFileReading.PinnedTo("old")]), Is.Empty);
    }

    [Test]
    public void AReadableProject_OffersNoSlugYetAndItsSlugs_ThenNotInAProject()
    {
        var menu = Menu([Readable]);

        Assert.That(menu.Select(item => item.Header), Is.EqualTo(new[] { "a__project", ProjectMenu.NotInAProject }),
            "an underscore is doubled so it shows rather than marking an access key");
        Assert.That(menu[0].Children.Select(item => item.Header), Is.EqualTo(new[] { ProjectMenu.NoSlugYet, "first-slug" }));
        Assert.That(menu[0].Children[1].ToolTip, Is.EqualTo("The work, objective 0.1: First"));
    }

    [Test]
    public void ChoosingASlug_PassesTheProjectAndSlug()
    {
        var menu = Menu([Readable]);

        menu[0].Children[1].Command!.Execute(null);
        menu[0].Children[0].Command!.Execute(null);
        menu[1].Command!.Execute(null);

        Assert.That(_chosen, Is.EqualTo(new (string?, string?)[] { ("a_project", "first-slug"), ("a_project", null), (null, null) }));
    }

    [Test]
    public void TheCardsMembership_IsTicked()
    {
        var inProject = Menu([Readable], Card("one", "a_project", "first-slug"));
        var adHoc = Menu([Readable], Card("two"));

        Assert.That(inProject[0].IsChecked && inProject[0].Children[1].IsChecked && !inProject[0].Children[0].IsChecked, Is.True);
        Assert.That(adHoc[1].IsChecked, Is.True);
    }

    [Test]
    public void AnUnreadableProject_BesideAReadableOne_IsListedDisabled_WithItsReason()
    {
        var menu = Menu([Readable, ProjectFileReading.Unreadable("broken", "line 1: no **Kit:** line under the title")]);

        Assert.That(menu[1].IsEnabled, Is.False);
        Assert.That(menu[1].ToolTip, Is.EqualTo("broken: unreadable: line 1: no **Kit:** line under the title"));
    }
}
