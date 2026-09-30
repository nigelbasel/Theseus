using Theseus.Core.Domain;
using Theseus.Core.Launching;

namespace Theseus.Tests.Launching;

[TestFixture]
public class WorkstreamLaunchPlannerTests
{
    private static readonly DateTimeOffset Expiry = new(2026, 8, 26, 9, 2, 0, TimeSpan.Zero);

    [Test]
    public void Plan_PutsNothingButThePipeAndTicketOnTheTerminalCommandLine()
    {
        var argv = Plan().TerminalArguments;
        var separator = argv.ToList().IndexOf("--");

        Assert.That(argv.Skip(separator + 1),
            Is.EqualTo(new[]
            {
                @"C:\dev\git\Theseus\Theseus.Launcher.exe", "--pipe", "Theseus.Launch", "--ticket", "abc123",
            }));
    }

    [Test]
    public void Plan_CarriesThePromptInTheTicketWhereTheTerminalCannotMangleIt()
    {
        var plan = Plan(opening: "Resolve the conflicts; then stop.");

        Assert.That(plan.Ticket.Spec.Prompt, Is.EqualTo("Resolve the conflicts; then stop."));
        Assert.That(plan.TerminalArguments, Has.None.Contains("Resolve"));
    }

    [Test]
    public void Plan_EscapesTheSemicolonInATabTitleSoOneTabOpensRatherThanTwo()
    {
        var plan = Plan(title: "GDPR; contact forms");

        Assert.That(plan.TerminalArguments.ToList()[plan.TerminalArguments.ToList().IndexOf("--title") + 1],
            Is.EqualTo(@"GDPR\; contact forms"));
    }

    [Test]
    public void Plan_TargetsTheNamedWindowAndTheWorkstreamColour()
    {
        var argv = Plan().TerminalArguments.ToList();

        Assert.That(argv[argv.IndexOf("-w") + 1], Is.EqualTo("theseus"));
        Assert.That(argv[argv.IndexOf("--tabColor") + 1], Is.EqualTo("#C0392B"));
        Assert.That(argv[argv.IndexOf("-d") + 1], Is.EqualTo(@"C:\work\repo"));
    }

    [Test]
    public void Plan_MintsAnIdentityOnAFirstRun()
    {
        var plan = Plan();

        Assert.That(plan.Ticket.Spec.Mode, Is.EqualTo(LaunchMode.FirstRun));
        Assert.That(plan.Ticket.Spec.SessionId, Is.EqualTo("session-1"));
    }

    [Test]
    public void Plan_ResumesTheSameIdentityWhenAskedTo()
    {
        var plan = Plan(mode: LaunchMode.Resume);

        Assert.That(plan.Ticket.Spec.Mode, Is.EqualTo(LaunchMode.Resume));
    }

    [Test]
    public void Plan_TiesTheTicketToTheWorkstreamSoAReportCanBeAttributed()
    {
        Assert.That(Plan().Ticket.WorkstreamKey, Is.EqualTo("contact-forms"));
        Assert.That(Plan().Ticket.ExpiresUtc, Is.EqualTo(Expiry));
    }

    [Test]
    public void Plan_PassesTheWorkstreamsExtraDirectoriesThroughToTheSession()
    {
        Assert.That(Plan().Ticket.Spec.AddDirectories,
            Is.EqualTo(new[] { @"C:\work\repo\features\contact-forms" }));
    }

    [Test]
    public void Plan_CarriesTheOpeningARemintWouldNeedAlongsideTheResumeLine()
    {
        var plan = Plan(mode: LaunchMode.Resume, opening: "Resumed at 08:02.", openingIfReminted: "Read the handoff.");

        Assert.That(plan.Ticket.Spec.Prompt, Is.EqualTo("Resumed at 08:02."));
        Assert.That(plan.Ticket.Spec.PromptIfReminted, Is.EqualTo("Read the handoff."));
    }

    [Test]
    public void Plan_LeavesTheRemintOpeningUnsetWhenTheSamePromptServesBoth()
    {
        Assert.That(Plan().Ticket.Spec.PromptIfReminted, Is.Null);
    }

    private static WorkstreamLaunchPlan Plan(
        string title = "Contact form validation",
        string prompt = "Carry on.",
        LaunchMode mode = LaunchMode.FirstRun,
        string opening = "Carry on.",
        string? openingIfReminted = null)
    {
        var workstream = new Workstream("contact-forms", title, "#C0392B", ColorIsManual: true,
            @"C:\work\repo", [@"C:\work\repo\features\contact-forms"], prompt, 1, "Today",
            IsBlocked: false, ReopenTomorrow: true, IsArchived: false, LastSessionId: null,
            [], null, DateTimeOffset.UtcNow);

        var request = new WorkstreamLaunchRequest(workstream, "abc123", "session-1", mode,
            @"C:\Users\dev\.local\bin\claude.exe",
            @"C:\Program Files\WindowsApps\WindowsTerminal.exe",
            @"C:\dev\git\Theseus\Theseus.Launcher.exe",
            "Theseus.Launch", "theseus", opening, OpeningIfReminted: openingIfReminted);

        return WorkstreamLaunchPlanner.Plan(request, Expiry);
    }
    [Test]
    public void Plan_CarriesTheOpeningItWasGivenWithoutRewritingIt()
    {
        var opening = $"House rules.{Environment.NewLine}{Environment.NewLine}Carry on.";

        Assert.That(Plan(opening: opening).Ticket.Spec.Prompt, Is.EqualTo(opening));
    }
}
