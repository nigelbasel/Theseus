using Theseus.Core.Diagnostics;
using Theseus.Core.Trust;

namespace Theseus.App.ViewModels;

/// <summary>
/// Options › Folders, and the first-run step: the base folders Theseus keeps trusted, set once, and Undo (objective
/// 1.3f, Nigel 2026-11-27). There is no consent switch and no list of folders to click: choosing the base folders is the
/// consent, and anything outside them Claude Code asks about as usual. Each action takes effect at once, not at Save.
/// </summary>
public sealed class FolderTrustViewModel : ObservableObject
{
    private readonly BaseFolderTrust _base;
    private string _result = string.Empty;

    /// <summary>Creates the section over the base folders.</summary>
    public FolderTrustViewModel(BaseFolderTrust baseFolders)
    {
        _base = baseFolders;
        AddCommand = new RelayCommand(_ => Act(Add));
        ResetCommand = new RelayCommand(_ => Act(Reset));
        UndoCommand = new RelayCommand(_ => Act(Undo), _ => _base.Folders.Count > 0 || _base.Records.CanUndo);
    }

    /// <summary>
    /// Says what an action came to in the status bar too: the line under the section alone left Nigel unable to tell
    /// whether anything had changed (2026-11-23). Set by the shell; null says nothing more.
    /// </summary>
    public Action<string>? Said { get; set; }

    /// <summary>Asks the owner for a folder to add, or null when he cancels. Set by the app.</summary>
    public Func<string?>? ChooseFolder { get; set; }

    /// <summary>The base folders, one per line, or a line saying there are none.</summary>
    public string Folders => _base.Folders.Count == 0 ? "None: Claude Code asks about every folder as usual." : string.Join(Environment.NewLine, _base.Folders);

    /// <summary>What Theseus writes, and what it never trusts, said once.</summary>
    public string Explanation => _base.Explanation();

    /// <summary>What the last action came to, or empty.</summary>
    public string Result
    {
        get => _result;
        private set => Set(ref _result, value);
    }

    /// <summary>Whether the base folders are set, so the first-run step has nothing left to do.</summary>
    public bool IsSet => _base.Folders.Count > 0;

    /// <summary>Add…: a folder chosen by the owner becomes a base folder, unless it may never be one.</summary>
    public RelayCommand AddCommand { get; }

    /// <summary>Reset: the base folders go back to the projects folder's root and the worktrees folder.</summary>
    public RelayCommand ResetCommand { get; }

    /// <summary>Undo: takes back every record Theseus set and empties the base folders.</summary>
    public RelayCommand UndoCommand { get; }

    /// <summary>The folders a start into these would be asked about now, so the board can say so and expect the question.</summary>
    public IReadOnlyList<FolderTrustNeed> NeedsFor(IEnumerable<string> folders) => _base.Records.Needs(folders);

    /// <summary>Keeps the base folders trusted, at startup and before each launch.</summary>
    public BaseTrustOutcome Keep() => _base.Keep();

    /// <summary>Sets the defaults on an upgraded install; true when it did, for the one banner that says so.</summary>
    public bool SetDefaultsOnUpgrade() => _base.SetDefaultsOnUpgrade();

    /// <summary>Reads the base folders again, as Options opens.</summary>
    public void Load()
    {
        Result = string.Empty;
        RaiseState();
    }

    private string Add()
    {
        if (ChooseFolder?.Invoke() is not { Length: > 0 } folder)
        {
            return "Nothing was changed.";
        }

        if (_base.Refusal(folder) is { } refusal)
        {
            return $"Not added: {refusal}.";
        }

        return _base.Add(folder) ? Trusted($"Added {folder}.") : $"{folder} is a base folder already.";
    }

    private string Reset()
    {
        _base.Reset();

        return Trusted("The base folders are back to their defaults.");
    }

    private string Undo() => _base.Undo().Message;

    private string Trusted(string said)
    {
        var kept = _base.Keep();

        return kept.Failure ?? (kept.Trusted.Count == 0 ? said : $"{said} Trusted {string.Join(", ", kept.Trusted)} in Claude Code.");
    }

    private void Act(Func<string> action)
    {
        var message = action();
        RaiseState();
        Result = message;
        Said?.Invoke(message);
        TheseusLog.Information("Folder trust: {Result}", message);
    }

    private void RaiseState()
    {
        Raise(nameof(Folders));
        Raise(nameof(IsSet));
    }
}
