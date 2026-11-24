namespace Theseus.Core.Settings;

/// <summary>One of the headless runs' limits as Options shows and changes it.</summary>
/// <param name="Label">Its name.</param>
/// <param name="Explanation">What it does, and its default.</param>
/// <param name="Show">Its value as shown.</param>
/// <param name="Apply">The settings with a typed value applied, or null when the value is not a valid one.</param>
public sealed record HeadlessLimitSetting(
    string Label,
    string Explanation,
    Func<TheseusSettings, string> Show,
    Func<TheseusSettings, string, TheseusSettings?> Apply);
