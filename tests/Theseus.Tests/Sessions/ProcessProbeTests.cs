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
    public void GetStartFileTime_DoesNotThrowForAProcessThisUserCannotOpen()
    {
        const int systemProcess = 4;

        Assert.DoesNotThrow(() => new ProcessProbe().GetStartFileTime(systemProcess),
            "a registry pid reused by a protected process took the whole board down on 2026-09-30");
    }
}
