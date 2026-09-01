namespace Theseus.Core.Layout;

/// <summary>How a set of windows should be placed.</summary>
public enum ArrangementKind
{
    /// <summary>Side by side in a grid, none overlapping, filling the area.</summary>
    Tile,

    /// <summary>Overlapping and offset, so every title bar stays clickable.</summary>
    Cascade,

    /// <summary>Every window the full area, one on top of another.</summary>
    Stack,
}
