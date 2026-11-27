using System.IO;
using Theseus.Core.Domain;
using Theseus.Core.Launching;
using Theseus.Core.Projects;
using Theseus.Core.Settings;
using Theseus.Core.Terminal;

namespace Theseus.App.ViewModels;

/// <summary>
/// The settings panel. Each path shows what it resolves to right now, because "blank means find it
/// automatically" is only reassuring if you can see what was found. Every row writes the settings file the moment its
/// value changes, and there is no Save: a settings page applies what it shows (W6, checklist G138, 2026-11-24). The board
/// is told once, when Options closes, so it re-reads stages and areas a single time however many rows changed.
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly ISettingsStore _store;
    private readonly Action _changedAndClosed;

    private string _planDirectory = string.Empty;
    private string _terminalWindowName = string.Empty;
    private string _terminalExecutablePath = string.Empty;
    private string _claudeExecutablePath = string.Empty;
    private string _jiraBaseUrl = string.Empty;
    private string _holdoutRemote = string.Empty;
    private string _projectsFolder = string.Empty;
    private string _worktreesFolder = string.Empty;
    private string _clonesFolder = string.Empty;
    private bool _showsProjectsIntro;
    private bool _includePreReleases;
    private string _sessionPreamble = string.Empty;
    private string _continuePrompt = string.Empty;
    private string _stopPrompt = string.Empty;
    private StartMode _defaultStartMode;
    private string _identityPrompt = string.Empty;
    private string _laneList = string.Empty;
    private string _areaList = string.Empty;
    private bool _isOpen;
    private bool _loading;
    private bool _changed;

    private IReadOnlyList<LimitSettingViewModel>? _headlessLimits;

    /// <summary>Creates the panel over the settings file.</summary>
    public SettingsViewModel(ISettingsStore store, Action changedAndClosed)
    {
        _store = store;
        _changedAndClosed = changedAndClosed;
        Duties = new DutyOptionsViewModel(store);

        OpenCommand = new RelayCommand(_ => Open());
        OpenProjectPropertiesCommand = new RelayCommand(parameter =>
            {
                if (parameter is HoldoutRowViewModel row)
                {
                    // One dialog at a time (W8, G17): Options closes before Project properties opens.
                    IsOpen = false;
                    OpenProjectHoldouts?.Invoke(row.Project);
                }
            },
            parameter => parameter is HoldoutRowViewModel { IsAdHoc: false } row && OpenProjectHoldouts is not null
                         && (HasProjectCard?.Invoke(row.Project) ?? true));
        ResetTerminalCommand = new RelayCommand(_ => TerminalExecutablePath = string.Empty, _ => !string.IsNullOrWhiteSpace(TerminalExecutablePath));
        ResetClaudeCommand = new RelayCommand(_ => ClaudeExecutablePath = string.Empty, _ => !string.IsNullOrWhiteSpace(ClaudeExecutablePath));
        ResetHoldoutRemoteCommand = new RelayCommand(_ => HoldoutRemote = string.Empty, _ => !string.IsNullOrWhiteSpace(HoldoutRemote));
        ShowProjectFolderCommand = new RelayCommand(parameter =>
        {
            if (parameter is string name && HiddenProjectFolders.Remove(name))
            {
                Write(settings => settings with { HiddenProjectFolders = [.. settings.HiddenProjectFolders.Where(folder => folder != name)] });
            }
        });
        CloseCommand = new RelayCommand(_ => IsOpen = false);
        ResetIdentityPromptCommand = new RelayCommand(_ => IdentityPrompt = WorkstreamIdentity.DefaultTemplate,
            _ => IdentityPrompt != WorkstreamIdentity.DefaultTemplate);

        Load(store.Read());
    }

    /// <summary>Shows the panel, read afresh from the settings file.</summary>
    public RelayCommand OpenCommand { get; }

    /// <summary>The themes to choose between, each named in words.</summary>
    public IReadOnlyList<ThemeChoice> ThemeChoices { get; } =
    [
        new(AppTheme.System, "Use system setting"),
        new(AppTheme.Light, "Light"),
        new(AppTheme.Dark, "Dark"),
    ];

    /// <summary>The theme in force, as Options shows it.</summary>
    public string ThemeValue => ThemeChoices.First(choice => choice.Theme == Theme).Label;

    /// <summary>This Theseus's version, for the About section at the foot of Advanced (W53).</summary>
    public string VersionDescribed => Services.AppVersion.Described;

    /// <summary>Which theme to paint in, applied as soon as it is chosen, because a theme is judged by looking at it.</summary>
    public AppTheme Theme
    {
        get => _store.Read().Theme;
        set
        {
            if (value == _store.Read().Theme)
            {
                return;
            }

            _store.Write(_store.Read() with { Theme = value });
            Services.ThemeApplier.Apply(value);
            Raise(nameof(Theme));
            Raise(nameof(ThemeValue));
        }
    }

    /// <summary>Close: every change is already written.</summary>
    public RelayCommand CloseCommand { get; }

    /// <summary>Puts the built-in identity wording back.</summary>
    public RelayCommand ResetIdentityPromptCommand { get; }

    /// <summary>Whether the panel is showing.</summary>
    public bool IsOpen
    {
        get => _isOpen;
        set
        {
            if (Set(ref _isOpen, value) && !value && _changed)
            {
                _changed = false;
                _changedAndClosed();
            }
        }
    }

    /// <summary>Where the Claude Code sessions write their plan files.</summary>
    public string PlanDirectory
    {
        get => _planDirectory;
        set
        {
            if (Set(ref _planDirectory, value))
            {
                Write(settings => settings with { PlanDirectory = value });
                Raise(nameof(PlanDirectoryState));
                Raise(nameof(PlanDirectoryValue));
            }
        }
    }

    /// <summary>Whether the plan directory exists, and how many plans are in it.</summary>
    public string PlanDirectoryState => Directory.Exists(_planDirectory)
        ? $"{Directory.GetFiles(_planDirectory, "*.json").Length} plan files"
        : "does not exist";

    /// <summary>The plan directory as Options shows it.</summary>
    public string PlanDirectoryValue => $"{_planDirectory} ({PlanDirectoryState})";

    /// <summary>The named terminal window tabs are launched into.</summary>
    public string TerminalWindowName
    {
        get => _terminalWindowName;
        set
        {
            if (Set(ref _terminalWindowName, value))
            {
                Write(settings => settings with { TerminalWindowName = value });
                Raise(nameof(AreaListState));
            }
        }
    }

    /// <summary>Reset beside Windows Terminal: found automatically again.</summary>
    public RelayCommand ResetTerminalCommand { get; }

    /// <summary>Reset beside Claude Code: found automatically again.</summary>
    public RelayCommand ResetClaudeCommand { get; }

    /// <summary>Windows Terminal as Options shows it: the one chosen, or what is found.</summary>
    public string TerminalValue => string.IsNullOrWhiteSpace(_terminalExecutablePath)
        ? $"Found automatically: {TerminalState}"
        : $"{_terminalExecutablePath.Trim()} (you chose it)";

    /// <summary>Claude Code as Options shows it: the one chosen, or what is found.</summary>
    public string ClaudeValue => string.IsNullOrWhiteSpace(_claudeExecutablePath)
        ? $"Found automatically: {ClaudeState}"
        : $"{_claudeExecutablePath.Trim()} (you chose it)";


    /// <summary>A terminal chosen by hand. Blank resolves from whatever is running.</summary>
    public string TerminalExecutablePath
    {
        get => _terminalExecutablePath;
        set
        {
            if (Set(ref _terminalExecutablePath, value))
            {
                Write(settings => settings with { TerminalExecutablePath = value });
                Raise(nameof(TerminalState));
                Raise(nameof(TerminalValue));
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
                TerminalSource.OnlyPackageAlias => $"wt.exe alias, the only terminal installed — {resolved.ExecutablePath}",
                TerminalSource.RefusedFallback =>
                    $"wt.exe alias, because Windows refused to start the terminal package directly — {resolved.ExecutablePath}",
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
                Write(settings => settings with { ClaudeExecutablePath = value });
                Raise(nameof(ClaudeState));
                Raise(nameof(ClaudeValue));
            }
        }
    }

    /// <summary>What the Claude Code setting resolves to right now.</summary>
    public string ClaudeState =>
        new ClaudeLocator(_claudeExecutablePath).Locate() ?? "not found";

    /// <summary>
    /// Each project's holdout folder and how it was found, read-only (options-made-clear item 1): each is changed in its
    /// project's Project properties, so nothing here reaches a project you did not name.
    /// </summary>
    public IReadOnlyList<HoldoutRowViewModel> HoldoutRows =>
        [.. _holdoutEntries.Select(entry => new HoldoutRowViewModel(entry.Project, entry.Folder, entry.Source)
        {
            IsMissing = entry.Source is Theseus.Core.Links.HoldoutSource.Chosen or Theseus.Core.Links.HoldoutSource.Recorded
                        && (entry.Folder.Length == 0 || !Directory.Exists(entry.Folder)),
        })];

    /// <summary>The folders in force, set by the board each time it looks.</summary>
    public IReadOnlyList<Theseus.Core.Links.HoldoutFolderEntry> HoldoutEntries
    {
        get => _holdoutEntries;
        set
        {
            _holdoutEntries = value ?? [];
            Raise(nameof(HoldoutRows));
        }
    }

    private IReadOnlyList<Theseus.Core.Links.HoldoutFolderEntry> _holdoutEntries = [];

    /// <summary>Opens a project's Project properties at its Holdouts tab, by project folder name. Set by the shell.</summary>
    public Action<string>? OpenProjectHoldouts { get; set; }

    /// <summary>Whether a project, by folder name, has a card whose Project properties can open; set by the shell.</summary>
    public Func<string, bool>? HasProjectCard { get; set; }

    /// <summary>Project properties… beside a project's holdout row.</summary>
    public RelayCommand OpenProjectPropertiesCommand { get; }

    /// <summary>The folders said not to be projects (1.1e AC 20), each to be shown again.</summary>
    public System.Collections.ObjectModel.ObservableCollection<string> HiddenProjectFolders { get; } = [];

    /// <summary>Show again: a folder said not to be a project is shown as a project card again.</summary>
    public RelayCommand ShowProjectFolderCommand { get; }

    /// <summary>The holdout's remote address, which the guard also keeps from implementing sessions.</summary>
    public string HoldoutRemote
    {
        get => _holdoutRemote;
        set
        {
            if (Set(ref _holdoutRemote, value))
            {
                Write(settings => settings with { HoldoutRemote = value.Trim() });
                Raise(nameof(HoldoutRemoteValue));
            }
        }
    }

    /// <summary>The holdout remote address as Options shows it.</summary>
    public string HoldoutRemoteValue => string.IsNullOrWhiteSpace(_holdoutRemote) ? "None" : _holdoutRemote.Trim();

    /// <summary>Reset beside the holdout remote address: none.</summary>
    public RelayCommand ResetHoldoutRemoteCommand { get; }

    /// <summary>The folder of project homes, as typed (1.1b AC 1).</summary>
    public string ProjectsFolder
    {
        get => _projectsFolder;
        set
        {
            if (Set(ref _projectsFolder, value))
            {
                Write(settings => settings with { ProjectsFolder = ProjectsFolderPath.Normalize(value) });
                Raise(nameof(ProjectsFolderValue));
            }
        }
    }

    /// <summary>The projects folder as Options shows it.</summary>
    public string ProjectsFolderValue => string.IsNullOrWhiteSpace(_projectsFolder) ? "None: no projects are shown" : _projectsFolder.Trim();

    /// <summary>
    /// Whether Settings opened with no projects folder saved, so it explains the model and offers the two folders
    /// (1.1b AC 11). Decided when it opens, so the panel does not vanish while a path is typed.
    /// </summary>
    public bool ShowsProjectsIntro
    {
        get => _showsProjectsIntro;
        private set => Set(ref _showsProjectsIntro, value);
    }

    /// <summary>The folder workstreams' worktrees go in, as typed (1.1b AC 11).</summary>
    public string WorktreesFolder
    {
        get => _worktreesFolder;
        set
        {
            if (Set(ref _worktreesFolder, value))
            {
                Write(settings => settings with { WorktreesFolder = ProjectsFolderPath.Normalize(value) });
                Raise(nameof(WorktreesFolderValue));
            }
        }
    }

    /// <summary>The worktrees folder as Options shows it.</summary>
    public string WorktreesFolderValue => string.IsNullOrWhiteSpace(_worktreesFolder) ? "None chosen" : _worktreesFolder.Trim();

    /// <summary>The folder the primary clones are in, by the layout rule, as typed (worktree-per-repo AC 18).</summary>
    public string ClonesFolder
    {
        get => _clonesFolder;
        set
        {
            if (Set(ref _clonesFolder, value))
            {
                Write(settings => settings with { ClonesFolder = ProjectsFolderPath.Normalize(value) });
                Raise(nameof(ClonesFolderValue));
            }
        }
    }

    /// <summary>The clones folder as Options shows it.</summary>
    public string ClonesFolderValue => string.IsNullOrWhiteSpace(_clonesFolder) ? "None chosen" : _clonesFolder.Trim();

    /// <summary>Whether Check for updates also offers the pre-releases built from a project's primary branch.</summary>
    public bool IncludePreReleases
    {
        get => _includePreReleases;
        set
        {
            if (Set(ref _includePreReleases, value))
            {
                Write(settings => settings with { IncludePreReleases = value });
                Raise(nameof(UpdatesValue));
            }
        }
    }

    /// <summary>What Check for updates offers, as Options shows it.</summary>
    public string UpdatesValue => _includePreReleases ? "Releases and pre-releases" : "Releases from main only";

    /// <summary>The Jira instance issue keys link to. Empty leaves the keys as plain labels.</summary>
    public string JiraBaseUrl
    {
        get => _jiraBaseUrl;
        set
        {
            if (Set(ref _jiraBaseUrl, value))
            {
                Write(settings => settings with { JiraBaseUrl = value });
                Raise(nameof(JiraState));
                Raise(nameof(JiraValue));
                Raise(nameof(JiraExplanation));
            }
        }
    }

    /// <summary>What a key would link to with the current setting.</summary>
    public string JiraState =>
        JiraSite.IssueUrl(_jiraBaseUrl, "ABC-123") ?? "keys will not be links";

    /// <summary>The Jira address as Options shows it.</summary>
    public string JiraValue => string.IsNullOrWhiteSpace(_jiraBaseUrl) ? "None" : _jiraBaseUrl.Trim();

    /// <summary>What the Jira address does now.</summary>
    public string JiraExplanation => JiraSite.IssueUrl(_jiraBaseUrl, "ABC-123") is { } link
        ? $"Issue keys on cards link here: ABC-123 opens {link}."
        : "Issue keys on cards are plain labels. Set an address to make them links.";

    /// <summary>Standing instructions put in front of every workstream own opening prompt.</summary>
    public string SessionPreamble
    {
        get => _sessionPreamble;
        set
        {
            if (Set(ref _sessionPreamble, value))
            {
                Write(settings => settings with { SessionPreamble = value });
                Raise(nameof(SessionPreambleState));
                Raise(nameof(StandingValue));
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
                Write(settings => settings with { ContinuePrompt = value });
                Raise(nameof(ContinuePromptState));
                Raise(nameof(ContinueValue));
            }
        }
    }

    /// <summary>Whether the default continuation is in force.</summary>
    public string ContinuePromptState => string.IsNullOrWhiteSpace(_continuePrompt)
        ? "using the built-in continuation"
        : "custom continuation";

    /// <summary>What a session is told when it is asked to stop.</summary>
    public string StopPrompt
    {
        get => _stopPrompt;
        set
        {
            if (Set(ref _stopPrompt, value))
            {
                Write(settings => settings with { StopPrompt = value });
                Raise(nameof(StopValue));
            }
        }
    }

    /// <summary>
    /// What opens every session. Shown filled in with the built-in wording rather than empty, because
    /// an empty box says nothing about what sessions are actually told.
    /// </summary>
    public string IdentityPrompt
    {
        get => _identityPrompt;
        set
        {
            if (Set(ref _identityPrompt, value))
            {
                Write(settings => settings with { IdentityPrompt = value == WorkstreamIdentity.DefaultTemplate ? string.Empty : value });
                Raise(nameof(IdentityPromptState));
                Raise(nameof(IdentityValue));
            }
        }
    }

    /// <summary>Whether the identity wording is the built-in one, and whether it still says where notes go.</summary>
    public string IdentityPromptState =>
        !_identityPrompt.Contains(SessionOpening.HandoffPlaceholder, StringComparison.Ordinal)
            ? "This never mentions {handoff}, so sessions will not be told where their handover notes belong."
            : _identityPrompt == WorkstreamIdentity.DefaultTemplate
                ? "The built-in wording."
                : "Your own wording, in place of the built-in one.";

    /// <summary>Whether Start and Restart reopen a workstream's last conversation.</summary>
    public bool ResumeByDefault
    {
        get => _defaultStartMode == StartMode.Resume;
        set
        {
            if (value)
            {
                SetStartMode(StartMode.Resume);
            }
        }
    }

    /// <summary>Whether Start and Restart open a new conversation pointed at the handoff.</summary>
    public bool FreshByDefault
    {
        get => _defaultStartMode == StartMode.Fresh;
        set
        {
            if (value)
            {
                SetStartMode(StartMode.Fresh);
            }
        }
    }

    /// <summary>The board's lanes, one per line, in the order they are shown.</summary>
    public string LaneList
    {
        get => _laneList;
        set
        {
            if (Set(ref _laneList, value))
            {
                Write(settings => settings with { Lanes = Lanes });
                Raise(nameof(LaneListState));
                Raise(nameof(LanesValue));
            }
        }
    }

    /// <summary>
    /// What the typed lanes come to, and which of them a whole-board Start will leave alone. The
    /// last lane is protected, so which lane that is has to be visible while the list is being edited.
    /// </summary>
    public string LaneListState
    {
        get
        {
            var lanes = Lanes;

            return $"{lanes.Count} lanes. Cards in a lane you remove move to the first lane. "
                   + "No lane is treated as special: what a bulk Start or Stop acts on is what you "
                   + "tick on the cards.";
        }
    }

    /// <summary>The board's areas, one per line, in the order the rows are shown.</summary>
    public string AreaList
    {
        get => _areaList;
        set
        {
            if (Set(ref _areaList, value))
            {
                Write(settings => settings with { Areas = Areas });
                Raise(nameof(AreaListState));
                Raise(nameof(AreasValue));
            }
        }
    }

    /// <summary>
    /// What the typed areas come to. The window name is spelled out because an area is not only a
    /// row: it decides which terminal window that area's sessions start in, and a renamed area means
    /// the next session starts somewhere new while the ones already running stay where they are.
    /// </summary>
    public string AreaListState
    {
        get
        {
            var areas = Areas;

            return $"{areas.Count} areas. Sessions in \"{areas[0]}\" start in a window called "
                   + $"\"{WindowGroupName.Sanitise(TerminalWindowName)}-{WindowGroupName.Sanitise(areas[0])}\". "
                   + "Cards in an area you remove move to the first area.";
        }
    }

    /// <summary>The board's stages as Options shows them, in order.</summary>
    public string LanesValue => string.Join(" · ", Lanes);

    /// <summary>The board's areas as Options shows them, in order.</summary>
    public string AreasValue => string.Join(" · ", Areas);

    /// <summary>What Start and Restart do for a workstream that has run before, as Options shows it.</summary>
    public string StartModeValue => _defaultStartMode == StartMode.Fresh ? "Start fresh from its handoff" : "Resume its last conversation";

    /// <summary>What the start mode in force costs and keeps; the ▾ beside Start offers the other either way.</summary>
    public string StartModeExplanation => (_defaultStartMode == StartMode.Fresh
            ? "A new conversation, told to read the handoff: small and cheap, but it knows only what the handoff says."
            : "Keeps everything the session knew, but reloads the conversation, often hundreds of thousands of tokens, on every reply.")
        + " The ▾ beside a card's Start offers the other.";

    /// <summary>The identity prompt as Options shows it.</summary>
    public string IdentityValue => _identityPrompt == WorkstreamIdentity.DefaultTemplate ? "The built-in wording" : PromptValue(_identityPrompt, string.Empty);

    /// <summary>The standing prompt as Options shows it.</summary>
    public string StandingValue => PromptValue(_sessionPreamble, "None: sessions open with the workstream's own prompt alone");

    /// <summary>The continuation prompt as Options shows it.</summary>
    public string ContinueValue => PromptValue(_continuePrompt, "The built-in continuation");

    /// <summary>The stop prompt as Options shows it.</summary>
    public string StopValue => PromptValue(_stopPrompt, "The built-in wording");

    private static string PromptValue(string text, string whenEmpty)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return whenEmpty;
        }

        var lines = text.Trim().Split('\n');
        var first = lines[0].Trim();

        return $"Your own wording, {lines.Length} {(lines.Length == 1 ? "line" : "lines")}: \"{(first.Length > 60 ? first[..60] + "…" : first)}\"";
    }

    private IReadOnlyList<string> Lanes =>
        BoardLanes.Sanitise(_laneList.Split('\n', StringSplitOptions.RemoveEmptyEntries));

    private IReadOnlyList<string> Areas =>
        BoardAreas.Sanitise(_areaList.Split('\n', StringSplitOptions.RemoveEmptyEntries));

    private void SetStartMode(StartMode mode)
    {
        if (_defaultStartMode != mode)
        {
            _defaultStartMode = mode;
            Write(settings => settings with { DefaultStartMode = mode });
        }

        Raise(nameof(ResumeByDefault));
        Raise(nameof(FreshByDefault));
        Raise(nameof(StartModeValue));
        Raise(nameof(StartModeExplanation));
    }

    /// <summary>
    /// Asks for a typed value: title, label and current value in, the value or null out. Set by the Options window, which
    /// owns the prompt.
    /// </summary>
    public Func<string, string, string, string?>? AskValue { get; set; }

    /// <summary>Options, Headless: the limits headless runs work inside, and the thresholds of the breakers (Nigel, 2026-11-24).</summary>
    public IReadOnlyList<LimitSettingViewModel> HeadlessLimits => _headlessLimits ??= [.. HeadlessLimitSettings.All.Select(setting =>
        new LimitSettingViewModel(setting, _store, (title, label, current) => AskValue?.Invoke(title, label, current)))];

    private void Open()
    {
        Load(_store.Read());

        foreach (var limit in HeadlessLimits)
        {
            limit.Reload();
        }

        Tags?.Load();
        foreach (var guard in Guards)
        {
            guard.Load();
        }

        AutoMode?.Load();
        LockTools?.Load();
        FolderTrust?.Load();
        IsOpen = true;
    }

    /// <summary>Options, Tags (card-search-and-tags item 13); it applies its changes at once. Set by the shell.</summary>
    public TagsViewModel? Tags { get; set; }

    /// <summary>Options, Guards: each guard Theseus ships, installed from there; acts at once. Set by the shell.</summary>
    public IReadOnlyList<GuardViewModel> Guards { get; set; } = [];

    /// <summary>Options, Guards: auto mode's environment, built from the owner's entries. Set by the shell.</summary>
    public AutoModeViewModel? AutoMode { get; set; }

    /// <summary>Options, Locks: the installed lock tools and the arbiter; acts at once. Set by the shell.</summary>
    public LockOptionsViewModel? LockTools { get; set; }

    /// <summary>Options, Duties: the limits for duties and the overnight queue; each change is saved at once.</summary>
    public DutyOptionsViewModel Duties { get; }

    /// <summary>Options, Folder trust: Claude Code's trust of the folders sessions start in; acts at once. Set by the shell.</summary>
    public FolderTrustViewModel? FolderTrust { get; set; }

    private void Load(TheseusSettings settings)
    {
        _loading = true;

        try
        {
            PlanDirectory = settings.PlanDirectory;
            TerminalWindowName = settings.TerminalWindowName;
            TerminalExecutablePath = settings.TerminalExecutablePath;
            ClaudeExecutablePath = settings.ClaudeExecutablePath;
            JiraBaseUrl = settings.JiraBaseUrl;
            HiddenProjectFolders.Clear();

            foreach (var folder in settings.HiddenProjectFolders)
            {
                HiddenProjectFolders.Add(folder);
            }
            HoldoutRemote = settings.HoldoutRemote;
            ProjectsFolder = settings.ProjectsFolder;
            WorktreesFolder = settings.WorktreesFolder;
            ClonesFolder = settings.ClonesFolder;
            ShowsProjectsIntro = string.IsNullOrWhiteSpace(settings.ProjectsFolder);
            IncludePreReleases = settings.IncludePreReleases;
            SessionPreamble = settings.SessionPreamble;
            ContinuePrompt = settings.ContinuePrompt;
            StopPrompt = settings.StopPrompt;
            SetStartMode(settings.DefaultStartMode);
            IdentityPrompt = string.IsNullOrWhiteSpace(settings.IdentityPrompt)
                ? WorkstreamIdentity.DefaultTemplate
                : settings.IdentityPrompt;
            LaneList = string.Join(Environment.NewLine, settings.Lanes);
            AreaList = string.Join(Environment.NewLine, settings.Areas);
        }
        finally
        {
            _loading = false;
        }
    }

    // Reads the file again before each write, so a row never puts back what another part of Theseus wrote meanwhile.
    private void Write(Func<TheseusSettings, TheseusSettings> change)
    {
        if (_loading)
        {
            return;
        }

        _store.Write(change(_store.Read()));
        _changed = true;
    }
}
