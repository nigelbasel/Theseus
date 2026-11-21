using System.ComponentModel;
using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// A project's Start confirmation (1.1d AC 43; Nigel's answer to question 4, 2026-11-21): its cards with tick boxes, the
/// ones that can start ticked, paused ones unticked, running and blocked ones greyed with the reason. Start stays off
/// until something is ticked.
/// </summary>
public sealed class ProjectStartViewModel : ObservableObject
{
    /// <summary>Creates the confirmation of a project's Start.</summary>
    public ProjectStartViewModel(ProjectStart plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        Plan = plan;
        Rows = [.. plan.Entries.Select(entry => new ProjectStartRowViewModel(entry))];

        foreach (var row in Rows)
        {
            row.PropertyChanged += OnRowChanged;
        }
    }

    /// <summary>What it confirms.</summary>
    public ProjectStart Plan { get; }

    /// <summary>The window's title.</summary>
    public string Caption => $"Start {Plan.Title}";

    /// <summary>What it asks.</summary>
    public string Question =>
        $"Start the ticked cards of {Plan.Title} in its terminal window? A paused card starts only if you tick it; a card that is running, blocked, done or waiting on you is not started.";

    /// <summary>One row per card, in file order.</summary>
    public IReadOnlyList<ProjectStartRowViewModel> Rows { get; }

    /// <summary>The keys of the ticked cards.</summary>
    public IReadOnlyList<string> TickedKeys => [.. Rows.Where(row => row.IsTicked).Select(row => row.Entry.Key)];

    /// <summary>Whether anything is ticked, so Start can be pressed.</summary>
    public bool AnyTicked => Rows.Any(row => row.IsTicked);

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProjectStartRowViewModel.IsTicked))
        {
            Raise(nameof(AnyTicked));
            Raise(nameof(TickedKeys));
        }
    }
}
