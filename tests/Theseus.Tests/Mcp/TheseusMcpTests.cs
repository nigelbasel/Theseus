using System.Text.Json.Nodes;
using Theseus.Core.Domain;
using Theseus.Core.Launching;
using Theseus.Core.Mcp;
using Theseus.Core.Terminal;

namespace Theseus.Tests.Mcp;

[TestFixture]
public class TheseusMcpTests
{
    private const string SessionId = "3f2b1c40-9d5e-4a10-8b77-2c9e1d4a55aa";

    [Test]
    public void ConfigJson_PointsTheSessionAtItsOwnEndpointOnLocalhost()
    {
        var config = JsonNode.Parse(TheseusMcp.ConfigJson(47813, SessionId))!;
        var server = config["mcpServers"]![TheseusMcp.ServerName]!;

        Assert.That(server["type"]!.GetValue<string>(), Is.EqualTo("http"));
        Assert.That(server["url"]!.GetValue<string>(), Is.EqualTo($"http://localhost:47813/mcp/{SessionId}"));
    }

    [Test]
    public void Build_PutsTheMcpConfigLastBecauseItAndAddDirAreBothVariadic()
    {
        var spec = new LaunchSpec(SessionId, LaunchMode.FirstRun, @"C:\claude.exe", @"C:\work", "Name", "Carry on.",
            [@"C:\work\Core"]) { McpConfig = """{"mcpServers":{}}""" };

        var argv = ClaudeArgumentBuilder.Build(spec).ToList();

        Assert.That(argv[^2], Is.EqualTo("--mcp-config"));
        Assert.That(argv[^1], Is.EqualTo("""{"mcpServers":{}}"""));
        Assert.That(argv.IndexOf("Carry on."), Is.LessThan(argv.IndexOf("--add-dir")));
        Assert.That(argv.IndexOf("--add-dir"), Is.LessThan(argv.IndexOf("--allowedTools")));
    }

    [Test]
    public void Build_AllowsExactlyTheTheseusToolsSoASessionIsNotAskedAboutThem()
    {
        var spec = new LaunchSpec(SessionId, LaunchMode.FirstRun, @"C:\claude.exe", @"C:\work", "Name", "Carry on.", [])
        {
            McpConfig = TheseusMcp.ConfigJson(47813, SessionId),
        };

        var argv = ClaudeArgumentBuilder.Build(spec).ToList();

        Assert.That(argv[argv.IndexOf("--allowedTools") + 1],
            Is.EqualTo("mcp__theseus__start_work,mcp__theseus__stop_work,mcp__theseus__time_for,mcp__theseus__whoami,mcp__theseus__locks,mcp__theseus__lock_acquire,mcp__theseus__lock_release,mcp__theseus__related,mcp__theseus__proposal_status"));
        Assert.That(argv[argv.IndexOf("--allowedTools") + 1], Does.Not.Contain("propose_workstream").And.Not.Contain("withdraw_proposal"),
            "a proposal puts something on the board, so the person in the tab approves each one");
        Assert.That(argv[argv.IndexOf("--allowedTools") + 1], Does.Not.Contain("report_back"),
            "a report is the one thing a session records on the board, so the person in the tab approves each one");
        Assert.That(argv[argv.IndexOf("--allowedTools") + 1], Does.Not.Contain("*"),
            "a tool added to the server later must not be approved without anyone deciding it should be");
    }

    [Test]
    public void Build_AllowsNoToolsWhenTheServerIsNotRunning()
    {
        var spec = new LaunchSpec(SessionId, LaunchMode.FirstRun, @"C:\claude.exe", @"C:\work", "Name", "Carry on.", []);

        Assert.That(ClaudeArgumentBuilder.Build(spec), Does.Not.Contain("--allowedTools"));
    }

    [Test]
    public void Build_LeavesOutMcpConfigWhenTheServerIsNotRunning()
    {
        var spec = new LaunchSpec(SessionId, LaunchMode.FirstRun, @"C:\claude.exe", @"C:\work", "Name", "Carry on.", []);

        Assert.That(ClaudeArgumentBuilder.Build(spec), Does.Not.Contain("--mcp-config"));
    }

    [Test]
    public void Plan_CarriesTheMcpConfigToTheSession()
    {
        var workstream = new Workstream("gdpr-mcp", "GDPR MCP", "#208DD7", ColorIsManual: true, @"C:\work", [], "Prompt.",
            1, "New", IsBlocked: false, ReopenTomorrow: true, IsArchived: false, SessionId, [], null, DateTimeOffset.UtcNow);
        var request = new WorkstreamLaunchRequest(workstream, "ticket", SessionId, LaunchMode.Resume, @"C:\claude.exe",
            @"C:\wt.exe", @"C:\Theseus.Launcher.exe", "Theseus.Launch", "theseus", "Carry on.",
            TheseusMcp.ConfigJson(47813, SessionId));

        var plan = WorkstreamLaunchPlanner.Plan(request, DateTimeOffset.UtcNow.AddMinutes(2));

        Assert.That(plan.Ticket.Spec.McpConfig, Is.EqualTo(TheseusMcp.ConfigJson(47813, SessionId)));
    }
}
