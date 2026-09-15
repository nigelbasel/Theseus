using Theseus.Core.Domain;
using Theseus.Core.Launching;

namespace Theseus.Tests.Launching;

[TestFixture]
public class WorkstreamIdentityTests
{
    private const string Handoff = @"C:\dev\tfsgit\SESSION-STATUS-2026-08-26-contact-forms.md";

    [Test]
    public void For_TellsTheSessionWhichWorkstreamItIs()
    {
        var identity = WorkstreamIdentity.For(Workstream(), Handoff);

        Assert.That(identity, Does.Contain("Contact form validation"));
        Assert.That(identity, Does.Contain("contact-forms"));
    }

    [Test]
    public void For_TellsItWhichNotesAreItsOwn()
    {
        Assert.That(WorkstreamIdentity.For(Workstream(), Handoff), Does.Contain(Handoff));
    }

    [Test]
    public void For_WarnsThatTheNotesNearbyBelongToOtherWorkstreams()
    {
        var identity = WorkstreamIdentity.For(Workstream(), Handoff);

        Assert.That(identity, Does.Contain("Other workstreams"));
        Assert.That(identity, Does.Contain("say so rather than doing it"),
            "the failure was a session doing work it found rather than reporting it");
    }

    [Test]
    public void For_FallsBackToTheKeyWhenAWorkstreamHasNoTitle()
    {
        var identity = WorkstreamIdentity.For(Workstream() with { Title = "  " }, Handoff);

        Assert.That(identity, Does.Contain("\"contact-forms\""));
    }

    private static Workstream Workstream() =>
        new("contact-forms", "Contact form validation", "#208DD7", ColorIsManual: true,
            @"C:\dev\tfsgit", [], "Add validation.", 1, "Today", IsBlocked: false,
            ReopenTomorrow: true, IsArchived: false, "session-1", [], null, DateTimeOffset.UtcNow);

    [Test]
    public void For_TellsASessionWhereToRecordAnIssueItRaises()
    {
        var identity = WorkstreamIdentity.For(Workstream(), Handoff, @"C:\plans\contact-forms.json");

        Assert.That(identity, Does.Contain(@"C:\plans\contact-forms.json"));
        Assert.That(identity, Does.Contain("jiraKeys"));
    }

    [Test]
    public void For_SaysNothingAboutAPlanFileItWasNotGiven()
    {
        Assert.That(WorkstreamIdentity.For(Workstream(), Handoff), Does.Not.Contain("jiraKeys"),
            "a session sent to edit a file nobody named would go looking for one");
    }

    [Test]
    public void For_UsesTheWordingFromSettingsInPlaceOfTheBuiltInText()
    {
        var identity = WorkstreamIdentity.For(Workstream(), Handoff, template: "Workstream {title} ({key}); notes at {handoff}.");

        Assert.That(identity, Is.EqualTo($"Workstream Contact form validation (contact-forms); notes at {Handoff}."));
    }

    [Test]
    public void For_FillsInThePlanFileWhenThereIsOne()
    {
        var identity = WorkstreamIdentity.For(Workstream(), Handoff, @"C:\plans\contact-forms.json", "Card: {plan}");

        Assert.That(identity, Is.EqualTo(@"Card: C:\plans\contact-forms.json"));
    }

    [Test]
    public void For_LeavesOutALineNamingThePlanFileWhenThereIsNone()
    {
        var identity = WorkstreamIdentity.For(Workstream(), Handoff, template: "Line one.\r\nYour card is {plan}.\nLine three.");

        Assert.That(identity, Is.EqualTo($"Line one.{Environment.NewLine}Line three."));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void For_UsesTheBuiltInTextWhenSettingsLeaveItEmpty(string? template)
    {
        Assert.That(WorkstreamIdentity.For(Workstream(), Handoff, @"C:\plans\a.json", template),
            Is.EqualTo(WorkstreamIdentity.For(Workstream(), Handoff, @"C:\plans\a.json")));
    }

    [Test]
    public void DefaultTemplate_LeavesNoPlaceholderUnfilled()
    {
        var identity = WorkstreamIdentity.For(Workstream(), Handoff, @"C:\plans\contact-forms.json");

        foreach (var placeholder in new[] { WorkstreamIdentity.TitlePlaceholder, WorkstreamIdentity.KeyPlaceholder,
                     WorkstreamIdentity.PlanPlaceholder, SessionOpening.HandoffPlaceholder })
        {
            Assert.That(identity, Does.Not.Contain(placeholder));
        }
    }
}
