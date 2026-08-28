using System.IO;
using Theseus.Core.Diagnostics;
using Theseus.Core.Help;
using Theseus.Core.Stopping;

namespace Theseus.App.ViewModels;

/// <summary>
/// The help window's state: the written explanations, and what is actually true on this machine
/// right now.
///
/// The second part is the point. "Stop asks the session to hand over" is worth reading once; "the
/// hook that delivers it is not registered on this machine" is what somebody needs at the moment
/// they press Stop and nothing happens. Help that cannot see the machine it is running on leaves
/// them reading a description of a mechanism they do not have.
/// </summary>
public sealed class HelpViewModel : ObservableObject
{
    private bool _isOpen;
    private bool _hookIsRegistered;

    /// <summary>Creates the help.</summary>
    public HelpViewModel()
    {
        OpenCommand = new RelayCommand(_ =>
        {
            Check();
            IsOpen = true;
        });

        CloseCommand = new RelayCommand(_ => IsOpen = false);
    }

    /// <summary>The written explanations.</summary>
    public IReadOnlyList<HelpSection> Sections { get; } = HelpBook.Sections;

    /// <summary>Shows the help.</summary>
    public RelayCommand OpenCommand { get; }

    /// <summary>Hides it.</summary>
    public RelayCommand CloseCommand { get; }

    /// <summary>Whether the window is showing.</summary>
    public bool IsOpen
    {
        get => _isOpen;
        set => Set(ref _isOpen, value);
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

    /// <summary>Re-reads what is true on this machine. Cheap, and done every time help is opened.</summary>
    public void Check() => HookIsRegistered = HookInstallation.IsInstalled(Read());

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
