namespace Theseus.Core.Layout;

/// <summary>
/// Where a window was, remembered between runs.
/// </summary>
/// <param name="Bounds">Its position and size when it was not maximised.</param>
/// <param name="IsMaximised">
/// Whether it was maximised. Kept apart from the bounds, which record where it would go on being
/// restored - a maximised window whose bounds were its maximised size would fill the screen for ever.
/// </param>
public sealed record WindowPlacement(LayoutRect Bounds, bool IsMaximised);
