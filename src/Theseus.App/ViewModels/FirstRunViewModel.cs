using Theseus.Core.Diagnostics;
using Theseus.Core.Projects;
using Theseus.Core.Settings;
using Theseus.Core.Setup;

namespace Theseus.App.ViewModels;

/// <summary>
/// The first-run setup (Nigel, 2026-11-25: "a startup dialog that runs at startup if the values are not set, and it asks a
/// few questions to get initial setup right"): only the steps still missing, walked with Back, Next, Skip and Finish. It opens
/// by itself while no projects folder is set and it has not been finished or skipped, from Tools › Set up Theseus…, and from
/// New project when there is no projects folder. Every choice applies at once, as Options does, so Skip loses nothing chosen.
/// </summary>
public sealed class FirstRunViewModel : ObservableObject
{
    private readonly ISettingsStore _settings;
    private readonly StopHookRegistration _hook;
    private readonly Func<IReadOnlyList<ToolFinding>> _checkTools;
    private readonly Action _changed;
    private IReadOnlyList<SetupStep> _steps = [];
    private IReadOnlyList<ToolFinding> _tools = [];
    private int _index;
    private bool _isOpen;
    private string _hookResult = string.Empty;

    /// <summary>Creates the setup.</summary>
    /// <param name="settings">Where the folders and the done flag are kept.</param>
    /// <param name="hook">The stop hook's registration in Claude Code's settings.</param>
    /// <param name="guard">Options › Holdouts' guard section, whose Install and Add the hook entry… the guard step offers.</param>
    /// <param name="checkTools">Reads the tools the method needs; run each time the setup opens.</param>
    /// <param name="changed">Tells the board a folder changed, so New project and the project cards follow at once.</param>
    public FirstRunViewModel(ISettingsStore settings, StopHookRegistration hook, HoldoutGuardViewModel guard,
        Func<IReadOnlyList<ToolFinding>> checkTools, Action changed)
    {
        _settings = settings;
        _hook = hook;
        Guard = guard;
        _checkTools = checkTools;
        _changed = changed;
        IsGuardReady = () => Guard.IsReady;

        OpenCommand = new RelayCommand(_ => Open());
        BackCommand = new RelayCommand(_ => Move(-1), _ => _isOpen && _index > 0);
        NextCommand = new RelayCommand(_ => Move(1), _ => _isOpen && !IsLast);
        SkipCommand = new RelayCommand(_ => Close());
        FinishCommand = new RelayCommand(_ => Close());
        ForwardCommand = new RelayCommand(_ =>
        {
            if (IsLast)
            {
                Close();
            }
            else
            {
                Move(1);
            }
        });
        AddHookCommand = new RelayCommand(_ => AddHook(), _ => !HookIsRegistered);
    }

    /// <summary>Shows the setup's window; set by the app. The window closes itself when <see cref="IsOpen"/> turns false.</summary>
    public Action? ShowWindow { get; set; }

    /// <summary>Asks the owner to agree to a change, shown in full; true to make it. Set by the app.</summary>
    public Func<string, bool>? ConfirmChange { get; set; }

    /// <summary>Whether the holdout guard step is done; the guard section's own reading unless a test says otherwise.</summary>
    public Func<bool> IsGuardReady { get; set; }

    /// <summary>The guard step's actions, shared with Options › Holdouts.</summary>
    public HoldoutGuardViewModel Guard { get; }

    /// <summary>Tools › Set up Theseus…: opens on the steps still missing.</summary>
    public RelayCommand OpenCommand { get; }

    /// <summary>Back to the step before.</summary>
    public RelayCommand BackCommand { get; }

    /// <summary>On to the next step, whether or not this one was done.</summary>
    public RelayCommand NextCommand { get; }

    /// <summary>Closes the setup, remembered so it does not open by itself again.</summary>
    public RelayCommand SkipCommand { get; }

    /// <summary>Closes the setup from its last step, remembered as Skip is.</summary>
    public RelayCommand FinishCommand { get; }

    /// <summary>The footer's default button: Next, and Finish on the last step, as a Windows wizard's is.</summary>
    public RelayCommand ForwardCommand { get; }

    /// <summary>The default button's label, with its access key.</summary>
    public string ForwardLabel => IsLast ? "_Finish" : "_Next";

