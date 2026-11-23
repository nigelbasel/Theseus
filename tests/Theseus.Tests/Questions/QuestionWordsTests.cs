using Theseus.Core.Questions;

namespace Theseus.Tests.Questions;

/// <summary>A question in Theseus's words (1.6 spec items 6 and 8).</summary>
[TestFixture]
public class QuestionWordsTests
{
    private static readonly DateTimeOffset Nine = new(2026, 11, 23, 9, 0, 0, TimeSpan.Zero);
    private static readonly OwnerQuestion Asked = new("s", "Push now?", IsGuess: false, Nine) { Options = ["Yes", "Later"], Why = "CI is idle" };
    private static readonly OwnerQuestion Guessed = new("s", "Shall I push?", IsGuess: true, Nine);

    [Test]
    public void AnAskedQuestion_ReadsAsAsks()
    {
        Assert.That(QuestionWords.Card(Asked), Is.EqualTo("Asks: Push now?"));
        Assert.That(QuestionWords.Named("W7", Asked), Is.EqualTo("W7 asks: Push now?"));
    }

    [Test]
    public void AGuess_SaysItIsOne()
    {
        Assert.That(QuestionWords.Card(Guessed), Is.EqualTo("Probably asking you something: Shall I push?"));
        Assert.That(QuestionWords.Named("W7", Guessed), Is.EqualTo("W7 is probably asking you something: Shall I push?"));
    }

    [Test]
    public void TheDetail_CarriesTheOptionsAndWhy()
    {
        Assert.That(QuestionWords.Detail(Asked).Split(Environment.NewLine),
            Is.EqualTo(new[] { "Asks: Push now?", "Options: Yes · Later", "Why: CI is idle" }));
        Assert.That(QuestionWords.Detail(Guessed), Does.Contain("did not ask with the question tool"));
    }
}
