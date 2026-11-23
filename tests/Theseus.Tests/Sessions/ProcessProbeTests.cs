using Theseus.Core.Sessions;

namespace Theseus.Tests.Sessions;

[TestFixture]
public class ProcessProbeTests
{
    [Test]
    public void GetStartFileTime_ReadsThisProcess()
    {
        Assert.That(new ProcessProbe().GetStartFileTime(Environment.ProcessId), Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void GetStartFileTime_ReturnsNothingForAProcessIdNobodyHolds()
    {
        Assert.That(new ProcessProbe().GetStartFileTime(int.MaxValue - 1), Is.Null);
    }

    [Test]
    public void GetStartFileTime_ReturnsNothingForAProcessThatHasExitedButIsStillHeld()
    {
        using var held = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
            Path.Combine(Environment.SystemDirectory, "cmd.exe"), "/c exit 0") { UseShellExecute = false, CreateNoWindow = true })!;
        held.WaitForExit();

        Assert.That(new ProcessProbe().GetStartFileTime(held.Id), Is.Null,
            "git-22, 2026-11-23: an exited process still held open was logged every five seconds");
    }

    [Test]
    public void AGoneProcess_IsSaidOnce()
    {
        var told = new OnceEachProcess();

        Assert.That(told.First(7232), Is.True);
        Assert.That(told.First(7232), Is.False, "the log was flooded with the same line every refresh");
        Assert.That(told.First(7233), Is.True);
    }

    [Test]
    public void GetStartFileTime_DoesNotThrowForAProcessThisUserCannotOpen()
    {
        const int systemProcess = 4;

        Assert.DoesNotThrow(() => new ProcessProbe().GetStartFileTime(systemProcess),
            "a registry pid reused by a protected process took the whole board down on 2026-09-30");
    }
}
