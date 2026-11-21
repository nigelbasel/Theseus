using System.Windows.Input;
using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// A project on the board, standing for its cards (project-view AC 12 and 13; 1.1d AC 38 to 44): unmistakably a project
/// by its stripe, mark and the word PROJECT in its own colour; where it is, in the project view's sentence and counts;
/// selected, dragged, started and stopped as a card is.
/// </summary>
public sealed class ProjectCardViewModel : ObservableObject, IBoardCard
{
    /// <summary>The colour of a project not given one yet: the To do grey.</summary>
    public const string NoColour = "#8A9490";

    private string _phaseText = string.Empty;
    private string _objectivesText = string.Empty;
    private IReadOnlyList<string> _waiting = [];
    private bool _isDimmed;
    private string? _warning;
    private string _colourHex = NoColour;
    private string _sentence = string.Empty;
    private IReadOnlyList<StateCountViewModel> _counts = [];
    private bool _isSelected;
    private bool _isLive;
    private ProjectStart? _startPlan;
    private ProjectStop? _stopPlan;

    /// <summary>Creates a project card.</summary>
    /// <param name="name">The project's name: its project home folder's name.</param>
    /// <param name="title">Its title.</param>
    public ProjectCardViewModel(string name, string title)
    {
        Name = name;
        Title = title;
    }

    /// <summary>The project's name, which keys its place on the board, its colour and its view.</summary>
    public string Name { get; }

    /// <summary>Its title.</summary>
    public string Title { get; }

    /// <summary>
    /// Whether its project.md does not read (pinned to an older kit, or out of shape): the card is dimmed, says why, and
    /// opens the file instead of the project view (1.1c AC 5). It keeps its mark and colour, and has no ▶ or ■ (1.1d AC 44).
    /// </summary>
    public bool IsDimmed
    {
        get => _isDimmed;
        set
        {
            if (Set(ref _isDimmed, value))
            {
                Raise(nameof(CardOpacity));
                Raise(nameof(OpenToolTip));
                Raise(nameof(ShowsRunButtons));
            }
        }
    }

    /// <summary>The card's opacity: dimmed when its project does not read.</summary>
    public double CardOpacity => _isDimmed ? 0.55 : 1.0;

    /// <summary>What Open does, for its menu item.</summary>
    public string OpenToolTip => _isDimmed
        ? "Open its project.md, to see why it does not read"
        : "Open the project view in the main area (also View › Open project)";

    /// <summary>Whether ▶ and ■ show: only for a project that reads (1.1d AC 44).</summary>
    public bool ShowsRunButtons => !_isDimmed;

    /// <summary>The project's own colour, as <c>#rrggbb</c> (1.1d AC 40): its stripe, its mark, its tab and its summary.</summary>
    public string ColourHex
    {
        get => _colourHex;
        set => Set(ref _colourHex, value);
    }

    /// <summary>Its current phase, such as "Phase 1: Evidence", or why it does not read.</summary>
    public string PhaseText
    {
        get => _phaseText;
        set => Set(ref _phaseText, value);
    }

    /// <summary>"n of m objectives done" (departure 3), or what to do with a dimmed card.</summary>
    public string ObjectivesText
    {
        get => _objectivesText;
        set => Set(ref _objectivesText, value);
    }

    /// <summary>Where the project is, the project view's sentence (1.1d AC 8, 39); empty for a dimmed card.</summary>
    public string Sentence
    {
        get => _sentence;
        set
        {
            if (Set(ref _sentence, value))
            {
                Raise(nameof(HasSentence));
            }
        }
    }

    /// <summary>Whether the sentence line shows.</summary>
    public bool HasSentence => _sentence.Length > 0;

    /// <summary>The project view's count chips with the zeros left out (1.1d AC 39).</summary>
    public IReadOnlyList<StateCountViewModel> Counts
    {
        get => _counts;
        set
        {
            if (!_counts.SequenceEqual(value))
            {
                _counts = value;
                Raise();
            }
        }
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

    /// <summary>
    /// What is wrong with what its file says that the owner should know, such as a card it names that another project
    /// names too (1.1c AC 2), or null.
    /// </summary>
    public string? Warning
    {
        get => _warning;
        set
        {
            if (Set(ref _warning, value))
            {
                Raise(nameof(ToolTipText));
            }
        }
    }

    /// <summary>The card's tooltip: its title, and its warning when it has one.</summary>
    public string ToolTipText => _warning is null ? Title : $"{Title}{Environment.NewLine}{_warning}";

    /// <summary>Whether it is selected (1.1d AC 41): it then has the card's 2 px accent border.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (Set(ref _isSelected, value))
            {
                SelectionChanged?.Invoke();
            }
        }
    }

    /// <summary>Raised when it is selected or unselected, so the selection commands look again.</summary>
    public Action? SelectionChanged { get; set; }

    /// <summary>Whether any of its cards has a session open, so Select live chooses it.</summary>
    public bool IsLive
    {
        get => _isLive;
        set => Set(ref _isLive, value);
    }

    /// <summary>A drag of it carries its name, marked as a project's (1.1d AC 42).</summary>
    public string DragPayload => Services.BoardGesture.ProjectPrefix + Name;

    /// <summary>What its Start would list, or null for a dimmed card (1.1d AC 43).</summary>
    public ProjectStart? StartPlan
    {
        get => _startPlan;
        set
        {
            if (Set(ref _startPlan, value))
            {
                Raise(nameof(StartToolTip));
            }
        }
    }

    /// <summary>What its Stop would ask, or null for a dimmed card.</summary>
    public ProjectStop? StopPlan
    {
        get => _stopPlan;
        set
        {
            if (Set(ref _stopPlan, value))
            {
                Raise(nameof(StopToolTip));
            }
        }
    }

    /// <summary>What ▶ does, or why it cannot.</summary>
    public string StartToolTip => _startPlan?.ToolTip ?? "A project that does not read cannot be started";

    /// <summary>What ■ does, or why it cannot.</summary>
    public string StopToolTip => _stopPlan?.ToolTip ?? "A project that does not read cannot be stopped";

    /// <summary>The full path of its project.md, for Project properties.</summary>
    public string ProjectFile { get; set; } = string.Empty;

    /// <summary>Open project.md: the project's file in its editor (1.1c AC 8).</summary>
    public ICommand? OpenFileCommand { get; set; }

    /// <summary>Add objective…, at the end of the current phase (1.1c AC 8); none for a dimmed card.</summary>
    public ICommand? AddObjectiveCommand { get; set; }

    /// <summary>Opens the project view (AC 14), or a dimmed card's project.md.</summary>
    public ICommand? OpenCommand { get; set; }

    /// <summary>▶: starts the project's cards, after a confirmation that lists them (1.1d AC 43).</summary>
    public ICommand? StartCommand { get; set; }

    /// <summary>■: asks each of its running sessions to write its handover and stop.</summary>
    public ICommand? StopCommand { get; set; }

    /// <summary>Project properties…: its title, colour and project.md (1.1d AC 40).</summary>
    public ICommand? PropertiesCommand { get; set; }
}
