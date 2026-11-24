namespace Theseus.App.ViewModels;

/// <summary>
/// The status bar's last notice and how serious it is (baseline W46; finding C17). Saying a notice without a severity
/// makes it Information, so a plain notice never keeps the warning or error glyph of the one before it.
/// </summary>
public sealed class NoticeLine
{
    /// <summary>The notice's text.</summary>
    public string Text { get; private set; } = string.Empty;

    /// <summary>How serious it is.</summary>
    public NoticeSeverity Severity { get; private set; }

    /// <summary>The severity of more said after this notice: the notice's own, or Information when there is none.</summary>
    public NoticeSeverity SeverityToExtend => string.IsNullOrWhiteSpace(Text) ? NoticeSeverity.Information : Severity;

    /// <summary>Says a notice of information.</summary>
    public void Say(string text) => Say(NoticeSeverity.Information, text);

    /// <summary>Says a notice of the severity given.</summary>
    public void Say(NoticeSeverity severity, string text)
    {
        Text = text;
        Severity = severity;
    }
}
