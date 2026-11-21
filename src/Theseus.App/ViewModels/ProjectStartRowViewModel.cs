using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// One card's tick box in a project's Start confirmation (1.1d AC 43): ticked when it can start, unticked when paused,
/// greyed with its reason when it cannot start from here.
/// </summary>
public sealed class ProjectStartRowViewModel : ObservableObject
{
    private bool _isTicked;

    /// <summary>Creates the row for one card.</summary>
    public ProjectStartRowViewModel(ProjectStartEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        Entry = entry;
        _isTicked = entry.IsTicked;
    }

    /// <summary>The card as the plan has it.</summary>
    public ProjectStartEntry Entry { get; }

    /// <summary>What the tick box says: the card's name, and its reason when it has one.</summary>
    public string Line => Entry.Line;

    /// <summary>Whether the box can be ticked; a greyed card is never started.</summary>
    public bool CanStart => Entry.CanStart;

    /// <summary>Whether the card starts. A greyed card stays unticked whatever is asked.</summary>
    public bool IsTicked
    {
        get => _isTicked;
        set => Set(ref _isTicked, value && CanStart);
    }
}
