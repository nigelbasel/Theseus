namespace Theseus.App.ViewModels;

/// <summary>
/// How serious a status message is, which decides its glyph and its background (baseline W46; checklist G147, G150):
/// Information E946 on the attention brush, Warning E7BA on caution, Error E783 on critical. A warning or an error also
/// says so in words, never by its colour alone.
/// </summary>
public enum NoticeSeverity
{
    /// <summary>Something happened, and nothing needs doing.</summary>
    Information,

    /// <summary>Something may need attention.</summary>
    Warning,

    /// <summary>Something failed.</summary>
    Error,
}
