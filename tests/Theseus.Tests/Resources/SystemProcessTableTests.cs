using System.Diagnostics;
using Theseus.Core.Resources;

namespace Theseus.Tests.Resources;

/// <summary>The real process table, read on this machine.</summary>
[TestFixture]
public class SystemProcessTableTests
{
    [Test]
    public void Capture_FindsThisProcess_WithItsParentNameAndCounters()
    {
        using var self = Process.GetCurrentProcess();

        var table = SystemProcessTable.Capture();
        var entry = table.Processes.Single(process => process.ProcessId == self.Id);

        Assert.That(entry.Name, Is.EqualTo(self.ProcessName).IgnoreCase);
        Assert.That(entry.CpuTicks, Is.GreaterThan(0));
        Assert.That(entry.PrivateBytes, Is.GreaterThan(0));
        Assert.That(entry.IoOperations, Is.GreaterThan(0));
        Assert.That(entry.CreateTime, Is.EqualTo(self.StartTime.ToFileTimeUtc()).Within(TimeSpan.TicksPerSecond));
        Assert.That(table.Processes.Any(process => process.ProcessId == entry.ParentProcessId), Is.True);
        Assert.That(table.BusyAndIdleTicks, Is.GreaterThan(table.IdleTicks));
    }
}
