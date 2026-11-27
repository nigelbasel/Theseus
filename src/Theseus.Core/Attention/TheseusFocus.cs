namespace Theseus.Core.Attention;

/// <summary>
/// What Theseus's own window in front was showing, with someone at the keyboard (7.3). Attended time had nearly stopped
/// being recorded because the owner now works in Theseus's windows, which a sampler looking only at the terminal never
/// saw: in ISO week 48, 4 of his 170 board actions fell inside an attended interval.
/// </summary>
/// <param name="Card">The card it showed: the selected card, or the card a run's output window belongs to; null for none.</param>
/// <param name="Project">The project whose view was shown, or null.</param>
public sealed record TheseusFocus(string? Card, string? Project);
