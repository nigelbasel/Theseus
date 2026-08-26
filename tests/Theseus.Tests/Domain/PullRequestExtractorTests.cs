using Theseus.Core.Domain;

namespace Theseus.Tests.Domain;

[TestFixture]
public class PullRequestExtractorTests
{
    [Test]
    public void Extract_FindsAPullRequestMentionedInPassing()
    {
        Assert.That(PullRequestExtractor.Extract("PR 20769 has merge conflicts that need resolving."),
            Is.EqualTo(new[] { 20769 }));
    }

    [Test]
    public void Extract_FindsSeveralAndKeepsTheOrderTheyAppearIn()
    {
        Assert.That(PullRequestExtractor.Extract("PR 20769 is paired with PR 20770. Both drafts."),
            Is.EqualTo(new[] { 20769, 20770 }));
    }

    [TestCase("PR #20769")]
    [TestCase("PR#20769")]
    [TestCase("pr 20769")]
    [TestCase("pull request 20769")]
    [TestCase("Pull Request #20769")]
    [TestCase("PRs 20769")]
    public void Extract_ReadsTheWaysPeopleActuallyWriteIt(string text)
    {
        Assert.That(PullRequestExtractor.Extract(text), Is.EqualTo(new[] { 20769 }));
    }

    [Test]
    public void Extract_ReportsEachOnceHoweverOftenItIsRepeated()
    {
        Assert.That(PullRequestExtractor.Extract("PR 20769, then PR 20769 again, still PR 20769."),
            Is.EqualTo(new[] { 20769 }));
    }

    [Test]
    public void Extract_IgnoresAJiraKeyThatHappensToStartWithThoseLetters()
    {
        Assert.That(PullRequestExtractor.Extract("PROJ-101 is the issue."), Is.Empty);
    }

    [Test]
    public void Extract_IgnoresABareNumberWithNothingMarkingItAsAPullRequest()
    {
        Assert.That(PullRequestExtractor.Extract("There are 20769 rows in the table."), Is.Empty);
    }

    [Test]
    public void Extract_IgnoresAWordThatMerelyStartsWithPr()
    {
        Assert.That(PullRequestExtractor.Extract("Preview 2026 of the designer."), Is.Empty);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void Extract_HandlesAnEmptyPrompt(string? prompt)
    {
        Assert.That(PullRequestExtractor.Extract(prompt), Is.Empty);
    }
}
