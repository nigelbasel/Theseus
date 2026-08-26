namespace Theseus.Core.Colour;

/// <summary>
/// The fixed set of terminal tab colours a workstream can be given. Every entry sits on one
/// perceptual lightness band so Windows Terminal renders tab text legibly against all of them.
/// </summary>
public static class TabColourPalette
{
    /// <summary>The OKLab lightness every palette entry is built to.</summary>
    public const double Lightness = 0.62;

    /// <summary>The palette, ordered by hue.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        "#CF5D61",
        "#CC6437",
        "#BE7100",
        "#A68008",
        "#8C8C01",
        "#639831",
        "#1D9F5E",
        "#0A9C85",
        "#03999F",
        "#0095B8",
        "#208DD7",
        "#6080DD",
        "#8674D6",
        "#A469C4",
        "#BA61A8",
        "#C85C87",
    ];

    /// <summary>Whether a colour belongs to the palette, ignoring case.</summary>
    public static bool Contains(string hex) =>
        All.Any(c => string.Equals(c, hex, StringComparison.OrdinalIgnoreCase));

    /// <summary>The palette position of a colour, or -1 when it is not a palette colour.</summary>
    public static int IndexOf(string hex)
    {
        for (var n = 0; n < All.Count; n++)
        {
            if (string.Equals(All[n], hex, StringComparison.OrdinalIgnoreCase))
            {
                return n;
            }
        }

        return -1;
    }
}
