using System.Windows.Input;

namespace Theseus.App.ViewModels;

/// <summary>
/// A tag on a card's face (card-search-and-tags item 11): a small rounded label in the tag's colour. Clicking it filters
/// the board by that tag (item 12).
/// </summary>
/// <param name="Tag">The tag.</param>
/// <param name="ColourHex">Its colour.</param>
/// <param name="FilterCommand">Ticks it in the Tags list.</param>
public sealed record TagChipViewModel(string Tag, string ColourHex, ICommand FilterCommand)
{
    /// <summary>What the label says.</summary>
    public string Label => "#" + Tag;

    /// <summary>What hovering it says.</summary>
    public string ToolTip => $"Show only cards tagged #{Tag}";
}
