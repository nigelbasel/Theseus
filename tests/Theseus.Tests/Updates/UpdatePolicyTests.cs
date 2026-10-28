using Theseus.Core.Diagnostics;
using Theseus.Core.Updates;

namespace Theseus.Tests.Updates;

[TestFixture]
public class UpdatePolicyTests
{
    [TestCase("2026.10.28.20000", "2026.10.28.20001", true)]
    [TestCase("2026.10.28.20000", "2026.10.29.100", true)]
    [TestCase("2026.10.28.20000", "2026.10.28.20000", false)]
    [TestCase("2026.10.29.100", "2026.10.28.20000", false)]
    public void IsNewer_ComparesBuildTimes(string current, string candidate, bool newer)
    {
        Assert.That(UpdatePolicy.IsNewer(current, candidate), Is.EqualTo(newer));
    }

    [Test]
    public void IsNewer_NeverOffersAnUpdateToALocalDebugBuild()
    {
        Assert.That(UpdatePolicy.IsNewer(BuildStamp.Unstamped, "2026.10.29.100"), Is.False);
    }

    [TestCase(null)]
    [TestCase("nightly")]
    public void IsNewer_IgnoresACandidateThatIsNotAStamp(string? candidate)
    {
        Assert.That(UpdatePolicy.IsNewer("2026.10.28.20000", candidate), Is.False);
    }
}
