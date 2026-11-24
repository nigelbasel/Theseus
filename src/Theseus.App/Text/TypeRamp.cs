using System.Windows.Media;

namespace Theseus.App.Text;

/// <summary>
/// The Windows type ramp (checklist G94; baseline W18) and the UI font (W21), for text built in code. Markup uses the
/// ramp styles in App.xaml (CaptionText, BodyText and the rest), which hold the same sizes.
/// </summary>
public static class TypeRamp
{
    /// <summary>Caption: dense detail lines, tags and explanations.</summary>
    public const double Caption = 12;

    /// <summary>Body, and Body Strong when semibold.</summary>
    public const double Body = 14;

    /// <summary>Body Large.</summary>
    public const double BodyLarge = 18;

    /// <summary>Subtitle.</summary>
    public const double Subtitle = 20;

    /// <summary>Title.</summary>
    public const double Title = 28;

    /// <summary>The UI font, the same face as App.xaml's UiFont resource.</summary>
    public static readonly FontFamily UiFont = new("Segoe UI Variable Text, Segoe UI");
}
