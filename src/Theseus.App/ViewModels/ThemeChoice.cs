using Theseus.Core.Settings;

namespace Theseus.App.ViewModels;

/// <summary>A theme as Options offers it, named in words rather than by the enum (D2, W17; checklist G142).</summary>
/// <param name="Theme">The theme.</param>
/// <param name="Label">What the choice says, such as "Use system setting".</param>
public sealed record ThemeChoice(AppTheme Theme, string Label);
