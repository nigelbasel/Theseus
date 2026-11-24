namespace Theseus.App.Text;

/// <summary>
/// Escapes a name or a note before it goes into an access-key label, such as a menu header or a button's text (baseline
/// W26, findings B12 and C-m3): WPF reads the first underscore as the access-key marker, so "my_card" would show as
/// "mycard" with the c underlined.
/// </summary>
public static class AccessKeyText
{
    /// <summary>The text with each underscore doubled, so every underscore is drawn and none is a marker.</summary>
    public static string Escape(string? text) => text?.Replace("_", "__", StringComparison.Ordinal) ?? string.Empty;
}
