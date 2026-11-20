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

    /// <summary>Creates a project card.</summary>
    /// <param name="name">The project's name: its project home folder's name.</param>
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
                RaiseCount();
            }
        }
    }

    /// <summary>How many things wait on the owner.</summary>
    public int WaitingCount => _waiting.Count;

    /// <summary>Whether anything waits on him.</summary>
    public bool HasWaiting => _waiting.Count > 0;

    /// <summary>Whether the count's badge shows: something waits.</summary>
    public bool HasBadge => HasWaiting;

    /// <summary>The badge: the count.</summary>
    public string CountText => WaitingCount.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The waiting list, for the count's tooltip.</summary>
    public string WaitingText => _waiting.Count == 0 ? "Nothing is waiting on you" : string.Join(Environment.NewLine, _waiting);

    private void RaiseCount()
    {
        Raise(nameof(WaitingCount));
        Raise(nameof(WaitingText));
        Raise(nameof(HasWaiting));
        Raise(nameof(HasBadge));
        Raise(nameof(CountText));
    }

    /// <summary>The card's tooltip: its title.</summary>
    public string ToolTipText => Title;

    /// <summary>Opens the project view (AC 14).</summary>
    public ICommand? OpenCommand { get; set; }
}
