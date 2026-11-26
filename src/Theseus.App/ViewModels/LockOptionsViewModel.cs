using Theseus.Core.Diagnostics;
using Theseus.Core.Locks;
using Theseus.Core.Settings;

namespace Theseus.App.ViewModels;

/// <summary>
/// Options, Locks: the installed lock client, guard and command classes with Install and Undo, and the card whose
/// sessions may rule on resources the lock table does not know.
/// </summary>
public sealed class LockOptionsViewModel : ObservableObject
{
    public const string NoArbiter = "None";

    private readonly LockToolsInstallation _installation;
    private readonly ISettingsStore _store;
    private readonly Func<IReadOnlyList<(string Key, string Title)>> _cards;
    private LockToolsState _state;
    private string _result = string.Empty;
    private IReadOnlyList<string> _arbiterChoices = [NoArbiter];
    private string _arbiter = NoArbiter;
    private bool _loading;

    public LockOptionsViewModel(LockToolsInstallation installation, ISettingsStore store, Func<IReadOnlyList<(string Key, string Title)>> cards)
    {
        _installation = installation;
        _store = store;
        _cards = cards;
        _state = installation.State();
        InstallCommand = new RelayCommand(_ => Show(_installation.Install()));
        UndoCommand = new RelayCommand(_ => Show(_installation.Undo()), _ => _state.CanUndo);
    }

    public string Line => _state.Line;

    public string Folder => _state.Files.Count == 0 ? string.Empty : System.IO.Path.GetDirectoryName(_state.Files[0].Path) ?? string.Empty;

    public string Result
    {
        get => _result;
        private set => Set(ref _result, value);
    }

    public RelayCommand InstallCommand { get; }

    public RelayCommand UndoCommand { get; }

    /// <summary>None, then every card on the board by title, for the arbiter.</summary>
    public IReadOnlyList<string> ArbiterChoices
    {
        get => _arbiterChoices;
        private set => Set(ref _arbiterChoices, value);
    }

    public string Arbiter
    {
        get => _arbiter;
        set
        {
            if (!Set(ref _arbiter, value) || _loading)
            {
                return;
            }

            string? key = value == NoArbiter ? null : _cards().FirstOrDefault(card => card.Title == value).Key;
            _store.Write(_store.Read() with { LockArbiterCard = key });
            TheseusLog.Information("Lock arbiter set to {Card}", key ?? "none");
        }
    }

    public void Load()
    {
        _loading = true;

        try
        {
            _state = _installation.State();
            Result = string.Empty;
            IReadOnlyList<(string Key, string Title)> cards = _cards();
            ArbiterChoices = [NoArbiter, .. cards.Select(card => card.Title)];
            string? key = _store.Read().LockArbiterCard;
            Arbiter = cards.FirstOrDefault(card => string.Equals(card.Key, key, StringComparison.OrdinalIgnoreCase)).Title ?? NoArbiter;
            RaiseState();
        }
        finally
        {
            _loading = false;
        }
    }

    private void Show(LockToolsOutcome outcome)
    {
        _state = _installation.State();
        RaiseState();
        Result = outcome.Message;
        TheseusLog.Information("Lock tools: {Result}", outcome.Message);
    }

    private void RaiseState()
    {
        Raise(nameof(Line));
        Raise(nameof(Folder));
    }
}
