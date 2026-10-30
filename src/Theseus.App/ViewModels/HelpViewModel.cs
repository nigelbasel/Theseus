using System.Diagnostics;
using System.IO;
using Theseus.Core.Diagnostics;
using Theseus.Core.Stopping;

namespace Theseus.App.ViewModels;

/// <summary>
/// Help: web pages kept in the repository's docs folder and published with GitHub Pages (Nigel,
/// 2026-10-30: "a normal help page, html with hyperlinks, images etc ... opened in a browser"), and
/// the one thing about this machine that help used to show beside its text: whether the stop hook is
/// registered. About shows that now.
/// </summary>
public sealed class HelpViewModel : ObservableObject
{
    /// <summary>Where the help pages are published.</summary>
    public const string Site = "https://nigelbasel.github.io/Theseus/";

    private bool _hookIsRegistered;

    /// <summary>Creates the help.</summary>
    public HelpViewModel() => OpenCommand = new RelayCommand(parameter => Open(parameter as string));

    /// <summary>Opens the help in the browser: the page named by the parameter, or the contents.</summary>
    public RelayCommand OpenCommand { get; }

    /// <summary>The address of a help page, such as <c>locks</c> or <c>board#folding</c>.</summary>
    public static string PageUrl(string? page)
    {
        if (string.IsNullOrWhiteSpace(page))
        {
            return Site;
        }

        var parts = page.Trim().Split('#', 2);

        return parts.Length == 2 ? $"{Site}{parts[0]}.html#{parts[1]}" : $"{Site}{parts[0]}.html";
    }

    /// <summary>Whether a Theseus hook is registered in the Claude Code settings.</summary>
    public bool HookIsRegistered
    {
        get => _hookIsRegistered;
        private set
        {
            if (Set(ref _hookIsRegistered, value))
            {
                Raise(nameof(HookStatus));
            }
        }
    }

    /// <summary>What to say about the hook, as a sentence rather than a state.</summary>
    public string HookStatus => HookIsRegistered
        ? "The stop hook is registered, so Stop delivers on its own. A session started before it was"
          + " registered still will not answer: hooks are loaded when a session starts."
        : "The stop hook is not registered, so nothing will be delivered to any session. Run"
          + " install.ps1 - it prints the configuration to merge - or stop sessions by typing into"
          + " their tabs, which needs no hook.";

    /// <summary>Where the hook is registered, shown so it can be opened and read.</summary>
    public string SettingsPath => HookInstallation.SettingsPath;

    /// <summary>Re-reads what is true on this machine. Cheap, and done every time About is opened.</summary>
    public void Check() => HookIsRegistered = HookInstallation.IsInstalled(Read());

    private static void Open(string? page) =>
        Process.Start(new ProcessStartInfo(PageUrl(page)) { UseShellExecute = true });

    private static string? Read()
    {
        try
        {
            return File.Exists(HookInstallation.SettingsPath)
                ? File.ReadAllText(HookInstallation.SettingsPath)
                : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Warning(exception, "The Claude Code settings could not be read");

            return null;
        }
    }
}
