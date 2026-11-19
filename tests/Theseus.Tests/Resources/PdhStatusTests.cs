using Theseus.Core.Resources;

namespace Theseus.Tests.Resources;

/// <summary>
/// Which PDH statuses cost one sample and which stop the reader. A warning-level status (0x8xxxxxxx) is about one
/// calculation: PDH_CALC_NEGATIVE_DENOMINATOR was seen in 4 of 300 back-to-back reads on 2026-11-19.
/// </summary>
[TestFixture]
public class PdhStatusTests
{
    [TestCase(0x800007D6u, true, Description = "PDH_CALC_NEGATIVE_DENOMINATOR")]
    [TestCase(0x800007D5u, true, Description = "a warning-level status")]
    [TestCase(0xC0000BC6u, false, Description = "PDH_INVALID_DATA, an error-level status")]
    [TestCase(0xC0000BB8u, false, Description = "PDH_CSTATUS_NO_OBJECT")]
    [TestCase(0u, false, Description = "success is not a status to skip")]
    public void OnlyWarningLevelStatuses_AreTemporary(uint status, bool temporary)
    {
        Assert.That(PdhStatus.IsTemporary(status), Is.EqualTo(temporary));
    }
}
