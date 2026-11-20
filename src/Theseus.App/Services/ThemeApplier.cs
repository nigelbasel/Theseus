using System.Windows;
using Microsoft.Win32;
using Theseus.Core.Settings;

namespace Theseus.App.Services;

/// <summary>
/// Applies the chosen theme to the running application. <c>ThemeMode</c> is still an experimental
/// WPF API, opted into deliberately for this file alone in .editorconfig: it is the only route to switching theme
/// without a restart, and following the operating system is the whole point of the System option.
/// </summary>
public static class ThemeApplier
{
    private const string LightStates = "StateColours.Light.xaml";

    private const string DarkStates = "StateColours.Dark.xaml";

    private static AppTheme _theme;
    private static bool _following;

    /// <summary>Applies a theme to the application and every open window.</summary>
    public static void Apply(AppTheme theme)
    {
        var mode = ToThemeMode(theme);

        if (Application.Current is not { } application)
        {
            return;
        }

        _theme = theme;
        application.ThemeMode = mode;

        foreach (var window in application.Windows.OfType<Window>())
        {
            window.ThemeMode = mode;
        }

        ApplyStateColours(application, IsDark(theme));

        // The Fluent theme follows the system by itself; the project view's state colours (1.1d) are Theseus's own,
        // so they are swapped here when the system's choice changes.
        if (!_following)
        {
            _following = true;
            SystemEvents.UserPreferenceChanged += (_, args) =>
            {
                if (args.Category == UserPreferenceCategory.General && _theme == AppTheme.System)
                {
                    application.Dispatcher.BeginInvoke(() => ApplyStateColours(application, IsDark(AppTheme.System)));
                }
            };
        }
    }

    private static void ApplyStateColours(Application application, bool dark)
    {
        var merged = application.Resources.MergedDictionaries;
        var wanted = dark ? DarkStates : LightStates;
        var current = merged.FirstOrDefault(dictionary => IsStates(dictionary, LightStates) || IsStates(dictionary, DarkStates));

        if (current is not null && IsStates(current, wanted))
        {
            return;
        }

        if (current is not null)
        {
            merged.Remove(current);
        }

        merged.Add(new ResourceDictionary { Source = new Uri($"pack://application:,,,/Theseus;component/Themes/{wanted}") });
    }

    private static bool IsStates(ResourceDictionary dictionary, string file) =>
        dictionary.Source?.OriginalString.EndsWith(file, StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsDark(AppTheme theme) => theme switch
    {
        AppTheme.Dark => true,
        AppTheme.Light => false,
        _ => Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme", 1) is 0,
    };

    private static ThemeMode ToThemeMode(AppTheme theme) => theme switch
    {
        AppTheme.Light => ThemeMode.Light,
        AppTheme.Dark => ThemeMode.Dark,
        _ => ThemeMode.System,
    };
}
