using System.Windows.Input;

namespace Theseus.App.ViewModels;

/// <summary>One colour of the tag palette in Options, Tags, as a swatch that gives the tag that colour (item 11).</summary>
/// <param name="ColourHex">The colour.</param>
/// <param name="Command">Gives the tag this colour.</param>
public sealed record TagColourChoice(string ColourHex, ICommand Command)
{
    /// <summary>What hovering the swatch says.</summary>
    public string ToolTip => $"Use {ColourHex} for this tag";
}
