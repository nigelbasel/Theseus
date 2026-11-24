namespace Theseus.App.ViewModels;

/// <summary>
/// The chevrons of everything that folds in the project view, in the icon font (baseline W25, finding C2; checklist G87,
/// G88): E76C folded, E70D open, used for nothing else. A text ▸ or ▾ drew at a different size in every font.
/// </summary>
public static class FoldGlyph
{
    /// <summary>ChevronDown: it is open.</summary>
    public const string Open = "";

    /// <summary>ChevronRight: it is folded.</summary>
    public const string Folded = "";

    /// <summary>The chevron for a fold as it is now.</summary>
    public static string Of(bool isOpen) => isOpen ? Open : Folded;
}
