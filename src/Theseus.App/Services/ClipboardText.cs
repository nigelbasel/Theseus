using System.Windows;
using Theseus.Core.Diagnostics;

namespace Theseus.App.Services;

/// <summary>Puts text on the clipboard, or says it could not.</summary>
public static class ClipboardText
{
    /// <summary>
    /// Copies text, reporting failure rather than throwing. The clipboard is shared with every other
    /// application on the desktop and any of them can be holding it open at the moment we ask.
    /// </summary>
    public static bool TryCopy(string text)
    {
        try
        {
            Clipboard.SetText(text);

            return true;
        }
        catch (Exception exception)
        {
            TheseusLog.Warning(exception, "The clipboard would not take the text");

            return false;
        }
    }
}
