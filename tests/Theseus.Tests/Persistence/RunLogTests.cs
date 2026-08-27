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

    private static SessionRun Run(string key = "contact-forms") =>
        new(Guid.NewGuid(), "3f2b1c40-9d5e-4a10-8b77-2c9e1d4a55aa", key, @"C:\work\repo",
            @"C:\Users\dev\.claude\projects\C--work-repo\3f2b1c40.jsonl",
            SessionLaunchMode.FirstRun, "Carry on.", "Contact form validation", "#208DD7",
            new DateTimeOffset(2026, 8, 26, 9, 0, 0, TimeSpan.Zero), null, 4242, 8080, null,
            SessionExitReason.Unknown);

    [Test]
    public void AppendEnd_LeavesTheStartLineExactlyAsItWasWritten()
    {
        var run = Run();
        _log.Append(run);

        var afterStart = File.ReadAllLines(_path)[0];

        _log.AppendEnd(new RunEnd(run.Id, DateTimeOffset.UtcNow, 0, SessionExitReason.ClosedCleanly));

        Assert.That(File.ReadAllLines(_path)[0], Is.EqualTo(afterStart),
            "recording an ending must never touch a byte already on disk");
    }

    [Test]
    public void Read_FoldsAnEndingOntoTheRunItBelongsTo()
    {
        var run = Run();
        var ended = DateTimeOffset.UtcNow;

        _log.Append(run);
        _log.AppendEnd(new RunEnd(run.Id, ended, 0, SessionExitReason.ClosedCleanly));

        var read = _log.Read().Single();

        Assert.That(read.EndedUtc, Is.EqualTo(ended));
        Assert.That(read.ExitCode, Is.Zero);
        Assert.That(read.ExitReason, Is.EqualTo(SessionExitReason.ClosedCleanly));
        Assert.That(read.HasEnded, Is.True);
    }

    [Test]
    public void Read_KeepsTheFirstEndingWhenARunIsEndedTwice()
    {
        var run = Run();
        _log.Append(run);
        _log.AppendEnd(new RunEnd(run.Id, DateTimeOffset.UtcNow, 0, SessionExitReason.ClosedCleanly));
        _log.AppendEnd(new RunEnd(run.Id, DateTimeOffset.UtcNow, null, SessionExitReason.CrashedOrKilled));

        Assert.That(_log.Read().Single().ExitReason, Is.EqualTo(SessionExitReason.ClosedCleanly),
            "a later, worse observation must not overwrite the closest one");
    }

    [Test]
    public void Read_FoldsAnEndingThatWasWrittenBeforeItsStartLine()
    {
        var run = Run();

        _log.AppendEnd(new RunEnd(run.Id, DateTimeOffset.UtcNow, 0, SessionExitReason.ClosedCleanly));
        _log.Append(run);

        Assert.That(_log.Read().Single().HasEnded, Is.True, "the join is by id, not by position");
    }

    [Test]
    public void Read_LeavesARunWithNoEndingUnfinished()
    {
        _log.Append(Run());

        var read = _log.Read().Single();

        Assert.That(read.HasEnded, Is.False);
        Assert.That(read.Duration, Is.Null);
    }

    [Test]
    public void Read_ReportsARunEndedWhileTheseusWasDownAsFinishedWithNoDuration()
    {
        var run = Run();
        _log.Append(run);
        _log.AppendEnd(new RunEnd(run.Id, null, null, SessionExitReason.TheseusWasDown));

        var read = _log.Read().Single();

        Assert.That(read.HasEnded, Is.True, "it must not be mistaken for a run still going");
        Assert.That(read.EndedUtc, Is.Null, "no time was observed, so none is invented");
        Assert.That(read.Duration, Is.Null, "and it can never enter a measurement");
    }

    [Test]
    public void Read_StillReadsTheRunsWrittenBeforeEndingsExisted()
    {
        _log.Append(Run());

        Assert.That(_log.Read(), Has.Count.EqualTo(1),
            "a line with no ending folds to itself, so existing records survive the format change");
    }

    [Test]
    public void Read_IgnoresAnEndingWhoseRunIsNotInTheLog()
    {
        _log.Append(Run());
        _log.AppendEnd(new RunEnd(Guid.NewGuid(), DateTimeOffset.UtcNow, 0, SessionExitReason.ClosedCleanly));

        Assert.That(_log.Read(), Has.Count.EqualTo(1), "an orphaned ending never fabricates a run");
    }

    [Test]
    public void Read_SkipsALineThatParsesAsJsonButCarriesNoRunIdentity()
    {
        _log.Append(Run());
        File.AppendAllText(_path, """{"something":"else"}""" + Environment.NewLine);

        Assert.That(_log.Read(), Has.Count.EqualTo(1),
            "valid JSON that is not a run must not deserialise into a record full of defaults");
    }
}
