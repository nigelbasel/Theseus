namespace Theseus.Core.Resources;

/// <summary>
/// What a PDH status means for the reader. PDH statuses carry their severity in the top two bits: warnings
/// (0x8xxxxxxx), such as PDH_CALC_NEGATIVE_DENOMINATOR, are about one calculation and pass; errors (0xCxxxxxxx) are
/// not. Seen on 2026-11-19: 4 of 300 back-to-back reads gave that warning, and treating it as a failure turned the
/// cores-held-back warning off for the rest of the board's run (parked-cores pass 3).
/// </summary>
public static class PdhStatus
{
    private const uint SeverityMask = 0xC0000000;
    private const uint Warning = 0x80000000;

    /// <summary>Whether this status is temporary: skip the sample and read again next time.</summary>
    public static bool IsTemporary(uint status) => (status & SeverityMask) == Warning;
}
