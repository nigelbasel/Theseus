using System.Windows.Input;
using Theseus.Core.Kanban;
using Theseus.Core.Settings;

namespace Theseus.App.ViewModels;

/// <summary>
/// The projects kanban, the All projects tab of the project view (projects-kanban spec, accepted 2026-11-26): a row per
/// project and a column per pipeline stage, each card a piece of work in the column of its current stage. Its cards are
/// the projects' own view boxes; it adds only where each sits and how the board is laid out.
/// </summary>
public sealed class KanbanViewModel : ObservableObject
{
    private readonly ISettingsStore _settings;
    private KanbanLayout _layout;

    private KanbanViewModel(ISettingsStore settings, KanbanLayout layout)
    {
        _settings = settings;
        _layout = layout;
        FoldAllCommand = new RelayCommand(_ => SetFolds(true));
        UnfoldAllCommand = new RelayCommand(_ => SetFolds(false));
    }

    /// <summary>A column per stage, in order (AC 8).</summary>
    public IReadOnlyList<KanbanColumnViewModel> Columns { get; private set; } = [];

    /// <summary>A row per project shown (AC 4).</summary>
    public IReadOnlyList<KanbanRowViewModel> Rows { get; private set; } = [];

    /// <summary>Every card, row by row.</summary>
    public IEnumerable<KanbanCardViewModel> Cards => Rows.SelectMany(row => row.Cards);

    /// <summary>Whether closed and archived projects are shown, from View › Show closed projects (AC 4).</summary>
    public bool ShowsClosed { get; private set; }

    /// <summary>Folds every row.</summary>
    public ICommand FoldAllCommand { get; }

    /// <summary>Unfolds every row.</summary>
    public ICommand UnfoldAllCommand { get; }

    /// <summary>
    /// What it draws, as text: the shell replaces it only when this changes (AC 21), so a refresh that changes nothing
    /// costs no layout.
    /// </summary>
    public string Signature { get; private set; } = string.Empty;

    /// <summary>
    /// Builds the kanban.
    /// </summary>
    /// <param name="sources">Every project, in the order of the tabs; put-away ones are left out unless shown.</param>
    /// <param name="settings">Where its layout is kept.</param>
    /// <param name="linksOf">A card's links from its board card, by card key; null gives none.</param>
    /// <param name="openProject">Opens a project's tab, by name.</param>
    /// <param name="openPiece">Opens a project's tab with a piece selected, by project and piece name.</param>
    public static KanbanViewModel Build(IReadOnlyList<KanbanProjectSource> sources, ISettingsStore settings,
        Func<string, KanbanCardLinks?> linksOf, Action<string> openProject, Action<string, string> openPiece)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(linksOf);
        ArgumentNullException.ThrowIfNull(openProject);
        ArgumentNullException.ThrowIfNull(openPiece);

        var stored = settings.Read();
        var view = new KanbanViewModel(settings, KanbanLayout.From(stored)) { ShowsClosed = stored.KanbanShowsClosed };
        var shown = sources.Where(source => source.PutAway is null || view.ShowsClosed)
            .OrderBy(source => source.PutAway is null ? 0 : 1).ToList();

        var cards = shown.ToDictionary(source => source, source => source.View is null
            ? []
            : source.View.AllNodes.Select(box => new KanbanCardViewModel(box, KanbanPlacement.ColumnOf(box.Piece), source.Title,
                linksOf(box.Piece.CardKey) ?? KanbanCardLinks.None, () => openPiece(source.Name, box.Name))).ToList());

        view.Columns = [.. KanbanColumns.All.Select(column => new KanbanColumnViewModel(column,
            cards.Values.Sum(list => list.Count(card => card.Column == column)), view._layout, view.ColumnChanged))];
        view.Rows = [.. shown.Select(source => new KanbanRowViewModel(source,
            [.. view.Columns.Select(column => new KanbanCellViewModel(column, cards[source].Where(card => card.Column == column.Column)))],
            view._layout.FoldedRows.Contains(source.Name), view.RowChanged, openProject))];
        view.Signature = string.Join("\n", view.Rows.Select(row => string.Join("|", row.Name, row.Title, row.WaitsText, row.CountsText,
            row.PutAwayText, row.ColourHex, string.Join(";", row.Cards.Select(card => card.Signature)))));

        return view;
    }

    /// <summary>Fits every open column to its widest card (Ctrl+Plus, AC 13), given each column's widest as measured.</summary>
    public void FitAll(Func<KanbanColumnViewModel, double> widestOf)
    {
        ArgumentNullException.ThrowIfNull(widestOf);

        foreach (var column in Columns.Where(column => column.IsOpen))
        {
            column.Fit(widestOf(column));
        }
    }

    /// <summary>Shows or hides closed and archived projects; the shell rebuilds the kanban after it.</summary>
    public void SetShowsClosed(bool shows)
    {
        ShowsClosed = shows;
        _settings.Write(_settings.Read() with { KanbanShowsClosed = shows });
        Raise(nameof(ShowsClosed));
    }

    private void SetFolds(bool folded)
    {
        foreach (var row in Rows)
        {
            row.IsFolded = folded;
        }
    }

    private void ColumnChanged(KanbanColumnViewModel column)
    {
        _layout = _layout.WithCollapsed(column.Column, column.IsCollapsed).WithWidth(column.Column, column.OpenWidth);
        Save();
    }

    private void RowChanged(KanbanRowViewModel row)
    {
        _layout = _layout.WithFolded(row.Name, row.IsFolded);
        Save();
    }

    private void Save() => _settings.Write(_layout.Into(_settings.Read()));
}
