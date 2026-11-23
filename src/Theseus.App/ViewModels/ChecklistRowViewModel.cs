using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>One ticked row of a checklist confirmation.</summary>
public sealed class ChecklistRowViewModel : ObservableObject
{
    private readonly Action _changed;
    private bool _isTicked = true;

    /// <summary>Creates the row, ticked.</summary>
    /// <param name="row">The item.</param>
    /// <param name="changed">Told when its tick changes, so the foot is recounted.</param>
    public ChecklistRowViewModel(ChecklistRow row, Action changed)
    {
        Row = row;
        _changed = changed;
    }

    /// <summary>The item.</summary>
    public ChecklistRow Row { get; }

    /// <summary>Its name.</summary>
    public string Label => Row.Label;

    /// <summary>What it is, and its state.</summary>
    public string Detail => Row.Detail;

    /// <summary>Why it is listed.</summary>
    public string Note => Row.Note;

    /// <summary>Whether the press acts on it.</summary>
    public bool IsTicked
    {
        get => _isTicked;
        set
        {
            if (Set(ref _isTicked, value))
            {
                _changed();
            }
        }
    }
}
