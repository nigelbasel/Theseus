using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// The reader after the review of PR 1: a pipeline is exactly one of the defined names (item 5), and an indented tick
/// box is a criterion of its own, so an unticked sub-criterion keeps its objective open (item 6).
/// </summary>
[TestFixture]
public class ProjectFileReaderReviewFixTests
{
    private const string Header = "# A project\n\n**Kit:** method-v0.2\n**Primary branch:** owner/project\n\n## Phase 0: Seed\n";

    private const string Table = "| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n";

    private static ProjectFileReading Read(string criteria, string pipeline = "feature") =>
        ProjectFileReader.Read("a-project", $"{Header}### Objective 0.1: Something\n{criteria}\n**Pipeline:** {pipeline}\n{Table}");

    [TestCase("feature, bug")]
    [TestCase("bug,freeform")]
    [TestCase("1")]
    [TestCase("Feature | Bug")]
    public void APipelineThatIsNotExactlyADefinedName_IsRefused(string written)
    {
        var reading = Read("- [ ] One.\n", written);

        Assert.That(reading.Status, Is.EqualTo(ProjectReadingStatus.Unreadable));
        Assert.That(reading.Reason, Does.EndWith("is not feature, bug or freeform"));
    }

    [Test]
    public void AnIndentedTickBox_IsACriterionOfItsOwn()
    {
        var objective = Read("- [x] The parent,\n  carried on.\n  - [ ] A sub-criterion not met.\n  - [x] One met.\n").File!.Objectives.Single();

        Assert.That(objective.Criteria.Select(criterion => (criterion.Text, criterion.IsTicked)), Is.EqualTo(new[]
        {
            ("The parent, carried on.", true),
            ("A sub-criterion not met.", false),
            ("One met.", true),
        }));
        Assert.That(objective.IsDone, Is.False, "an unticked sub-criterion keeps the objective open");
    }
}
