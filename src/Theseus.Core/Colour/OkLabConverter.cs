using System.Globalization;

namespace Theseus.Core.Colour;

/// <summary>Converts sRGB hex colours to OKLab and measures perceptual distance between them.</summary>
public static class OkLabConverter
{
    /// <summary>Converts an sRGB hex colour to OKLab.</summary>
    /// <param name="hex">A six-digit hex colour, with or without a leading hash.</param>
    /// <exception cref="FormatException">The value is not a six-digit hex colour.</exception>
    public static OkLabColour FromHex(string hex)
    {
        var value = Normalise(hex);

        var r = ToLinear(Channel(value, 0));
        var g = ToLinear(Channel(value, 2));
        var b = ToLinear(Channel(value, 4));

        var longWave = Math.Cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
        var mediumWave = Math.Cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
        var shortWave = Math.Cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);

        return new OkLabColour(
            0.2104542553 * longWave + 0.7936177850 * mediumWave - 0.0040720468 * shortWave,
            1.9779984951 * longWave - 2.4285922050 * mediumWave + 0.4505937099 * shortWave,
            0.0259040371 * longWave + 0.7827717662 * mediumWave - 0.8086757660 * shortWave);
    }

    /// <summary>Euclidean distance in OKLab, a perceptually uniform measure of how different two colours look.</summary>
    public static double Distance(OkLabColour first, OkLabColour second)
    {
        var deltaL = first.L - second.L;
        var deltaA = first.A - second.A;
        var deltaB = first.B - second.B;

        return Math.Sqrt(deltaL * deltaL + deltaA * deltaA + deltaB * deltaB);
    }

    private static string Normalise(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            throw new FormatException("A colour is required.");
        }

        var value = hex.Trim();
        if (value.StartsWith('#'))
        {
            value = value[1..];
        }

        if (value.Length != 6 || !value.All(Uri.IsHexDigit))
        {
            throw new FormatException($"'{hex}' is not a six-digit hex colour.");
        }

        return value;
    }

    private static double Channel(string value, int offset) =>
        int.Parse(value.AsSpan(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d;

    private static double ToLinear(double channel) =>
        channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
}
