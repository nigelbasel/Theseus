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
        var run = Run("contact-forms");

        _log.Append(run);

        Assert.That(_log.Read().Single(), Is.EqualTo(run));
    }

    [Test]
    public void Append_KeepsHistoryInTheOrderItHappened()
    {
        _log.Append(Run("contact-forms"));
        _log.Append(Run("job-queue"));
        _log.Append(Run("admin-console"));

        Assert.That(_log.Read().Select(r => r.WorkstreamKey),
            Is.EqualTo(new[] { "contact-forms", "job-queue", "admin-console" }));
    }

    [Test]
    public void Append_WritesOneLinePerRunSoTheFileStaysAppendOnly()
    {
        _log.Append(Run("contact-forms"));
        _log.Append(Run("job-queue"));

        Assert.That(File.ReadAllLines(_path), Has.Length.EqualTo(2));
    }

    [Test]
    public void Append_NeverRewritesWhatIsAlreadyOnDisk()
    {
        _log.Append(Run("contact-forms"));
        var first = File.ReadAllLines(_path)[0];

        _log.Append(Run("job-queue"));

        Assert.That(File.ReadAllLines(_path)[0], Is.EqualTo(first));
    }

    [Test]
    public void Read_SkipsALineItCannotUnderstandRatherThanLosingTheWholeHistory()
    {
        _log.Append(Run("contact-forms"));
        File.AppendAllText(_path, "{ not a run" + Environment.NewLine);
        _log.Append(Run("job-queue"));

        Assert.That(_log.Read().Select(r => r.WorkstreamKey),
            Is.EqualTo(new[] { "contact-forms", "job-queue" }));
    }

    [Test]
    public void Read_CarriesTheOutcomeFieldsARestartDecisionDependsOn()
    {
        _log.Append(Run("contact-forms") with
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
        _log.Append(Run("contact-forms") with { WorkingDirectory = @"C:\work\repo\features\contact-forms" });

        Assert.That(_log.Read().Single().WorkingDirectory, Is.EqualTo(@"C:\work\repo\features\contact-forms"));
    }

    [Test]
    public void Append_CreatesTheDirectoryOnFirstUse()
    {
        var nested = Path.Combine(_directory, "Theseus", "runs.jsonl");
        new RunLog(nested).Append(Run("contact-forms"));

        Assert.That(File.Exists(nested), Is.True);
    }

    private static SessionRun Run(string key) =>
        new(Guid.NewGuid(), "3f2b1c40-9d5e-4a10-8b77-2c9e1d4a55aa", key, @"C:\work\repo",
            @"C:\Users\dev\.claude\projects\C--work-repo\3f2b1c40.jsonl",
            SessionLaunchMode.FirstRun, "Carry on.", "Contact form validation", "#208DD7",
            new DateTimeOffset(2026, 8, 26, 9, 0, 0, TimeSpan.Zero), null, 4242, 8080, null,
            SessionExitReason.Unknown);
}
