using Theseus.Core.Diagnostics;
using Theseus.Core.Trust;

namespace Theseus.App.ViewModels;

/// <summary>
/// Options, Folder trust: the folders Theseus starts sessions in that Claude Code will ask about, Trust these folders…
/// after showing the exact change, and Undo (project-view-usability item 7, Nigel 2026-11-23), the way Install the
/// holdout guard works. Each acts at once, not at Save.
/// </summary>
public sealed class FolderTrustViewModel : ObservableObject
{
    private readonly ClaudeFolderTrust _trust;
    private readonly Func<IEnumerable<string>> _folders;
    private IReadOnlyList<FolderTrustNeed> _needs = [];
    private string _result = string.Empty;

    /// <summary>Creates the section over Claude Code's trust records and the folders the board's cards start in.</summary>
    public FolderTrustViewModel(ClaudeFolderTrust trust, Func<IEnumerable<string>> folders)
    {
        _trust = trust;
        _folders = folders;
        TrustCommand = new RelayCommand(_ => TrustThem(), _ => _needs.Count > 0);
        UndoCommand = new RelayCommand(_ => Show(_trust.Undo()), _ => _trust.CanUndo);
    }

    /// <summary>
    /// Says what Trust or Undo came to in the status bar too: the line under the section alone left Nigel unable to tell
    /// whether anything had changed (2026-11-23). Set by the shell; null says nothing more.
    /// </summary>
    public Action<string>? Said { get; set; }

    /// <summary>Asks the owner to agree to a change, shown in full; true to make it. Set by the app.</summary>
    public Func<string, bool>? ConfirmChange { get; set; }

    /// <summary>What Claude Code will ask about, in one line.</summary>
    public string Line => _needs.Count switch
    {
        0 => "Claude Code will not ask to trust any folder Theseus starts sessions in.",
        1 => "Claude Code will ask you to trust 1 folder when a session starts in it or adds it.",
        _ => $"Claude Code will ask you to trust {_needs.Count} folders when a session starts in them or adds them.",
    };

    /// <summary>The records that would be set, one per line.</summary>
    public string Folders => string.Join(Environment.NewLine, _needs.Select(need => need.Key));

    /// <summary>Whether there is anything to trust.</summary>
    public bool HasNeeds => _needs.Count > 0;

    /// <summary>What the last action came to, or empty.</summary>
    public string Result
    {
        get => _result;
        private set => Set(ref _result, value);
    }

    /// <summary>Trust these folders…: shows the change, and makes it only if the owner agrees.</summary>
    public RelayCommand TrustCommand { get; }

    /// <summary>Undo: takes back the records Theseus set.</summary>
    public RelayCommand UndoCommand { get; }

    /// <summary>The folders a start into these would be asked about now.</summary>
    public IReadOnlyList<FolderTrustNeed> NeedsFor(IEnumerable<string> folders) => _trust.Needs(folders);

    /// <summary>Reads the records again, as Options opens.</summary>
    public void Load()
    {
        _needs = _trust.Needs(_folders());
        Result = string.Empty;
        RaiseState();
    }

    private void TrustThem()
    {
        _needs = _trust.Needs(_folders());

        if (_needs.Count == 0)
        {
            RaiseState();

            return;
        }

        if (ConfirmChange?.Invoke(_trust.Change(_needs)) != true)
        {
            Result = "Nothing was changed.";

            return;
        }

        Show(_trust.Trust(_needs));
    }

    private void Show(FolderTrustOutcome outcome)
    {
        _needs = _trust.Needs(_folders());
        RaiseState();
        Result = outcome.Message;
        Said?.Invoke(outcome.Message);
        TheseusLog.Information("Folder trust: {Result}", outcome.Message);
    }

    private void RaiseState()
    {
        Raise(nameof(Line));
        Raise(nameof(Folders));
        Raise(nameof(HasNeeds));
    }
}
