using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>New project makes a lead card, working in the project's folder and told to fill in the starter's placeholders.</summary>
[TestFixture]
public class ProjectLeadCardTests
{
    [Test]
    public void TheLead_IsNamedForTheProject_AndWorksInItsFolder()
    {
        string folder = Path.Combine(Path.GetTempPath(), "bug-fixing");
        ProjectLeadCard lead = ProjectLeadCard.For("Bug Fixing", folder);

        Assert.That((lead.Title, lead.WorkingDirectory), Is.EqualTo(("Bug Fixing lead", folder)));
        Assert.That(lead.Prompt, Does.Contain(Path.Combine(folder, "project.md")).And.Contain("placeholders"));
    }
}
