using Theseus.Core.Diagnostics;
using Theseus.Core.Links;

namespace Theseus.App.ViewModels;

/// <summary>
/// Options, Holdouts: the installed holdout guard's state, Install the holdout guard, Undo, and a missing hook entry
/// added once its exact change is shown (verifier-runs brief step 3, Nigel 2026-11-22). Each acts at once.
/// </summary>
public sealed class HoldoutGuardViewModel : ObservableObject
{
    private readonly HoldoutGuardInstallation _installation;
    private HoldoutGuardState _state;
    private string _result = string.Empty;

    /// <summary>Creates the section over a Claude Code configuration folder's guard.</summary>
    public HoldoutGuardViewModel(HoldoutGuardInstallation installation)
    {
        _installation = installation;
        _state = installation.State();
        InstallCommand = new RelayCommand(_ => Show(_installation.Install()));
        UndoCommand = new RelayCommand(_ => Show(_installation.Undo()), _ => _state.CanUndo);
        AddHookCommand = new RelayCommand(_ => AddHook(), _ => !_state.HookIsRegistered);
    }

    /// <summary>Asks the owner to agree to a change, shown in full; true to make it. Set by the app.</summary>
    public Func<string, bool>? ConfirmChange { get; set; }

    /// <summary>The guard's state, such as "Up to date".</summary>
    public string Line => _state.Line;

    /// <summary>Where the hook runs the guard from.</summary>
    public string GuardPath => _state.GuardPath;

    /// <summary>Whether Claude Code's settings run the guard; when not, no session is guarded at all.</summary>
    public bool HookIsMissing => !_state.HookIsRegistered;

    /// <summary>What the last action came to, or empty.</summary>
    public string Result
    {
        get => _result;
        private set => Set(ref _result, value);
    }

    /// <summary>Install the holdout guard: copies this Theseus's guard into place and checks it took.</summary>
    public RelayCommand InstallCommand { get; }

    /// <summary>Undo: puts back the copy the last install replaced.</summary>
    public RelayCommand UndoCommand { get; }

    /// <summary>Adds the missing hook entry, after showing the change.</summary>
    public RelayCommand AddHookCommand { get; }

    /// <summary>Reads the guard's state again, as Options opens.</summary>
    public void Load()
    {
        _state = _installation.State();
        Result = string.Empty;
        RaiseState();
    }

    private void AddHook()
    {
        if (_installation.HookEntryChange() is not { } change)
        {
            Load();

            return;
        }

        if (ConfirmChange?.Invoke(change) != true)
        {
            Result = "Nothing was changed.";

            return;
        }

        Show(_installation.AddHookEntry());
    }

    private void Show(HoldoutGuardOutcome outcome)
    {
        _state = _installation.State();
        RaiseState();
        Result = outcome.Message;
        TheseusLog.Information("Holdout guard: {Result}", outcome.Message);
    }

    private void RaiseState()
    {
        Raise(nameof(Line));
        Raise(nameof(GuardPath));
        Raise(nameof(HookIsMissing));
    }
}
