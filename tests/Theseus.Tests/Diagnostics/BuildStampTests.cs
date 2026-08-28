using Theseus.Core.Diagnostics;

namespace Theseus.Tests.Diagnostics;

[TestFixture]
public class BuildStampTests
{
    [Test]
    public void BuiltAt_ReadsTheMomentBackOutOfAStamp()
    {
        // 11:53:20 UTC is 42800 seconds, which the fourth field carries halved.
        Assert.That(BuildStamp.BuiltAt("2026.08.28.21400"),
            Is.EqualTo(new DateTimeOffset(2026, 8, 28, 11, 53, 20, TimeSpan.Zero)));
    }

    [Test]
    public void BuiltAt_ReadsAStampFromLateInTheDay()
    {
        Assert.That(BuildStamp.BuiltAt("2026.08.28.43199"),
            Is.EqualTo(new DateTimeOffset(2026, 8, 28, 23, 59, 58, TimeSpan.Zero)),
            "halving is what keeps a whole day inside a 16-bit version field");
    }

    [Test]
    public void BuiltAt_RefusesAVersionThatIsNotOneOfOurs()
    {
        Assert.Multiple(() =>
        {
            Assert.That(BuildStamp.BuiltAt("1.0.0.0"), Is.Null);
            Assert.That(BuildStamp.BuiltAt("2026.08.28"), Is.Null);
            Assert.That(BuildStamp.BuiltAt("not.a.version.at.all"), Is.Null);
            Assert.That(BuildStamp.BuiltAt("2026.13.28.100"), Is.Null, "there is no thirteenth month");
            Assert.That(BuildStamp.BuiltAt("2026.08.28.99999"), Is.Null, "more halves than a day holds");
            Assert.That(BuildStamp.BuiltAt(null), Is.Null);
        });
    }

    [Test]
    public void Format_DropsTheCommitHashASourceLinkedBuildAppends()
    {
        Assert.That(BuildStamp.Format("2026.08.28.21400+0f3c1ae9"), Is.EqualTo("2026.08.28.21400"),
            "the hash is not what anybody is comparing against the copy they just installed");
    }

    [Test]
    public void Format_IsEmptyWhenThereIsNothingToShow()
    {
        Assert.That(BuildStamp.Format(null), Is.Empty);
        Assert.That(BuildStamp.Format("   "), Is.Empty);
    }

    [Test]
    public void Describe_SpellsOutTheBuildTime()
    {
        Assert.That(BuildStamp.Describe("2026.08.28.21400"),
            Is.EqualTo("2026.08.28.21400 — built 2026-08-28 11:53 UTC"));
    }

    [Test]
    public void Describe_SaysWhyALocalBuildCarriesNoTime()
    {
        Assert.That(BuildStamp.Describe(BuildStamp.Unstamped), Does.Contain("local build"),
            "Debug builds are deliberately not stamped, and that is worth saying rather than hiding");
    }

    [Test]
    public void Describe_FallsBackToTheVersionAloneWhenItIsNotOneOfOurs()
    {
        Assert.That(BuildStamp.Describe("3.2.1.0"), Is.EqualTo("3.2.1.0"));
    }
}
