using System.IO;
using Theseus.Core.Domain;
using Theseus.Core.Launching;
using Theseus.Core.Settings;
using Theseus.Core.Terminal;

namespace Theseus.App.ViewModels;

/// <summary>
/// The settings panel. Each path shows what it resolves to right now, because "blank means find it
/// automatically" is only reassuring if you can see what was found.
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly ISettingsStore _store;
    private readonly Action _saved;

    private string _planDirectory = string.Empty;
    private string _terminalWindowName = string.Empty;
    private string _terminalExecutablePath = string.Empty;
    private string _claudeExecutablePath = string.Empty;
    private string _jiraBaseUrl = string.Empty;
    private string _sessionPreamble = string.Empty;
    private string _continuePrompt = string.Empty;
    private bool _isOpen;

    /// <summary>Creates the panel over the settings file.</summary>
    public SettingsViewModel(ISettingsStore store, Action saved)
    {
        _store = store;
        _saved = saved;

        OpenCommand = new RelayCommand(_ => Open());
        CloseCommand = new RelayCommand(_ => IsOpen = false);
        SaveCommand = new RelayCommand(_ => Save());

        Load(store.Read());
    }

    /// <summary>Shows the panel, discarding any half-made edits from last time.</summary>
    public RelayCommand OpenCommand { get; }

    /// <summary>Hides the panel without saving.</summary>
    public RelayCommand CloseCommand { get; }

    /// <summary>Writes the settings and closes.</summary>
    public RelayCommand SaveCommand { get; }

    /// <summary>Whether the panel is showing.</summary>
    public bool IsOpen
    {
        get => _isOpen;
        set => Set(ref _isOpen, value);
    }

    /// <summary>Where the Claude Code sessions write their plan files.</summary>
    public string PlanDirectory
    {
        get => _planDirectory;
        set
        {
            if (Set(ref _planDirectory, value))
            {
                Raise(nameof(PlanDirectoryState));
            }
        }
    }

    /// <summary>Whether the plan directory exists, and how many plans are in it.</summary>
    public string PlanDirectoryState => Directory.Exists(_planDirectory)
        ? $"{Directory.GetFiles(_planDirectory, "*.json").Length} plan files"
        : "does not exist";

    /// <summary>The named terminal window tabs are launched into.</summary>
    public string TerminalWindowName
    {
        get => _terminalWindowName;
        set => Set(ref _terminalWindowName, value);
    }

    /// <summary>A terminal chosen by hand. Blank resolves from whatever is running.</summary>
    public string TerminalExecutablePath
    {
        get => _terminalExecutablePath;
        set
        {
            if (Set(ref _terminalExecutablePath, value))
            {
                Raise(nameof(TerminalState));
            }
        }
    }

    /// <summary>What the terminal setting resolves to right now.</summary>
    public string TerminalState
    {
        get
        {
            var resolved = new TerminalResolver(_terminalExecutablePath).Resolve();

            return resolved.Source switch
            {
                TerminalSource.None => "no terminal found",
                TerminalSource.ExecutionAlias => $"wt.exe alias — {resolved.ExecutablePath}",
                _ when resolved.DistinctRunningPackages > 1 =>
                    $"{resolved.ExecutablePath} ({resolved.DistinctRunningPackages} packages running)",
                _ => resolved.ExecutablePath,
            };
        }
    }

    /// <summary>Claude Code chosen by hand. Blank looks in the usual place and on the path.</summary>
    public string ClaudeExecutablePath
    {
        get => _claudeExecutablePath;
        set
        {
            if (Set(ref _claudeExecutablePath, value))
            {
                Raise(nameof(ClaudeState));
            }
        }
    }

    /// <summary>What the Claude Code setting resolves to right now.</summary>
    public string ClaudeState =>
        new ClaudeLocator(_claudeExecutablePath).Locate() ?? "not found";

    /// <summary>The Jira instance issue keys link to. Empty leaves the keys as plain labels.</summary>
    public string JiraBaseUrl
    {
        get => _jiraBaseUrl;
        set
        {
            if (Set(ref _jiraBaseUrl, value))
            {
                Raise(nameof(JiraState));
            }
        }
    }

    /// <summary>What a key would link to with the current setting.</summary>
    public string JiraState =>
        JiraSite.IssueUrl(_jiraBaseUrl, "ABC-123") ?? "keys will not be links";

    /// <summary>Standing instructions put in front of every workstream own opening prompt.</summary>
    public string SessionPreamble
    {
        get => _sessionPreamble;
        set
        {
            if (Set(ref _sessionPreamble, value))
            {
                Raise(nameof(SessionPreambleState));
            }
        }
    }

    /// <summary>Whether the preamble is in force, and how long it is.</summary>
    public string SessionPreambleState => string.IsNullOrWhiteSpace(_sessionPreamble)
        ? "sessions open with the workstream prompt alone"
        : $"{_sessionPreamble.Trim().Length} characters, ahead of every workstream prompt";

    /// <summary>What a workstream that has run before is told when it opens again.</summary>
    public string ContinuePrompt
    {
        get => _continuePrompt;
        set
        {
            if (Set(ref _continuePrompt, value))
            {
                Raise(nameof(ContinuePromptState));
            }
        }
    }

    /// <summary>Whether the default continuation is in force.</summary>
    public string ContinuePromptState => string.IsNullOrWhiteSpace(_continuePrompt)
        ? "using the built-in continuation"
        : "custom continuation";

    private void Open()
    {
        Load(_store.Read());
        IsOpen = true;
    }

    private void Load(TheseusSettings settings)
    {
        PlanDirectory = settings.PlanDirectory;
        TerminalWindowName = settings.TerminalWindowName;
        TerminalExecutablePath = settings.TerminalExecutablePath;
        ClaudeExecutablePath = settings.ClaudeExecutablePath;
        JiraBaseUrl = settings.JiraBaseUrl;
        SessionPreamble = settings.SessionPreamble;
        ContinuePrompt = settings.ContinuePrompt;
    }

    private void Save()
    {
        _store.Write(_store.Read() with
        {
            PlanDirectory = PlanDirectory,
            TerminalWindowName = TerminalWindowName,
            TerminalExecutablePath = TerminalExecutablePath,
            ClaudeExecutablePath = ClaudeExecutablePath,
            JiraBaseUrl = JiraBaseUrl,
            SessionPreamble = SessionPreamble,
            ContinuePrompt = ContinuePrompt,
        });

        IsOpen = false;
        _saved();
    }
}
