namespace Theseus.Core.Colour;

/// <summary>A colour in the OKLab perceptual colour space.</summary>
/// <param name="L">Perceptual lightness, 0 for black and 1 for white.</param>
/// <param name="A">The green/red opponent axis.</param>
/// <param name="B">The blue/yellow opponent axis.</param>
public readonly record struct OkLabColour(double L, double A, double B);
