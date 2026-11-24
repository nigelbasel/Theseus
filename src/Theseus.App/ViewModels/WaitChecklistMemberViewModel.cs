using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>One criterion under a wait in the waits checklist: ticked, it is among what the command bar acts on.</summary>
public sealed class WaitChecklistMemberViewModel : ObservableObject
{
    private readonly Action _changed;
    private bool _isTicked;

    /// <summary>Creates the row, unticked.</summary>
    public WaitChecklistMemberViewModel(WaitCriterion criterion, bool isTicked, Action changed)
    {
        Criterion = criterion;
        _isTicked = isTicked;
        _changed = changed;
    }

    /// <summary>The criterion.</summary>
    public WaitCriterion Criterion { get; }

    /// <summary>"1.1 · For a seeded leaver, the assistant states…".</summary>
    public string Label => $"{Criterion.Objective} · {Criterion.Text}";

    /// <summary>Whether it is ticked.</summary>
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

    /// <summary>Sets the tick without telling its group, for the group's own tick.</summary>
    internal void SetQuietly(bool ticked)
    {
        if (_isTicked != ticked)
        {
            _isTicked = ticked;
            Raise(nameof(IsTicked));
        }
    }
}
