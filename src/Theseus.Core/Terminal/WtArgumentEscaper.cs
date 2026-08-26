namespace Theseus.Core.Terminal;

/// <summary>
/// Escapes values bound for a Windows Terminal command line. Windows Terminal splits its own
/// command line on semicolons to separate tabs, and does so even inside a correctly quoted
/// argument, so a semicolon in a title or a path would otherwise open a second tab.
/// </summary>
public static class WtArgumentEscaper
{
    /// <summary>Escapes the tab separator in a value.</summary>
    public static string Escape(string value) =>
        string.IsNullOrEmpty(value) ? value : value.Replace(";", @"\;");
}
