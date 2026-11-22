using System.Windows.Input;

namespace Theseus.App.ViewModels;

/// <summary>One item of a card menu's Tags › (card-search-and-tags item 10): a tag with its tick, or New tag….</summary>
/// <param name="Header">The tag, or "New tag…".</param>
/// <param name="IsChecked">Whether every card it acts on has the tag.</param>
/// <param name="ColourHex">The tag's colour, shown as a dot beside it; null for New tag….</param>
/// <param name="Command">Puts the tag on, takes it off, or asks for a new one.</param>
/// <param name="ToolTip">What choosing it does, and to how many cards.</param>
public sealed record TagMenuItemViewModel(string Header, bool IsChecked, string? ColourHex, ICommand Command, string ToolTip)
{
    /// <summary>Whether it is a tag, with a colour dot, rather than New tag….</summary>
    public bool IsTag => ColourHex is not null;
}
