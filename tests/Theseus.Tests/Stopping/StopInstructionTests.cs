using Theseus.Core.Stopping;

namespace Theseus.Tests.Stopping;

[TestFixture]
public class StopInstructionTests
{
    private static readonly StopTarget[] Two =
    [
        new("Contact form validation", @"C:\work\repo\HANDOFF-contact-forms.md"),
        new("Job queue migration", @"C:\work\repo\HANDOFF-job-queue.md"),
    ];

    [Test]
    public void Compose_NamesEverySessionToBeStopped()
    {
        var instruction = StopInstruction.Compose(null, Two);

        Assert.That(instruction, Does.Contain("\"Contact form validation\""));
        Assert.That(instruction, Does.Contain("\"Job queue migration\""));
    }

    [Test]
    public void Compose_GivesEachSessionItsOwnHandoffPath()
    {
        var instruction = StopInstruction.Compose(null, Two);

        Assert.That(instruction, Does.Contain(@"C:\work\repo\HANDOFF-contact-forms.md"));
        Assert.That(instruction, Does.Contain(@"C:\work\repo\HANDOFF-job-queue.md"));
    }

    [Test]
    public void Compose_LeavesNoPlaceholderUnfilled()
    {
        Assert.That(StopInstruction.Compose(null, Two),
            Does.Not.Contain(StopInstruction.HandoffPlaceholder));
    }

    [Test]
    public void Compose_UsesTheConfiguredWordsWhenThereAreSome()
    {
        var instruction = StopInstruction.Compose(
            "Down tools. Put your notes in " + StopInstruction.HandoffPlaceholder + " and quit.", Two);

        Assert.That(instruction, Does.Contain("Down tools."));
        Assert.That(instruction, Does.Not.Contain("Please stop what you are doing"));
    }

    [Test]
    public void Compose_FallsBackToTheDefaultWordsWhenNoneAreConfigured()
    {
        Assert.That(StopInstruction.Compose("   ", Two), Does.Contain("Write a handoff to"));
    }

    [Test]
    public void Compose_TellsTheCourierNotToStrayBeyondTheSessionsNamed()
    {
        var instruction = StopInstruction.Compose(null, Two);

        Assert.That(instruction, Does.Contain("Send nothing to any other session"));
        Assert.That(instruction, Does.Contain("do not carry out the work"));
    }

    [Test]
    public void Compose_GivesNothingToDoWhenNoSessionIsRunning()
    {
        Assert.That(StopInstruction.Compose(null, []), Is.Null);
    }

    [Test]
    public void Compose_IgnoresATargetWithNoNameToAddressItBy()
    {
        var instruction = StopInstruction.Compose(null, [new StopTarget("  ", "path")]);

        Assert.That(instruction, Is.Null);
    }

    [Test]
    public void Compose_AsksForAReplyItCanRecognise()
    {
        Assert.That(StopInstruction.Compose(null, Two), Does.Contain("SENT"));
    }
}
