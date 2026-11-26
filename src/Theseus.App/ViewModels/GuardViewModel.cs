using Theseus.Core.Diagnostics;
using Theseus.Core.Guards;

namespace Theseus.App.ViewModels;

/// <summary>
/// One guard in Options, Guards: its installed state, Install, Undo, and a missing hook entry added once its exact change
/// is shown (verifier-runs brief step 3, Nigel 2026-11-22; every shipped guard, guards-ship-with-theseus, 2026-11-26).
/// Each acts at once.
/// </summary>
public sealed class GuardViewModel : ObservableObject
{
    private readonly GuardInstallation _installation;
    private GuardState _state;
    private string _result = string.Empty;

    /// <summary>Creates the row over a guard in a Claude Code configuration folder.</summary>
    public GuardViewModel(GuardInstallation installation)
    {
        _installation = installation;
        _state = installation.State();
        InstallCommand = new RelayCommand(_ => Show(_installation.Install()));
        UndoCommand = new RelayCommand(_ => Show(_installation.Undo()), _ => _state.CanUndo);
        AddHookCommand = new RelayCommand(_ => AddHook(), _ => !_state.HookIsRegistered);
    }

    /// <summary>Asks the owner to agree to a change, shown in full; true to make it. Set by the app.</summary>
    public Func<string, bool>? ConfirmChange { get; set; }

    /// <summary>The guard's name, such as "trunk guard".</summary>
    public string Name => _installation.Definition.Name;

    /// <summary>The guard's name as its row's label, such as "Trunk guard".</summary>
    public string Title => _installation.Definition.Title;

    /// <summary>What the guard does, for its row.</summary>
    public string Explanation => _installation.Definition.Explanation + " It runs from Claude Code's settings, so it covers every session on this machine.";

    /// <summary>The guard's state, such as "Up to date".</summary>
    public string Line => _state.Line;

    /// <summary>Where the hook runs the guard from.</summary>
    public string GuardPath => _state.GuardPath;

    /// <summary>Whether Claude Code's settings run the guard; when not, no session is guarded by it.</summary>
    public bool HookIsMissing => !_state.HookIsRegistered;

    /// <summary>What the missing hook means, for the row.</summary>
    public string HookMissingLine => $"Claude Code's settings do not run the {Name}, so no session is guarded by it.";

    /// <summary>Whether the guard is installed, this Theseus's own, and run by Claude Code: the setup's step is then done.</summary>
    public bool IsReady => _state.IsReady;

    /// <summary>What the last action came to, or empty.</summary>
    public string Result
    {
        get => _result;
        private set => Set(ref _result, value);
    }

    /// <summary>Install: copies this Theseus's guard into place and checks it took.</summary>
    public RelayCommand InstallCommand { get; }

    /// <summary>Undo: puts back the copies the last install replaced.</summary>
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

    private void Show(GuardOutcome outcome)
    {
        _state = _installation.State();
        RaiseState();
        Result = outcome.Message;
        TheseusLog.Information("{Guard}: {Result}", Title, outcome.Message);
    }

    private void RaiseState()
    {
        Raise(nameof(Line));
        Raise(nameof(GuardPath));
        Raise(nameof(HookIsMissing));
        Raise(nameof(IsReady));
    }
}
