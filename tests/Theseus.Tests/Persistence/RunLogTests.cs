using Theseus.Core.Domain;
using Theseus.Core.Persistence;

namespace Theseus.Tests.Persistence;

[TestFixture]
public class RunLogTests
{
    private string _directory = string.Empty;
    private string _path = string.Empty;
    private RunLog _log = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-runs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _path = Path.Combine(_directory, "runs.jsonl");
        _log = new RunLog(_path);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, recursive: true);

    [Test]
    public void Read_IsEmptyBeforeAnyRunIsRecorded()
    {
        Assert.That(_log.Read(), Is.Empty);
    }

    [Test]
    public void Append_RoundTripsARunThroughDisk()
    {
        var run = Run("gdpr-mcp");

        _log.Append(run);

        Assert.That(_log.Read().Single(), Is.EqualTo(run));
    }

    [Test]
    public void Append_KeepsHistoryInTheOrderItHappened()
    {
        _log.Append(Run("gdpr-mcp"));
        _log.Append(Run("legacy-jobs"));
        _log.Append(Run("identity-admin"));

        Assert.That(_log.Read().Select(r => r.WorkstreamKey),
            Is.EqualTo(new[] { "gdpr-mcp", "legacy-jobs", "identity-admin" }));
    }

    [Test]
    public void Append_WritesOneLinePerRunSoTheFileStaysAppendOnly()
    {
        _log.Append(Run("gdpr-mcp"));
        _log.Append(Run("legacy-jobs"));

        Assert.That(File.ReadAllLines(_path), Has.Length.EqualTo(2));
    }

    [Test]
    public void Append_NeverRewritesWhatIsAlreadyOnDisk()
    {
        _log.Append(Run("gdpr-mcp"));
        var first = File.ReadAllLines(_path)[0];

        _log.Append(Run("legacy-jobs"));

        Assert.That(File.ReadAllLines(_path)[0], Is.EqualTo(first));
    }

    [Test]
    public void Read_SkipsALineItCannotUnderstandRatherThanLosingTheWholeHistory()
    {
        _log.Append(Run("gdpr-mcp"));
        File.AppendAllText(_path, "{ not a run" + Environment.NewLine);
        _log.Append(Run("legacy-jobs"));

        Assert.That(_log.Read().Select(r => r.WorkstreamKey),
            Is.EqualTo(new[] { "gdpr-mcp", "legacy-jobs" }));
    }

    [Test]
    public void Read_CarriesTheOutcomeFieldsARestartDecisionDependsOn()
    {
        _log.Append(Run("gdpr-mcp") with
        {
            LaunchMode = SessionLaunchMode.RemintedAfterResumeFailure,
            ExitReason = SessionExitReason.CrashedOrKilled,
            ExitCode = 137,
            EndedUtc = new DateTimeOffset(2026, 8, 26, 17, 40, 0, TimeSpan.Zero),
        });

        var run = _log.Read().Single();
        Assert.That(run.LaunchMode, Is.EqualTo(SessionLaunchMode.RemintedAfterResumeFailure));
        Assert.That(run.ExitReason, Is.EqualTo(SessionExitReason.CrashedOrKilled));
        Assert.That(run.ExitCode, Is.EqualTo(137));
        Assert.That(run.EndedUtc, Is.Not.Null);
    }

    [Test]
    public void Append_KeepsTheWorkingDirectoryOfEachRunBecauseResumeIsScopedToIt()
    {
        _log.Append(Run("gdpr-mcp") with { WorkingDirectory = @"C:\dev\tfsgit\features\apps-2170" });

        Assert.That(_log.Read().Single().WorkingDirectory, Is.EqualTo(@"C:\dev\tfsgit\features\apps-2170"));
    }

    [Test]
    public void Append_CreatesTheDirectoryOnFirstUse()
    {
        var nested = Path.Combine(_directory, "Theseus", "runs.jsonl");
        new RunLog(nested).Append(Run("gdpr-mcp"));

        Assert.That(File.Exists(nested), Is.True);
    }

    private static SessionRun Run(string key) =>
        new(Guid.NewGuid(), "3f2b1c40-9d5e-4a10-8b77-2c9e1d4a55aa", key, @"C:\dev\tfsgit",
            @"C:\Users\nigel\.claude\projects\C--dev-tfsgit\3f2b1c40.jsonl",
            SessionLaunchMode.FirstRun, "Carry on.", "GDPR MCP contact forms", "#208DD7",
            new DateTimeOffset(2026, 8, 26, 9, 0, 0, TimeSpan.Zero), null, 4242, 8080, null,
            SessionExitReason.Unknown);
}
