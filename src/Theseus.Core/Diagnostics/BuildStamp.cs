using System.Globalization;

namespace Theseus.Core.Diagnostics;

/// <summary>
/// Reads a version stamp back into something a person can check against the build they just made.
///
/// The scheme puts the UTC date in the first three fields and seconds-since-midnight divided by two
/// in the fourth, so a stamp carries the moment it was built and not merely an ordering. That is the
/// fact worth showing: "is the installed copy the one I built five minutes ago" is the only question
/// anybody asks a version number here, and a bare four-part number answers it only if you happen to
/// remember the last one.
/// </summary>
public static class BuildStamp
{
    /// <summary>What a Debug build carries, having deliberately not been stamped.</summary>
    public const string Unstamped = "1.0.0.0";

    /// <summary>The version as it should appear, or an empty string when there is nothing to show.</summary>
    public static string Format(string? version) =>
        string.IsNullOrWhiteSpace(version) ? string.Empty : Trimmed(version);

    /// <summary>
    /// The version with its build time spelled out, for a tooltip. Falls back to the version alone
    /// when the stamp is not one of ours - a hand-set version is still worth showing, just not
    /// worth inventing a build time for.
    /// </summary>
    public static string Describe(string? version)
    {
        var shown = Format(version);

        if (shown.Length == 0)
        {
            return string.Empty;
        }

        if (string.Equals(shown, Unstamped, StringComparison.Ordinal))
        {
            return $"{shown} — a local build, which is not stamped with a build time";
        }

        return BuiltAt(shown) is { } built
            ? $"{shown} — built {built:yyyy-MM-dd HH:mm} UTC"
            : shown;
    }

    /// <summary>
    /// When a stamped build was made, or null when the version is not one of ours. The fourth field
    /// counts two-second intervals since midnight UTC, because every field of a Windows version
    /// resource is a 16-bit integer and a raw second count overflows part-way through the afternoon.
    /// </summary>
    public static DateTimeOffset? BuiltAt(string? version)
    {
        var parts = Format(version).Split('.');

        if (parts.Length != 4
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var year)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var month)
            || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var day)
            || !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var halves))
        {
            return null;
        }

        if (year < 2000 || month is < 1 or > 12 || day is < 1 or > 31 || halves is < 0 or > 43199)
        {
            return null;
        }

        try
        {
            return new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero).AddSeconds(halves * 2);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>
    /// The version without the build metadata a source-linked build appends. InformationalVersion
    /// arrives as "2026.08.28.21140+0f3c1a…" when the repository is being tracked, and the hash is
    /// not what anybody is comparing.
    /// </summary>
    private static string Trimmed(string version)
    {
        var trimmed = version.Trim();
        var plus = trimmed.IndexOf('+', StringComparison.Ordinal);

        return plus >= 0 ? trimmed[..plus] : trimmed;
    }
}
