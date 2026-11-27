using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>A project file at <c>method-v0.3</c> reads as one at <c>method-v0.2</c> does: v0.3 changed the rules, not the file.</summary>
[TestFixture]
public class KitVersionsReadTests
{
    private const string Body =
        "## Objective\nThe outcome.\n\n## Phase 0: Seed\n**Exit gate:** the owner.\n\n### Objective 0.1: Something\n- [ ] One.\n\n"
        + "**Pipeline:** feature\n| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n| A card | a-slug | owner/a-slug | Repo |\n";

    private static ProjectFileReading Read(string kit) =>
        ProjectFileReader.Read("a-project", $"# A project\n\n**Kit:** {kit}\n**Primary branch:** owner/project\n\n{Body}");

    [TestCase("method-v0.2")]
    [TestCase("method-v0.3")]
    public void EitherVersion_Reads_AndKeepsItsKit(string kit)
    {
        var reading = Read(kit);

        Assert.That(reading.Reason, Is.Null);
        Assert.That(reading.File!.Kit, Is.EqualTo(kit));
        Assert.That(reading.File.Objectives.Single().Workstreams.Single().Slug, Is.EqualTo("a-slug"));
    }

    [Test]
    public void AV03File_ReadsTheSameAsAV02File()
    {
        var two = Read("method-v0.2").File!;
        var three = Read("method-v0.3").File!;

        Assert.That((three.Title, three.PrimaryBranch, three.Phases.Count, three.Objectives.Count),
            Is.EqualTo((two.Title, two.PrimaryBranch, two.Phases.Count, two.Objectives.Count)));
    }

    [Test]
    public void TheKitLine_IsMatchedWithoutRegardToCase_AndNamedAsTheseusNamesIt()
    {
        Assert.That(Read("Method-V0.3").File!.Kit, Is.EqualTo("method-v0.3"));
    }

    [Test]
    public void ALaterVersion_IsStillRefused_NamingBoth()
    {
        Assert.That(Read("method-v0.4").Reason, Does.Contain("it reads method-v0.2 and method-v0.3"));
    }

    [Test]
    public void TheMethodsOwnVersion_IsOneTheseusReads()
    {
        var readme = System.IO.File.ReadAllText(Path.Combine(RepositoryRoot(), "method", "README.md"));
        var version = System.Text.RegularExpressions.Regex.Match(readme, @"\*\*Version:\*\* `([^`]+)`").Groups[1].Value;

        Assert.That(MethodKits.Readable(version), Is.EqualTo(version));
    }

    private static string RepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", ".."));
}
