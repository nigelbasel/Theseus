using Theseus.Core.Abstractions;
using Theseus.Core.Domain;
using Theseus.Core.Launching;
using Theseus.Launcher;

namespace Theseus.Tests.Launching;

/// <summary>
/// A card's role reaches its session's hooks as an environment variable set on the claude process, which the session's
/// own commands cannot change (verifier-card AC 2; measured on 2026-11-19 that a hook sees it).
/// </summary>
[TestFixture]
public class LaunchEnvironmentTests
{
    private static readonly DateTimeOffset Expiry = new(2026, 11, 19, 18, 0, 0, TimeSpan.Zero);

    [Test]
    public void AVerifiersLaunch_SetsTheRoleVariable()
    {
        Assert.That(Plan(WorkstreamRole.Verifier).Ticket.Spec.Environment,
            Is.EqualTo(new Dictionary<string, string> { ["THESEUS_ROLE"] = "verifier" }));
    }

    [Test]
    public void ALaunchWithNoRole_SetsNoVariableAtAll()
    {
        Assert.That(Plan(WorkstreamRole.None).Ticket.Spec.Environment, Is.Empty,
            "a card with no role starts exactly as it always has");
    }

    [Test]
    public void TheLauncher_StartsClaudeWithTheSpecsVariables()
    {
        var launcher = new RecordingLauncher();
        var spec = new LaunchSpec("session-1", LaunchMode.FirstRun, @"C:\claude.cmd", @"C:\work\repo", "Verify",
            "Carry on.", [])
        {
            Environment = new Dictionary<string, string> { ["THESEUS_ROLE"] = "verifier" },
        };

        new ClaudeProcessRunner(launcher, new FixedIds(), new FixedClock()).Run(spec, new SilentReporter());

        Assert.That(launcher.Requests.Single().Environment,
            Is.EqualTo(new Dictionary<string, string> { ["THESEUS_ROLE"] = "verifier" }));
    }

    private static WorkstreamLaunchPlan Plan(WorkstreamRole role)
    {
        var workstream = new Workstream("checking", "Verify: something", "#C0392B", ColorIsManual: true,
            @"C:\holdout", [@"C:\work\repo"], "Carry on.", 1, "Today",
            IsBlocked: false, ReopenTomorrow: true, IsArchived: false, LastSessionId: null,
            [], null, DateTimeOffset.UtcNow)
        {
            Role = role,
        };

        var request = new WorkstreamLaunchRequest(workstream, "abc123", "session-1", LaunchMode.FirstRun,
            @"C:\claude.exe", @"C:\WindowsTerminal.exe", @"C:\Theseus.Launcher.exe", "Theseus.Launch", "theseus",
            "Carry on.");

        return WorkstreamLaunchPlanner.Plan(request, Expiry);
    }

    private sealed class RecordingLauncher : IProcessLauncher
    {
        public List<ProcessStartRequest> Requests { get; } = [];

        public IProcessHandle Start(ProcessStartRequest request)
        {
            Requests.Add(request);

            return new ExitedHandle();
        }
    }

    private sealed class ExitedHandle : IProcessHandle
    {
        public int ProcessId => 1000;

        public int WaitForExit() => 0;

        public void Dispose()
        {
        }
    }

    private sealed class FixedIds : ISessionIdFactory
    {
        public string Create() => "minted";
    }

    private sealed class FixedClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Expiry;
    }

    private sealed class SilentReporter : ILaunchReporter
    {
        public void Started(string sessionId, int claudeProcessId)
        {
        }

        public void Reminted(string previousSessionId, string newSessionId, string reason)
        {
        }

        public void Exited(string sessionId, int exitCode, long durationMs)
        {
        }
    }
}
