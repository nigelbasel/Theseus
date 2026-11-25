namespace Theseus.App.ViewModels;

/// <summary>
/// One colour Project properties offers (1.1d AC 40; review of PR 3, item 7): a colour another project already holds is
/// marked and names it, and can still be chosen.
/// </summary>
/// <param name="Colour">The colour, as <c>#rrggbb</c>.</param>
/// <param name="TakenBy">The titles of the other projects that hold it, or empty.</param>
public sealed record ColourSwatchViewModel(string Colour, string TakenBy)
{
    /// <summary>Whether another project holds it.</summary>
    public bool IsTaken => TakenBy.Length > 0;

    /// <summary>The swatch's tooltip.</summary>
    public string ToolTip => IsTaken ? $"{Colour}, used by {TakenBy}" : Colour;
}
