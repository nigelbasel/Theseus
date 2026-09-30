using Theseus.Core.Abstractions;
using Theseus.Core.Launching;
using Theseus.Launcher;

namespace Theseus.Tests.Launcher;

[TestFixture]
public class ClaudeProcessRunnerTests
{
    private const string SessionId = "3f2b1c40-9d5e-4a10-8b77-2c9e1d4a55aa";
    private const string MintedId = "minted-0001";

    [Test]
    public void Run_StartsClaudeWithTheArgumentVectorTheSpecDescribes()
    {
        var launcher = new FakeProcessLauncher(exitCodes: [0]);
        Run(launcher, Spec(addDirectories: [@"C:\work\repo\Core"]));

        var request = launcher.Requests.Single();
        Assert.That(request.ExecutablePath, Is.EqualTo(@"C:\claude.cmd"));
        Assert.That(request.WorkingDirectory, Is.EqualTo(@"C:\work\repo"));
        Assert.That(request.Arguments, Is.EqualTo(new[]
        {
            "--session-id", SessionId, "-n", "Contact form validation",
            "Carry on.", "--add-dir", @"C:\work\repo\Core",
        }));
    }

    [Test]
    public void Run_ReportsTheClaudeProcessIdAsSoonAsItStarts()
    {
        var reporter = new RecordingReporter();
        Run(new FakeProcessLauncher(exitCodes: [0], processIds: [8080]), Spec(), reporter);

        Assert.That(reporter.Started, Is.EqualTo(new[] { (SessionId, 8080) }));
    }

    [Test]
    public void Run_ReportsTheExitCodeAndDuration()
    {
        var reporter = new RecordingReporter();
        var clock = new StubClock(TimeSpan.FromSeconds(30));
        Run(new FakeProcessLauncher(exitCodes: [0]), Spec(), reporter, clock);

        var (sessionId, exitCode, durationMs) = reporter.Exited.Single();
        Assert.That(sessionId, Is.EqualTo(SessionId));
        Assert.That(exitCode, Is.Zero);
        Assert.That(durationMs, Is.EqualTo(30_000));
    }

    [Test]
    public void Run_HoldsTheTabOnAnyNonZeroExit()
    {
        var result = Run(new FakeProcessLauncher(exitCodes: [1]), Spec());

        Assert.That(result.HoldTab, Is.True);
        Assert.That(result.ExitCode, Is.EqualTo(1));
    }

    [Test]
    public void Run_LetsTheTabCloseOnACleanExit()
    {
        Assert.That(Run(new FakeProcessLauncher(exitCodes: [0]), Spec()).HoldTab, Is.False);
    }

    [Test]
    public void Run_RemintsWhenAResumeDiesImmediately()
    {
        var launcher = new FakeProcessLauncher(exitCodes: [1, 0]);
        var result = Run(launcher, Spec(LaunchMode.Resume), clock: new StubClock(TimeSpan.FromSeconds(1)));

        Assert.That(launcher.Requests, Has.Count.EqualTo(2));
        Assert.That(launcher.Requests[1].Arguments[0], Is.EqualTo("--session-id"));
        Assert.That(launcher.Requests[1].Arguments[1], Is.EqualTo(MintedId));
        Assert.That(result.SessionId, Is.EqualTo(MintedId));
        Assert.That(result.ExitCode, Is.Zero);
        Assert.That(result.HoldTab, Is.False);
    }

    [Test]
    public void Run_ReportsTheRemintSoTheApplicationCanRewriteThePlan()
    {
        var reporter = new RecordingReporter();
        Run(new FakeProcessLauncher(exitCodes: [1, 0]), Spec(LaunchMode.Resume), reporter,
            new StubClock(TimeSpan.FromSeconds(1)));

        var (previous, minted, _) = reporter.Reminted.Single();
        Assert.That(previous, Is.EqualTo(SessionId));
        Assert.That(minted, Is.EqualTo(MintedId));
    }

    [Test]
    public void Run_DoesNotRemintWhenAResumeRanLongEnoughToBeRealWork()
    {
        var launcher = new FakeProcessLauncher(exitCodes: [1]);
        var result = Run(launcher, Spec(LaunchMode.Resume), clock: new StubClock(TimeSpan.FromSeconds(600)));

        Assert.That(launcher.Requests, Has.Count.EqualTo(1));
        Assert.That(result.SessionId, Is.EqualTo(SessionId));
    }

    [Test]
    public void Run_DoesNotRemintAFirstRunBecauseThereIsNoStaleIdentifierToBlame()
    {
        var launcher = new FakeProcessLauncher(exitCodes: [1]);
        Run(launcher, Spec(), clock: new StubClock(TimeSpan.FromSeconds(1)));

        Assert.That(launcher.Requests, Has.Count.EqualTo(1));
    }

    [Test]
    public void Run_RemintsAtMostOnceSoAFailingExecutableCannotLoop()
    {
        var launcher = new FakeProcessLauncher(exitCodes: [1, 1]);
        var result = Run(launcher, Spec(LaunchMode.Resume), clock: new StubClock(TimeSpan.FromSeconds(1)));

        Assert.That(launcher.Requests, Has.Count.EqualTo(2));
        Assert.That(result.HoldTab, Is.True);
    }

