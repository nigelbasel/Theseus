using Theseus.Core.Links;

namespace Theseus.Tests.Links;

[TestFixture]
public class BriefFolderTests
{
    [TestCase(@"C:\dev\tfsgit")]
    [TestCase(@"C:\dev\git")]
    public void ReportPath_IsUnderTheSpawningWorkstreamsOwnWorkingDirectory(string workingDirectory)
    {
        Assert.That(BriefFolder.ReportPath(workingDirectory, "proposals", "token-spike"),
            Is.EqualTo(Path.Combine(workingDirectory, "proposals", "token-spike", "REPORT.md")));
    }

    [Test]
    public void For_FallsBackToTheDefaultFolderNameWhenNoneIsSet()
    {
        Assert.That(BriefFolder.For(@"C:\work", " ", "token-spike"),
            Is.EqualTo(Path.Combine(@"C:\work", BriefFolder.DefaultName, "token-spike")));
    }
}
