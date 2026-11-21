namespace Theseus.Core.Colour;

/// <summary>
/// The colours a project can be given (decision 7, 2026-11-21): a palette of its own, none of them a workstream colour,
/// so a project card never shares a colour with a card. Deeper than the workstream band, each reads as a stripe and a mark
/// on the light and the dark board alike.
/// </summary>
public static class ProjectColourPalette
{
    /// <summary>The palette, ordered by hue.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        "#9B2C3C",
        "#A0521A",
        "#7A6A00",
        "#2F7D32",
        "#00796B",
        "#1F5FA8",
        "#5E4BB0",
        "#8E3A86",
    ];

    /// <summary>Whether a colour belongs to the palette, ignoring case.</summary>
    public static bool Contains(string hex) =>
        All.Any(colour => string.Equals(colour, hex, StringComparison.OrdinalIgnoreCase));
}
