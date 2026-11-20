using System.Windows.Input;

namespace Theseus.App.ViewModels;

/// <summary>
/// A project on the board, standing for its cards (project-view AC 12 and 13): its name, current phase, objectives done,
/// and how many things wait on the owner, listed on hover.
/// </summary>
public sealed class ProjectCardViewModel : ObservableObject
{
    private string _phaseText = string.Empty;
    private string _objectivesText = string.Empty;
    private IReadOnlyList<string> _waiting = [];
    private string? _lastFailure;

    /// <summary>Creates a project card.</summary>
    /// <param name="name">The project's name: its file name without <c>.md</c>.</param>
    /// <param name="title">Its title.</param>
    public ProjectCardViewModel(string name, string title)
    {
        Name = name;
        Title = title;
    }

    /// <summary>The project's name, which keys its place on the board.</summary>
    public string Name { get; }

    /// <summary>Its title.</summary>
    public string Title { get; }

    /// <summary>Its current phase, such as "Phase 1: Evidence", or that every objective is done.</summary>
    public string PhaseText
    {
        get => _phaseText;
        set => Set(ref _phaseText, value);
    }

    /// <summary>"n of m objectives done" (departure 3).</summary>
    public string ObjectivesText
    {
        get => _objectivesText;
        set => Set(ref _objectivesText, value);
    }

    /// <summary>What waits on the owner, one line each: sessions waiting for input, reports, proposals, specs.</summary>
    public IReadOnlyList<string> Waiting
    {
        get => _waiting;
        set
        {
            if (Set(ref _waiting, value))
            {
                Raise(nameof(WaitingCount));
                Raise(nameof(WaitingText));
                Raise(nameof(HasWaiting));
            }
        }
    }

    /// <summary>How many things wait on the owner.</summary>
    public int WaitingCount => _waiting.Count;

    /// <summary>Whether anything waits on him.</summary>
    public bool HasWaiting => _waiting.Count > 0;

    /// <summary>The waiting list, for the count's tooltip.</summary>
    public string WaitingText => _waiting.Count == 0 ? "Nothing is waiting on you" : string.Join(Environment.NewLine, _waiting);

    /// <summary>
    /// Why the last read failed while the reading before it is shown (PR 2 review, item 9), or null when it is current.
    /// </summary>
    public string? LastFailure
    {
        get => _lastFailure;
        set
        {
            if (Set(ref _lastFailure, value))
            {
                Raise(nameof(ToolTipText));
            }
        }
    }

    /// <summary>The card's tooltip: its title, and a failed last read when there was one.</summary>
    public string ToolTipText => _lastFailure is null
        ? Title
        : $"{Title}{Environment.NewLine}The last read failed: {_lastFailure}. Showing the reading before it; a second failure in a row makes it unreadable.";

    /// <summary>Opens the project view (AC 14).</summary>
    public ICommand? OpenCommand { get; set; }
}
