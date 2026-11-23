using System.IO;
using Theseus.Core.Domain;
using Theseus.Core.Launching;
using Theseus.Core.Projects;
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
    private string _holdoutFolder = string.Empty;
    private string _holdoutRemote = string.Empty;
    private string _projectsFolder = string.Empty;
    private string _worktreesFolder = string.Empty;
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

    /// <summary>Creates the panel over the settings file.</summary>
    public SettingsViewModel(ISettingsStore store, Action saved)
    {
        _store = store;
        _saved = saved;

        OpenCommand = new RelayCommand(_ => Open());
        ResetProjectHoldoutCommand = new RelayCommand(parameter =>
            {
                if (parameter is HoldoutRowViewModel row && _projectHoldouts.Remove(row.Project))
                {
                    _resetHere.Add(row.Project);
                    Raise(nameof(HoldoutRows));
                }
            },
            parameter => parameter is HoldoutRowViewModel { IsChosenHere: true });
        ShowProjectFolderCommand = new RelayCommand(parameter =>
        {
            if (parameter is string name)
            {
                HiddenProjectFolders.Remove(name);
            }
        });
        CloseCommand = new RelayCommand(_ => IsOpen = false);
        SaveCommand = new RelayCommand(_ => Save());
        ResetIdentityPromptCommand = new RelayCommand(_ => IdentityPrompt = WorkstreamIdentity.DefaultTemplate,
            _ => IdentityPrompt != WorkstreamIdentity.DefaultTemplate);

        Load(store.Read());
    }

    /// <summary>Shows the panel, discarding any half-made edits from last time.</summary>
    public RelayCommand OpenCommand { get; }

    /// <summary>The themes to choose between.</summary>
    public IReadOnlyList<AppTheme> Themes { get; } = Enum.GetValues<AppTheme>();

    /// <summary>
    /// Which theme to paint in. Applied and saved as soon as it is chosen, not on Save, because a
    /// theme is judged by looking at it.
    /// </summary>
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
        }
    }

    /// <summary>Hides the panel without saving.</summary>
    public RelayCommand CloseCommand { get; }

    /// <summary>Writes the settings and closes.</summary>
    public RelayCommand SaveCommand { get; }

    /// <summary>Puts the built-in identity wording back.</summary>
    public RelayCommand ResetIdentityPromptCommand { get; }

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
                Raise(nameof(ClaudeState));
            }
        }
    }

    /// <summary>What the Claude Code setting resolves to right now.</summary>
    public string ClaudeState =>
        new ClaudeLocator(_claudeExecutablePath).Locate() ?? "not found";

    /// <summary>
    /// The holdout scenarios' folder the owner chose, read by the holdout guard and used by Verify with holdouts…; empty for
    /// the default, a folder per project under <see cref="Theseus.Core.Links.HoldoutLocation.DefaultRoot"/> (1.1e AC 6c).
    /// </summary>
    public string HoldoutFolder
    {
        get => _holdoutFolder;
        set
        {
            if (Set(ref _holdoutFolder, value))
            {
                Raise(nameof(HoldoutFolderInUse));
            }
        }
    }

    /// <summary>What the single folder does (1.1e AC 6e): empty, each project's is found by convention below.</summary>
    public string HoldoutFolderInUse => string.IsNullOrWhiteSpace(_holdoutFolder)
        ? "None: each project's folder is found by convention, listed below"
        : $"{_holdoutFolder.Trim()}, for every project that has no folder of its own";

    /// <summary>
    /// Each project's holdout folder and how it was found (1.1e AC 6e), from the board's last look, with this dialog's own
    /// choices over it until saved.
    /// </summary>
    public IReadOnlyList<HoldoutRowViewModel> HoldoutRows =>
        [.. _holdoutEntries.Select(entry => _projectHoldouts.TryGetValue(entry.Project, out var chosen)
            ? new HoldoutRowViewModel(entry.Project, chosen, "chosen by you", true)
            : _resetHere.Contains(entry.Project)
                ? new HoldoutRowViewModel(entry.Project, "found by convention once saved", string.Empty, false)
                : new HoldoutRowViewModel(entry.Project, entry.Folder, entry.HowFound, false) { IsTheseusOwn = entry.Source == Theseus.Core.Links.HoldoutSource.TheseusOwn })];

    /// <summary>
    /// Why a folder cannot be a project's holdout, or null (review of PR 7, finding 9): the guard protects neither a whole
    /// drive nor a relative path.
    /// </summary>
    public static string? WhyNotHoldout(string folder) =>
        string.IsNullOrWhiteSpace(folder) || !System.IO.Path.IsPathFullyQualified(folder)
            ? "Choose a full path, such as C:\\src\\Repo-holdout."
            : string.Equals(System.IO.Path.GetPathRoot(folder)?.TrimEnd('\\', '/'), folder.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)
                ? "A whole drive cannot be a holdout folder: the guard would protect nothing."
                : null;

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

    /// <summary>Chooses a project's holdout folder, saved with the rest (1.1e AC 6e).</summary>
    public string? ChooseProjectHoldout(string project, string folder)
    {
        if (WhyNotHoldout(folder) is { } why)
        {
            return why;
        }

        _projectHoldouts[project] = folder;
        _resetHere.Remove(project);
        Raise(nameof(HoldoutRows));

        return null;
    }

    private readonly HashSet<string> _resetHere = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Reset: the project's folder is found by convention again, once saved.</summary>
    public RelayCommand ResetProjectHoldoutCommand { get; }

    private IReadOnlyList<Theseus.Core.Links.HoldoutFolderEntry> _holdoutEntries = [];
    private readonly Dictionary<string, string> _projectHoldouts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The folders said not to be projects (1.1e AC 20), each to be shown again; saved with the rest.</summary>
    public System.Collections.ObjectModel.ObservableCollection<string> HiddenProjectFolders { get; } = [];

    /// <summary>Show again: a folder said not to be a project is shown as a project card again, once saved.</summary>
    public RelayCommand ShowProjectFolderCommand { get; }

    /// <summary>What <see cref="FocusField"/> names for the holdout folder.</summary>
    public const string HoldoutFolderField = "HoldoutFolder";

    /// <summary>The field Options opens at, such as the holdout folder from Set the holdout folder… (1.1e AC 6a); null for none.</summary>
    public string? FocusField { get; set; }

    /// <summary>The holdout's remote address, which the guard also keeps from implementing sessions.</summary>
    public string HoldoutRemote
    {
        get => _holdoutRemote;
        set => Set(ref _holdoutRemote, value);
    }

    /// <summary>The folder of project homes, as typed (1.1b AC 1).</summary>
    public string ProjectsFolder
    {
        get => _projectsFolder;
        set => Set(ref _projectsFolder, value);
    }

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
        set => Set(ref _worktreesFolder, value);
    }

    /// <summary>Whether Check for updates also offers the pre-releases built from a project's primary branch.</summary>
    public bool IncludePreReleases
    {
        get => _includePreReleases;
        set => Set(ref _includePreReleases, value);
    }

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

    /// <summary>What a session is told when it is asked to stop.</summary>
    public string StopPrompt
    {
        get => _stopPrompt;
        set => Set(ref _stopPrompt, value);
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
                Raise(nameof(IdentityPromptState));
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
                Raise(nameof(LaneListState));
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
                Raise(nameof(AreaListState));
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

    private IReadOnlyList<string> Lanes =>
        BoardLanes.Sanitise(_laneList.Split('\n', StringSplitOptions.RemoveEmptyEntries));

    private IReadOnlyList<string> Areas =>
        BoardAreas.Sanitise(_areaList.Split('\n', StringSplitOptions.RemoveEmptyEntries));

    private void SetStartMode(StartMode mode)
    {
        _defaultStartMode = mode;
        Raise(nameof(ResumeByDefault));
        Raise(nameof(FreshByDefault));
    }

    private void Open()
    {
        Load(_store.Read());
        Tags?.Load();
        Guard?.Load();
        FolderTrust?.Load();
        IsOpen = true;
    }

    /// <summary>Options, Tags (card-search-and-tags item 13); its changes apply at once, not at Save. Set by the shell.</summary>
    public TagsViewModel? Tags { get; set; }

    /// <summary>Options, Holdouts: the installed holdout guard and its Install button; acts at once. Set by the shell.</summary>
    public HoldoutGuardViewModel? Guard { get; set; }

    /// <summary>Options, Folder trust: Claude Code's trust of the folders sessions start in; acts at once. Set by the shell.</summary>
    public FolderTrustViewModel? FolderTrust { get; set; }

    private void Load(TheseusSettings settings)
    {
        PlanDirectory = settings.PlanDirectory;
        TerminalWindowName = settings.TerminalWindowName;
        TerminalExecutablePath = settings.TerminalExecutablePath;
        ClaudeExecutablePath = settings.ClaudeExecutablePath;
        JiraBaseUrl = settings.JiraBaseUrl;
        HoldoutFolder = settings.HoldoutFolder;
        _projectHoldouts.Clear();
        _resetHere.Clear();

        foreach (var (project, folder) in settings.ProjectHoldoutFolders)
        {
            _projectHoldouts[project] = folder;
        }

        Raise(nameof(HoldoutRows));
        HiddenProjectFolders.Clear();

        foreach (var folder in settings.HiddenProjectFolders)
        {
            HiddenProjectFolders.Add(folder);
        }
        HoldoutRemote = settings.HoldoutRemote;
        ProjectsFolder = settings.ProjectsFolder;
        WorktreesFolder = settings.WorktreesFolder;
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

    private void Save()
    {
        _store.Write(_store.Read() with
        {
            PlanDirectory = PlanDirectory,
            TerminalWindowName = TerminalWindowName,
            TerminalExecutablePath = TerminalExecutablePath,
            ClaudeExecutablePath = ClaudeExecutablePath,
            JiraBaseUrl = JiraBaseUrl,
            HoldoutFolder = HoldoutFolder.Trim(),
            ProjectHoldoutFolders = new Dictionary<string, string>(_projectHoldouts, StringComparer.OrdinalIgnoreCase),
            HiddenProjectFolders = [.. HiddenProjectFolders],
            HoldoutRemote = HoldoutRemote.Trim(),
            ProjectsFolder = ProjectsFolderPath.Normalize(ProjectsFolder),
            WorktreesFolder = ProjectsFolderPath.Normalize(WorktreesFolder),
            IncludePreReleases = IncludePreReleases,
            SessionPreamble = SessionPreamble,
            ContinuePrompt = ContinuePrompt,
            StopPrompt = StopPrompt,
            DefaultStartMode = _defaultStartMode,
            IdentityPrompt = IdentityPrompt == WorkstreamIdentity.DefaultTemplate ? string.Empty : IdentityPrompt,
            Lanes = Lanes,
            Areas = Areas,
        });

        IsOpen = false;
        _saved();
    }
}
