using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Xml.Linq;

namespace Theseus.Tests.Theming;

/// <summary>The theme dictionaries as source files, and the WCAG 2 contrast ratio their colours are measured by.</summary>
internal static class ThemeFiles
{
    public static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    public static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    public static XDocument Load(string file) => XDocument.Load(PathOf(file));

    public static string Text(string file) => File.ReadAllText(PathOf(file));

    /// <summary>Every keyed entry of a theme dictionary: key to element.</summary>
    public static Dictionary<string, XElement> Entries(string file) =>
        Load(file).Root!.Elements()
            .Where(element => element.Attribute(Xaml + "Key") is not null)
            .ToDictionary(element => (string)element.Attribute(Xaml + "Key")!);

    /// <summary>The colour of every SolidColorBrush in a theme dictionary.</summary>
    public static Dictionary<string, Color> Colours(string file) =>
        Entries(file).Where(entry => entry.Value.Name == Presentation + "SolidColorBrush")
            .ToDictionary(entry => entry.Key, entry => Parse((string)entry.Value.Attribute("Color")!));

    public static Color Parse(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    /// <summary>The WCAG 2.2 contrast ratio of two opaque colours, from 1 to 21.</summary>
    public static double Contrast(Color first, Color second)
    {
        var a = Luminance(first);
        var b = Luminance(second);

        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    /// <summary>A translucent colour laid over an opaque one.</summary>
    public static Color Over(Color top, Color bottom)
    {
        var alpha = top.A / 255.0;

        byte Mix(byte up, byte down) => (byte)Math.Round(up * alpha + down * (1 - alpha));

        return Color.FromRgb(Mix(top.R, bottom.R), Mix(top.G, bottom.G), Mix(top.B, bottom.B));
    }

    private static double Luminance(Color colour) =>
        0.2126 * Linear(colour.R) + 0.7152 * Linear(colour.G) + 0.0722 * Linear(colour.B);

    private static double Linear(byte channel)
    {
        var value = channel / 255.0;

        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    private static string PathOf(string file) => Path.Combine(RepositoryRoot(), "src", "Theseus.App", "Themes", file);

    private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
}
