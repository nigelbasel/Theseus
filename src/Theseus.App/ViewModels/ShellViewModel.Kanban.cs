using System.ComponentModel;
using System.Windows.Input;
using Theseus.Core.Colour;
using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// The projects kanban in the All projects tab (projects-kanban spec AC 1 to 4, 21). Each refresh keeps what it read of
/// every project; the kanban is built from that only while the tab is open, since it builds a project view per project,
/// and is replaced only when what it draws changed.
/// </summary>
public sealed partial class ShellViewModel
{
    private IReadOnlyList<KanbanProjectRead> _kanbanReads = [];
    private bool _kanbanWatched;
    private RelayCommand? _kanbanShowsClosed;
    private RelayCommand? _kanbanFoldAll;
    private RelayCommand? _kanbanUnfoldAll;

    /// <summary>View › Show closed projects: closed and archived projects get rows of their own on All projects.</summary>
    public RelayCommand KanbanShowsClosedCommand => _kanbanShowsClosed ??= new RelayCommand(_ =>
        {
            if (AllProjectsView is KanbanViewModel kanban)
            {
                kanban.SetShowsClosed(!kanban.ShowsClosed);
                Raise(nameof(KanbanShowsClosed));
                BuildKanban(force: true);
            }
        }, _ => IsAllProjectsOpen);

    /// <summary>Whether All projects shows closed and archived projects, for the menu item's tick.</summary>
    public bool KanbanShowsClosed => _settings.Read().KanbanShowsClosed;

    /// <summary>View › Fold all rows, on All projects.</summary>
    public RelayCommand KanbanFoldAllCommand => _kanbanFoldAll ??= new RelayCommand(
        _ => (AllProjectsView as KanbanViewModel)?.FoldAllCommand.Execute(null), _ => IsAllProjectsOpen);

    /// <summary>View › Unfold all rows, on All projects.</summary>
    public RelayCommand KanbanUnfoldAllCommand => _kanbanUnfoldAll ??= new RelayCommand(
        _ => (AllProjectsView as KanbanViewModel)?.UnfoldAllCommand.Execute(null), _ => IsAllProjectsOpen);

    /// <summary>Keeps this refresh's reading of every project, and redraws the kanban if its tab is open.</summary>
    private void RefreshKanban(IReadOnlyList<KanbanProjectRead> reads)
    {
        _kanbanReads = reads;

        if (!_kanbanWatched)
        {
            _kanbanWatched = true;
            PropertyChanged += KanbanTabChanged;
        }

        BuildKanban(force: false);
    }

    private void KanbanTabChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IsAllProjectsOpen) && _isAllProjectsOpen)
        {
            BuildKanban(force: false);
        }
    }

    // While the tab is closed an empty kanban stands in, so the tab is there to open.
    private void BuildKanban(bool force)
    {
        var sources = _isAllProjectsOpen ? KanbanSources() : [];
        var kanban = KanbanViewModel.Build(sources, _settings,
            key => Board.Card(key) is { } card ? KanbanCardLinks.Of(card) : null,
            OpenProject,
            (project, piece) =>
            {
                OpenProject(project);
                ProjectView?.GoToPiece(piece);
            });

        if (force || AllProjectsView is not KanbanViewModel shown || shown.Signature != kanban.Signature)
        {
            AllProjectsView = kanban;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private List<KanbanProjectSource> KanbanSources()
    {
        var settings = _settings.Read();
        var colours = ProjectColoursFor([.. _kanbanReads.Select(read => read.Reading.Name)]);

        return [.. _kanbanReads.Select(read =>
        {
            var name = read.Reading.Name;
            var colour = ProjectColours.Of(colours, name);

            if (read.Pieces is not { } pieces || read.Overview is not { } overview)
            {
                return new KanbanProjectSource(name, name, null) { ColourHex = colour, Problem = ProblemOf(read.Reading) };
            }

            var putAway = ProjectClosures.Of(settings, name)?.Word.ToLowerInvariant();
            // A put-away project the kanban does not show needs no view built.
            var view = putAway is null || settings.KanbanShowsClosed ? BuildView(read.Reading, pieces, [], overview, read.Lead, null) : null;

            return new KanbanProjectSource(name, read.Reading.File!.Title, view)
            {
                ColourHex = colour,
                WaitingCount = overview.Waits.Count,
                PutAway = putAway,
            };
        })];
    }
}
