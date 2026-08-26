using Theseus.Core.Launching;

namespace Theseus.Tests.Launching;

[TestFixture]
public class SessionPromptTests
{
    private const string Preamble = "Read the handover notes before changing anything.";
    private const string Prompt = "Carry on with the contact form work.";

    [Test]
    public void Compose_PutsThePreambleFirstBecauseItFramesWhatFollows()
    {
        var composed = SessionPrompt.Compose(Preamble, Prompt);

        Assert.That(composed, Does.StartWith(Preamble));
        Assert.That(composed, Does.EndWith(Prompt));
    }

    [Test]
    public void Compose_SeparatesThemSoTheyDoNotReadAsOneParagraph()
    {
        var composed = SessionPrompt.Compose(Preamble, Prompt);

        Assert.That(composed, Is.EqualTo($"{Preamble}{Environment.NewLine}{Environment.NewLine}{Prompt}"));
    }

    [Test]
    public void Compose_IsJustThePromptWhenNoPreambleIsConfigured()
    {
        Assert.That(SessionPrompt.Compose(null, Prompt), Is.EqualTo(Prompt));
        Assert.That(SessionPrompt.Compose("   ", Prompt), Is.EqualTo(Prompt));
    }

    [Test]
    public void Compose_IsJustThePreambleWhenTheWorkstreamHasNoPromptOfItsOwn()
    {
        Assert.That(SessionPrompt.Compose(Preamble, "  "), Is.EqualTo(Preamble));
    }

    [Test]
    public void Compose_IsEmptyWhenThereIsNeither()
    {
        Assert.That(SessionPrompt.Compose(null, null), Is.Empty);
    }

    [Test]
    public void Compose_TrimsBothSoStrayBlankLinesDoNotAccumulate()
    {
        var composed = SessionPrompt.Compose($"  {Preamble}  ", $"\n\n{Prompt}\n\n");

        Assert.That(composed, Is.EqualTo($"{Preamble}{Environment.NewLine}{Environment.NewLine}{Prompt}"));
    }

    [Test]
    public void Compose_KeepsTheShapeOfAMultiLinePreamble()
    {
        var multiLine = $"First line.{Environment.NewLine}Second line.";

        Assert.That(SessionPrompt.Compose(multiLine, Prompt), Does.Contain(multiLine));
    }
}
