namespace Theseus.Tests.Conventions;

/// <summary>
/// One break of a convention found by <see cref="XamlConventionTests"/>: the rule, the value as written (spacing with its
/// parts joined by commas) and whether it sits on a glyph, whose sizes follow baseline W22 rather than the type ramp.
/// </summary>
public sealed record Finding(string Rule, string Value, bool Glyph);
