using Theseus.Core.Attention;

namespace Theseus.Tests.Attention;

[TestFixture]
public class AttentionOffsetTests
{
    [TestCase(2, "+02:00")]
    [TestCase(0, "+00:00")]
    [TestCase(-5, "-05:00")]
    public void Format_WritesTheOffsetWithItsSign(int hours, string written)
    {
        Assert.That(AttentionOffset.Format(TimeSpan.FromHours(hours)), Is.EqualTo(written));
        Assert.That(AttentionOffset.Parse(written), Is.EqualTo(TimeSpan.FromHours(hours)));
    }

    [Test]
    public void Parse_ReadsNothingFromNoOffset() => Assert.That(AttentionOffset.Parse(null), Is.Null);

    [Test]
    public void DayOf_FollowsTheStampedOffset_NotTheReadersZone()
    {
        // 23:30 UTC on the 26th is the 27th in SAST, where the line was written; a reader in UTC must still say the 27th.
        var line = new AttentionInterval(AttentionKind.Machine, "a", new DateTimeOffset(2026, 11, 26, 23, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 26, 23, 45, 0, TimeSpan.Zero)) { Offset = "+02:00" };

        Assert.That(AttentionOffset.DayOf(line, TimeZoneInfo.Utc), Is.EqualTo(new DateOnly(2026, 11, 27)));
    }

    [Test]
    public void DayOf_ReadsALineFromBefore73InTheFallbackZone()
    {
        var line = new AttentionInterval(AttentionKind.Machine, "a", new DateTimeOffset(2026, 11, 26, 23, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 26, 23, 45, 0, TimeSpan.Zero));

        Assert.That(AttentionOffset.DayOf(line, TimeZoneInfo.Utc), Is.EqualTo(new DateOnly(2026, 11, 26)));
    }
}
