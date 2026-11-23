using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// A checklist confirmation (Nigel, 2026-11-23): its rows, all ticked to begin with, and a foot recounted as ticks change.
/// </summary>
public sealed class ChecklistViewModel : ObservableObject
{
    private readonly Checklist _list;

    /// <summary>Creates the dialog's model over a checklist.</summary>
    public ChecklistViewModel(Checklist list)
    {
        _list = list;
        Rows = [.. list.Rows.Select(row => new ChecklistRowViewModel(row, Recount))];
    }

    /// <summary>The dialog's title.</summary>
    public string Title => _list.Title;

    /// <summary>What the press does.</summary>
    public string Intro => _list.Intro;

    /// <summary>The press's own button.</summary>
    public string ConfirmLabel => _list.ConfirmLabel;

    /// <summary>The rows.</summary>
    public IReadOnlyList<ChecklistRowViewModel> Rows { get; }

    /// <summary>The keys of the rows ticked, in order.</summary>
    public IReadOnlyList<string> Ticked => [.. Rows.Where(row => row.IsTicked).Select(row => row.Row.Key)];

    /// <summary>What the press will do for the rows ticked.</summary>
    public string Foot => _list.Foot(Ticked);

    /// <summary>Whether anything is ticked for the press to act on.</summary>
    public bool CanConfirm => Rows.Any(row => row.IsTicked);

    private void Recount()
    {
        Raise(nameof(Foot));
        Raise(nameof(Ticked));
        Raise(nameof(CanConfirm));
    }
}
