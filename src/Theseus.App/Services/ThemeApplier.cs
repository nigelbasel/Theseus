using System.Windows;
using Theseus.Core.Settings;

namespace Theseus.App.Services;

/// <summary>
/// Applies the chosen theme to the running application. <c>ThemeMode</c> is still an experimental
/// WPF API, opted into deliberately in the project file: it is the only route to switching theme
/// without a restart, and following the operating system is the whole point of the System option.
/// </summary>
public static class ThemeApplier
{
    /// <summary>Applies a theme to the application and every open window.</summary>
    public static void Apply(AppTheme theme)
    {
        var mode = ToThemeMode(theme);

        if (Application.Current is not { } application)
        {
            return;
        }

        application.ThemeMode = mode;

        foreach (var window in application.Windows.OfType<Window>())
        {
            window.ThemeMode = mode;
        }
    }

    private static ThemeMode ToThemeMode(AppTheme theme) => theme switch
    {
        AppTheme.Light => ThemeMode.Light,
        AppTheme.Dark => ThemeMode.Dark,
        _ => ThemeMode.System,
    };
}
