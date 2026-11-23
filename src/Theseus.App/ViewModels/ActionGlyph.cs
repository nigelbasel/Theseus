namespace Theseus.App.ViewModels;

/// <summary>
/// The Segoe Fluent Icons glyph of a box's action, by its label, so a box draws its actions as icons as the board's
/// toolbar does, with the words in the tooltip (walkthrough rows 24, 32; checklist G86 to G90: the same command has the
/// same glyph everywhere, and a standard glyph keeps its meaning). Null for a label with no clear icon, drawn as text.
/// </summary>
public static class ActionGlyph
{
    private static readonly (string Prefix, string Glyph)[] ByPrefix =
    [
        ("▶ Start", "\uE768"),
        ("Start anyway", "\uE768"),
        ("■ Stop", "\uE71A"),
        ("⧉ Focus", "\uE8A7"),
        ("Independent check", "\uE9D5"),
        ("Accept spec", "\uE8FB"),
        ("Open spec", "\uE8A5"),
        ("Open records", "\uE8E5"),
        ("Open ", "\uE8E5"),
        ("Archive", "\uE7B8"),
        ("Restore", "\uE777"),
        ("Remove from project", "\uE738"),
        ("Unlink from", "\uE71B"),
        ("Properties", "\uE946"),
        ("Add to project", "\uE710"),
        ("Set the holdout folder", "\uE8B7"),
        ("Write scenarios", "\uE70F"),
    ];

    /// <summary>The glyph for a label, or null.</summary>
    public static string? Of(string label)
    {
        ArgumentNullException.ThrowIfNull(label);

        return ByPrefix.FirstOrDefault(entry => label.StartsWith(entry.Prefix, StringComparison.Ordinal)).Glyph;
    }

    /// <summary>The label as a tooltip names it: without the ▶ ■ ⧉ marks a text button carried.</summary>
    public static string Name(string label)
    {
        ArgumentNullException.ThrowIfNull(label);

        return label.TrimStart('▶', '■', '⧉', ' ');
    }
}