    [Test]
    public void Run_OpensARemintedSessionWithTheFullOpeningRatherThanTheResumeLine()
    {
        var launcher = new FakeProcessLauncher(exitCodes: [1, 0]);
        var spec = Spec(LaunchMode.Resume) with { Prompt = "Resumed by Theseus.", PromptIfReminted = "You are the workstream. Read the handoff." };

        Run(launcher, spec, clock: new StubClock(TimeSpan.FromSeconds(1)));

        Assert.That(launcher.Requests[0].Arguments, Does.Contain("Resumed by Theseus."));
        Assert.That(launcher.Requests[1].Arguments, Does.Contain("You are the workstream. Read the handoff."),
            "a reminted session has no conversation to carry on from");
        Assert.That(launcher.Requests[1].Arguments, Does.Not.Contain("Resumed by Theseus."));
    }

    [Test]
    public void Run_OpensARemintedSessionWithTheSamePromptWhenNoOtherWasGiven()
    {
        var launcher = new FakeProcessLauncher(exitCodes: [1, 0]);

        Run(launcher, Spec(LaunchMode.Resume), clock: new StubClock(TimeSpan.FromSeconds(1)));

        Assert.That(launcher.Requests[1].Arguments, Does.Contain("Carry on."));
    }

    [Test]
    public void Run_ReportsTheExitUnderTheRemintedIdentifierNotTheAbandonedOne()
    {
        var reporter = new RecordingReporter();
        Run(new FakeProcessLauncher(exitCodes: [1, 0]), Spec(LaunchMode.Resume), reporter,
            new StubClock(TimeSpan.FromSeconds(1)));

        Assert.That(reporter.Exited.Single().SessionId, Is.EqualTo(MintedId));
    }

    private static ClaudeRunResult Run(
        FakeProcessLauncher launcher,
        LaunchSpec spec,
        ILaunchReporter? reporter = null,
        StubClock? clock = null) =>
        new ClaudeProcessRunner(launcher, new StubSessionIdFactory(), clock ?? new StubClock(TimeSpan.Zero))
            .Run(spec, reporter ?? new RecordingReporter());

    private static LaunchSpec Spec(
        LaunchMode mode = LaunchMode.FirstRun,
        IReadOnlyList<string>? addDirectories = null) =>
        new(SessionId, mode, @"C:\claude.cmd", @"C:\work\repo", "Contact form validation",
            "Carry on.", addDirectories ?? []);

    private sealed class FakeProcessLauncher : IProcessLauncher
    {
        private readonly Queue<int> _exitCodes;
        private readonly Queue<int> _processIds;

        public FakeProcessLauncher(IEnumerable<int> exitCodes, IEnumerable<int>? processIds = null)
        {
            _exitCodes = new Queue<int>(exitCodes);
            _processIds = new Queue<int>(processIds ?? [1000, 1001, 1002]);
        }

        public List<ProcessStartRequest> Requests { get; } = [];

        public IProcessHandle Start(ProcessStartRequest request)
        {
            Requests.Add(request);
            return new FakeProcessHandle(_processIds.Dequeue(), _exitCodes.Dequeue());
        }
    }

    private sealed class FakeProcessHandle : IProcessHandle
    {
        private readonly int _exitCode;

        public FakeProcessHandle(int processId, int exitCode)
        {
            ProcessId = processId;
            _exitCode = exitCode;
        }

        public int ProcessId { get; }

        public int WaitForExit() => _exitCode;

        public void Dispose()
        {
        }
    }

    private sealed class StubClock : ISystemClock
    {
        private readonly TimeSpan _step;
        private DateTimeOffset _now = new(2026, 8, 26, 9, 0, 0, TimeSpan.Zero);
        private bool _first = true;

        public StubClock(TimeSpan step) => _step = step;

        public DateTimeOffset UtcNow
        {
            get
            {
                if (_first)
                {
                    _first = false;
                    return _now;
                }

                _now = _now.Add(_step);
                return _now;
            }
        }
    }

    private sealed class StubSessionIdFactory : ISessionIdFactory
    {
        private int _next;

        public string Create() => $"minted-{++_next:D4}";
    }

    private sealed class RecordingReporter : ILaunchReporter
    {
        public List<(string SessionId, int ProcessId)> Started { get; } = [];

        public List<(string Previous, string Minted, string Reason)> Reminted { get; } = [];

        public List<(string SessionId, int ExitCode, long DurationMs)> Exited { get; } = [];

        void ILaunchReporter.Started(string sessionId, int claudeProcessId) =>
            Started.Add((sessionId, claudeProcessId));

        void ILaunchReporter.Reminted(string previousSessionId, string newSessionId, string reason) =>
            Reminted.Add((previousSessionId, newSessionId, reason));

        void ILaunchReporter.Exited(string sessionId, int exitCode, long durationMs) =>
            Exited.Add((sessionId, exitCode, durationMs));
    }
}