    /// <summary>Registers the stop hook, after showing the exact change and only on the owner's consent.</summary>
    public RelayCommand AddHookCommand { get; }

    /// <summary>Whether the setup is showing.</summary>
    public bool IsOpen
    {
        get => _isOpen;
        private set => Set(ref _isOpen, value);
    }

    /// <summary>The steps shown this time, decided as it opened.</summary>
    public IReadOnlyList<SetupStep> Steps => _steps;

    /// <summary>The step showing, or null when nothing is missing.</summary>
    public SetupStep? Current => _steps.Count > 0 ? _steps[_index] : null;

    /// <summary>Whether nothing was missing as it opened.</summary>
    public bool IsAllSet => _steps.Count == 0;

    /// <summary>Whether this is the last step, where Finish takes Next's place.</summary>
    public bool IsLast => _index >= _steps.Count - 1;

    /// <summary>Whether the folder step is showing.</summary>
    public bool IsFoldersStep => Current == SetupStep.Folders;

    /// <summary>Whether the stop hook step is showing.</summary>
    public bool IsHookStep => Current == SetupStep.Hook;

    /// <summary>Whether the holdout guard step is showing.</summary>
    public bool IsGuardStep => Current == SetupStep.HoldoutGuard;

    /// <summary>Whether the tools step is showing.</summary>
    public bool IsToolsStep => Current == SetupStep.Tools;

    /// <summary>The heading, such as "Step 1 of 3: Folders".</summary>
    public string StepHeading => Current is { } step
        ? $"Step {_index + 1} of {_steps.Count}: {Title(step)}"
        : "Everything is set up";

    /// <summary>The projects folder as chosen.</summary>
    public string ProjectsFolder
    {
        get => _settings.Read().ProjectsFolder;
        set => Choose(value, (settings, folder) => settings with { ProjectsFolder = folder });
    }

    /// <summary>The worktrees folder as chosen.</summary>
    public string WorktreesFolder
    {
        get => _settings.Read().WorktreesFolder;
        set => Choose(value, (settings, folder) => settings with { WorktreesFolder = folder });
    }

    /// <summary>The clones folder as chosen.</summary>
    public string ClonesFolder
    {
        get => _settings.Read().ClonesFolder;
        set => Choose(value, (settings, folder) => settings with { ClonesFolder = folder });
    }

    /// <summary>The projects folder, or what having none means.</summary>
    public string ProjectsFolderValue => Value(ProjectsFolder, "None chosen: New project needs one");

    /// <summary>The worktrees folder, or none.</summary>
    public string WorktreesFolderValue => Value(WorktreesFolder, "None chosen");

    /// <summary>The clones folder, or none.</summary>
    public string ClonesFolderValue => Value(ClonesFolder, "None chosen");

    /// <summary>A warning when the projects folder is inside a code repository, or empty.</summary>
    public string ProjectsFolderWarning => FirstRunSetup.ProjectsFolderWarning(ProjectsFolder) ?? string.Empty;

    /// <summary>Whether there is a warning to show about the projects folder.</summary>
    public bool HasProjectsFolderWarning => ProjectsFolderWarning.Length > 0;

    /// <summary>Whether the stop hook is registered.</summary>
    public bool HookIsRegistered => _hook.IsRegistered;

    /// <summary>The hook step's state in a sentence.</summary>
    public string HookLine => HookIsRegistered
        ? "The stop hook is registered. Sessions started from now on run it."
        : "The stop hook is not registered, so Stop cannot ask a session for its handover.";

    /// <summary>The exact entry the hook step would add, shown before anything is asked.</summary>
    public string HookEntry => _hook.Entry;

    /// <summary>Where the entry goes.</summary>
    public string HookSettingsPath => _hook.SettingsPath;

    /// <summary>What the last hook action came to, or empty.</summary>
    public string HookResult
    {
        get => _hookResult;
        private set => Set(ref _hookResult, value);
    }

    /// <summary>What was found of each tool, as the setup opened.</summary>
    public IReadOnlyList<ToolFinding> Tools => _tools;

    /// <summary>Opens on the steps still missing.</summary>
    public void Open() => Open(null);

