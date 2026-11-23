using Theseus.Core.Questions;

namespace Theseus.Tests.Questions;

/// <summary>The measured rule for a turn that asks the owner in prose (1.6 spec item 7). Every ending here is invented.</summary>
[TestFixture]
public class ProseQuestionTests
{
    [Test]
    public void AFinalLineEndingInAQuestionMark_Asks_AndIsTheLineShown()
    {
        Assert.That(ProseQuestion.AskingLine("All tests pass.\n\nShall I push the branch?"), Is.EqualTo("Shall I push the branch?"));
    }

    [Test]
    public void AQuestionFollowedByAListOfReasons_StillAsks()
    {
        const string message = "Which store should the cache use?\n- Redis is already running.\n- A file needs no service.";

        Assert.That(ProseQuestion.AskingLine(message), Is.EqualTo("Which store should the cache use?"));
    }

    [Test]
    public void AQuestionInThePenultimateParagraph_StillAsks()
    {
        const string message = "Report done.\n\nShould the old card go too?\n\nI have left it in place until you say.";

        Assert.That(ProseQuestion.AskingLine(message), Is.EqualTo("Should the old card go too?"));
    }

    [Test]
    public void AQuestionEarlierThanTheLastTwoParagraphs_IsNotWhatTheTurnEndsOn()
    {
        const string message = "Why did it fail? The cache was cold.\n\nI warmed it.\n\nThe build is green now.";

        Assert.That(ProseQuestion.AskingLine(message), Is.Null);
    }

    [Test]
    public void ASentenceEndingInAQuestionMarkMidLine_Asks()
    {
        const string message = "Question 3: is the disk encrypted on every laptop? Enforced means by policy.";

        Assert.That(ProseQuestion.AskingLine(message), Is.Not.Null);
    }

    [Test]
    public void AQuotedLabelWithAQuestionMark_IsNotAQuestion()
    {
        const string message = "Converted.\n\n- The button reads \"What next? Book a call\" as in the source.";

        Assert.That(ProseQuestion.AskingLine(message), Is.Null);
    }

    [TestCase("Give me the go-ahead and I'll run the migration.")]
    [TestCase("Done with step one. Say if you want the old files removed too.")]
    [TestCase("Let me know which of the two you prefer.")]
    [TestCase("If you send me the log, I'll take it from there.")]
    [TestCase("Do you want me to watch the build.")]
    public void AnAskingPhraseOrARequest_Asks(string message)
    {
        Assert.That(ProseQuestion.AskingLine(message), Is.Not.Null);
    }

    [TestCase("I can't say whether the other sessions are finished.")]
    [TestCase("Nothing is outstanding. The handoff is committed.")]
    [TestCase("Tell me when you're back and I'll pick it up.")]
    public void APlainEnding_DoesNotAsk(string message)
    {
        Assert.That(ProseQuestion.AskingLine(message), Is.Null);
    }

    [Test]
    public void QuestionMarksInCodeAndUrls_DoNotAsk()
    {
        const string message = "Run `dotnet test --filter Name~Foo?` and open https://example.test/build?id=1 to see it.";

        Assert.That(ProseQuestion.AskingLine(message), Is.Null);
    }

    [Test]
    public void AQuestionInsideAFencedBlock_DoesNotAsk()
    {
        const string message = "Here is the script.\n\n```\nread -p \"Continue?\" answer\n```";

        Assert.That(ProseQuestion.AskingLine(message), Is.Null);
    }

    [Test]
    public void TheLineShown_LosesItsListMarkerAndEmphasis_AndIsCut()
    {
        Assert.That(ProseQuestion.AskingLine("Done.\n\n- **Shall I raise the PR?**"), Is.EqualTo("Shall I raise the PR?"));

        var shown = ProseQuestion.AskingLine($"Shall I {new string('x', 300)}?")!;

        Assert.That(shown, Has.Length.EqualTo(ProseQuestion.MaxLength));
        Assert.That(shown, Does.EndWith("…"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void NothingSaid_DoesNotAsk(string? message)
    {
        Assert.That(ProseQuestion.AskingLine(message), Is.Null);
    }
}
