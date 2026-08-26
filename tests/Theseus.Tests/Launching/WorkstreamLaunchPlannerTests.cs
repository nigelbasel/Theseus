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
        var plan = Plan(prompt: "Resolve the conflicts; then stop.");

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

        Assert.That(argv[argv.IndexOf("-w") + 1], Is.EqualTo("nigel-theseus"));
        Assert.That(argv[argv.IndexOf("--tabColor") + 1], Is.EqualTo("#C0392B"));
        Assert.That(argv[argv.IndexOf("-d") + 1], Is.EqualTo(@"C:\dev\tfsgit"));
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
        Assert.That(Plan().Ticket.WorkstreamKey, Is.EqualTo("gdpr-mcp"));
        Assert.That(Plan().Ticket.ExpiresUtc, Is.EqualTo(Expiry));
    }

    [Test]
    public void Plan_PassesTheWorkstreamsExtraDirectoriesThroughToTheSession()
    {
        Assert.That(Plan().Ticket.Spec.AddDirectories,
            Is.EqualTo(new[] { @"C:\dev\tfsgit\features\apps-2170" }));
    }

    private static WorkstreamLaunchPlan Plan(
        string title = "GDPR MCP contact forms",
        string prompt = "Carry on.",
        LaunchMode mode = LaunchMode.FirstRun)
    {
        var workstream = new Workstream("gdpr-mcp", title, "#C0392B", ColorIsManual: true,
            @"C:\dev\tfsgit", [@"C:\dev\tfsgit\features\apps-2170"], prompt, 1, BoardColumn.Today,
            IsBlocked: false, ReopenTomorrow: true, IsArchived: false, LastSessionId: null,
            [], null, DateTimeOffset.UtcNow);

        var request = new WorkstreamLaunchRequest(workstream, "abc123", "session-1", mode,
            @"C:\Users\nigel\.local\bin\claude.exe",
            @"C:\Program Files\WindowsApps\WindowsTerminal.exe",
            @"C:\dev\git\Theseus\Theseus.Launcher.exe",
            "Theseus.Launch", "nigel-theseus");

        return WorkstreamLaunchPlanner.Plan(request, Expiry);
    }
}