    /// <summary>
    /// New project's way in: with no projects folder, opens the folder step and returns true, so New project explains itself
    /// rather than being greyed out (brief step 2); with one, returns false and New project goes ahead.
    /// </summary>
    public bool OpenForNewProject()
    {
        if (!string.IsNullOrWhiteSpace(ProjectsFolder))
        {
            return false;
        }

        Open(SetupStep.Folders);

        return true;
    }

    /// <summary>The window was closed by its title bar or Escape: the same as Skip.</summary>
    public void Closed()
    {
        if (IsOpen)
        {
            Close();
        }
    }

    private void Open(SetupStep? first)
    {
        _tools = _checkTools();
        Guard.Load();
        _steps = FirstRunSetup.Steps(new SetupFacts(!string.IsNullOrWhiteSpace(ProjectsFolder), _hook.IsRegistered, IsGuardReady(), _tools),
            first);
        _index = 0;
        HookResult = string.Empty;
        TheseusLog.Information("First-run setup opened on {Steps}", _steps.Count == 0 ? "nothing missing" : string.Join(", ", _steps));
        RaiseAll();

        if (!IsOpen)
        {
            IsOpen = true;
            ShowWindow?.Invoke();
        }
    }

    private void Close()
    {
        if (!_settings.Read().FirstRunSetupDone)
        {
            _settings.Write(_settings.Read() with { FirstRunSetupDone = true });
        }

        IsOpen = false;
    }

    private void Move(int by)
    {
        _index = Math.Clamp(_index + by, 0, Math.Max(0, _steps.Count - 1));
        RaiseAll();

        // Back and Next are enabled by step; WPF asks again only on input, which a key or click on Next may not yet have given.
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }

    private void Choose(string typed, Func<TheseusSettings, string, TheseusSettings> change)
    {
        _settings.Write(change(_settings.Read(), ProjectsFolderPath.Normalize(typed)));
        Raise(nameof(ProjectsFolder));
        Raise(nameof(WorktreesFolder));
        Raise(nameof(ClonesFolder));
        Raise(nameof(ProjectsFolderValue));
        Raise(nameof(WorktreesFolderValue));
        Raise(nameof(ClonesFolderValue));
        Raise(nameof(ProjectsFolderWarning));
        Raise(nameof(HasProjectsFolderWarning));
        _changed();
    }

    private void AddHook()
    {
        if (_hook.Change() is not { } change)
        {
            HookResult = HookIsRegistered
                ? "The stop hook is registered already."
                : $"{_hook.SettingsPath} cannot be changed safely, so nothing was changed. Add the entry by hand.";
            Raise(nameof(HookIsRegistered));
            Raise(nameof(HookLine));

            return;
        }

        if (ConfirmChange?.Invoke(change) != true)
        {
            HookResult = "Nothing was changed.";

            return;
        }

        var outcome = _hook.Add();
        HookResult = outcome.Message;
        TheseusLog.Information("Stop hook: {Result}", outcome.Message);
        Raise(nameof(HookIsRegistered));
        Raise(nameof(HookLine));
    }

    private void RaiseAll()
    {
        Raise(nameof(Steps));
        Raise(nameof(Current));
        Raise(nameof(IsAllSet));
        Raise(nameof(IsLast));
        Raise(nameof(ForwardLabel));
        Raise(nameof(IsFoldersStep));
        Raise(nameof(IsHookStep));
        Raise(nameof(IsGuardStep));
        Raise(nameof(IsToolsStep));
        Raise(nameof(StepHeading));
        Raise(nameof(Tools));
        Raise(nameof(HookIsRegistered));
        Raise(nameof(HookLine));
        Raise(nameof(ProjectsFolderValue));
        Raise(nameof(WorktreesFolderValue));
        Raise(nameof(ClonesFolderValue));
        Raise(nameof(ProjectsFolderWarning));
        Raise(nameof(HasProjectsFolderWarning));
    }

    private static string Title(SetupStep step) => step switch
    {
        SetupStep.Folders => "Folders",
        SetupStep.Hook => "The stop hook",
        SetupStep.HoldoutGuard => "The holdout guard",
        _ => "The tools",
    };

    private static string Value(string folder, string none) => string.IsNullOrWhiteSpace(folder) ? none : folder;
}
