using Theseus.Core.Settings;

namespace Theseus.App.ViewModels;

/// <summary>A theme as Options offers it, named in words rather than by the enum (D2, W17; checklist G142).</summary>
/// <param name="Theme">The theme.</param>
/// <param name="Label">What the choice says, such as "Use system setting".</param>
public sealed record ThemeChoice(AppTheme Theme, string Label)
{
    /// <summary>What a screen reader says for the item: the label the box shows, not the record (W50, G170).</summary>
    public override string ToString() => Label;
}
