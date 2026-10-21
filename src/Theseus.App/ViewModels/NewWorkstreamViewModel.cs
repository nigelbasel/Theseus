using System.Collections.ObjectModel;
using System.IO;
using Theseus.Core.Colour;
using Theseus.Core.Abstractions;
using Theseus.Core.Domain;
using Theseus.Core.Links;

namespace Theseus.App.ViewModels;

/// <summary>Creates a workstream from scratch and optionally starts it straight away.</summary>
public sealed class NewWorkstreamViewModel : ObservableObject
{
    private readonly IPlanStore _plans;
    private readonly ITabColourAllocator _colours;
    private readonly Func<IReadOnlyList<string>> _stages;
    private readonly Func<IReadOnlyList<string>> _areas;
    private readonly Action<NewWorkstream> _created;
    private readonly Func<IReadOnlyCollection<string>> _takenKeys;
    private readonly Func<string> _briefFolderName;

    private string _title = string.Empty;
    private string _workingDirectory = string.Empty;
    private string _prompt = string.Empty;
    private string _colourHex = string.Empty;
    private string _stage = string.Empty;
    private string _area = string.Empty;
    private bool _startNow = true;
    private bool _isOpen;
    private Workstream? _parent;
    private bool _reportBack;

    /// <summary>Creates the composer.</summary>
    /// <param name="plans">Where the new plan file is written.</param>
    /// <param name="colours">Chooses the workstream's permanent colour.</param>
    /// <param name="stages">The board's columns, offered as the cell to create it in.</param>
    /// <param name="areas">The board's rows, likewise.</param>
    /// <param name="created">Called with the new workstream, where it goes, and whether to start it.</param>
    /// <param name="takenKeys">
    /// Keys no new card may have although no plan file holds them: those of deleted cards that links
    /// still name, so a new card with an old title inherits none of the old card's links.
    /// </param>
    /// <param name="briefFolderName">The board's brief folder, relative to the working directory.</param>
    public NewWorkstreamViewModel(
        IPlanStore plans,
        ITabColourAllocator colours,
        Func<IReadOnlyList<string>> stages,
        Func<IReadOnlyList<string>> areas,
        Action<NewWorkstream> created,
        Func<IReadOnlyCollection<string>>? takenKeys = null,
        Func<string>? briefFolderName = null)
    {
        _takenKeys = takenKeys ?? (() => []);
        _briefFolderName = briefFolderName ?? (() => BriefFolder.DefaultName);
        _plans = plans;
        _colours = colours;
        _stages = stages;
        _areas = areas;
        _created = created;

        OpenCommand = new RelayCommand(_ => Open());
        CloseCommand = new RelayCommand(_ => IsOpen = false);
        CreateCommand = new RelayCommand(_ => Create(), _ => CanCreate);
    }

    /// <summary>Shows the composer with a fresh, empty workstream.</summary>
    public RelayCommand OpenCommand { get; }

    /// <summary>Hides the composer without creating anything.</summary>
    public RelayCommand CloseCommand { get; }

    /// <summary>Writes the plan file and closes.</summary>
    public RelayCommand CreateCommand { get; }

    /// <summary>Whether the composer is showing.</summary>
    public bool IsOpen
    {
        get => _isOpen;
        set => Set(ref _isOpen, value);
    }

    /// <summary>The card heading and the tab title.</summary>
    public string Title
    {
        get => _title;
        set
        {
            if (Set(ref _title, value))
            {
                Raise(nameof(Key));
                Raise(nameof(ReportPath));
                Raise(nameof(CanCreate));
            }
        }
    }

    /// <summary>The key this title will produce, shown so the plan file is never a surprise.</summary>
    public string Key => WorkstreamKey.Unique(_title, [.. _plans.Keys(), .. _takenKeys()]);

    /// <summary>Whether this is a follow-on spawned from another card.</summary>
    public bool IsFollowOn => _parent is not null;

    /// <summary>The card this follow-on is spawned from, as the dialog names it.</summary>
    public string ParentTitle => _parent?.Title ?? string.Empty;

    /// <summary>Whether the parent waits on a report from the follow-on.</summary>
    public bool ReportBack
    {
        get => _reportBack;
        set => Set(ref _reportBack, value);
    }

    /// <summary>Where the follow-on writes its report, under its parent's working directory.</summary>
    public string ReportPath => _parent is null
        ? string.Empty
        : BriefFolder.ReportPath(_parent.WorkingDirectory, _briefFolderName(), Key);

    /// <summary>
    /// Shows the composer for a follow-on to <paramref name="parent"/>: same working directory, so it
    /// keeps the same memory, and the same row. Creating it records that the parent spawned it.
    /// </summary>
    public void OpenFollowOn(Workstream parent)
    {
        ArgumentNullException.ThrowIfNull(parent);

        Open();
        _parent = parent;
        ReportBack = false;
        WorkingDirectory = parent.WorkingDirectory;

        if (parent.Area is { } area && Areas.Contains(area))
        {
            Area = area;
        }

        Raise(nameof(IsFollowOn));
        Raise(nameof(ParentTitle));
        Raise(nameof(ReportPath));
    }

    /// <summary>Where the session will start.</summary>
    public string WorkingDirectory
    {
        get => _workingDirectory;
        set
        {
            if (Set(ref _workingDirectory, value))
            {
                Raise(nameof(WorkingDirectoryExists));
                Raise(nameof(CanCreate));
            }
        }
    }

    /// <summary>Whether the working directory is there.</summary>
    public bool WorkingDirectoryExists => Directory.Exists(_workingDirectory);

    /// <summary>The prompt the session opens with.</summary>
    public string Prompt
    {
        get => _prompt;
        set
        {
            if (Set(ref _prompt, value))
            {
                Raise(nameof(CanCreate));
            }
        }
    }

    /// <summary>The colour the allocator chose, shown before it becomes permanent.</summary>
    public string ColourHex
    {
        get => _colourHex;
        set => Set(ref _colourHex, value);
    }

    /// <summary>The board's columns, to choose between.</summary>
    public ObservableCollection<string> Stages { get; } = [];

    /// <summary>The board's rows, to choose between.</summary>
    public ObservableCollection<string> Areas { get; } = [];

    /// <summary>
    /// Which column the card goes in. Until 2026-09-23 there was no choice and every new workstream
    /// landed in the first cell, wherever the person making it meant it to go.
    /// </summary>
    public string Stage
    {
        get => _stage;
        set => Set(ref _stage, value);
    }

    /// <summary>Which row the card goes in.</summary>
    public string Area
    {
        get => _area;
        set => Set(ref _area, value);
    }

    /// <summary>Whether the board has rows worth choosing between. One row is not a choice.</summary>
    public bool HasAreas => Areas.Count > 1;

    /// <summary>Whether to open a tab as soon as the workstream exists.</summary>
    public bool StartNow
    {
        get => _startNow;
        set => Set(ref _startNow, value);
    }

    /// <summary>Whether there is enough here to write a plan a launch can use.</summary>
    public bool CanCreate =>
        !string.IsNullOrWhiteSpace(_title)
        && !string.IsNullOrWhiteSpace(_prompt)
        && WorkingDirectoryExists;

    private void Open()
    {
        _parent = null;
        Raise(nameof(IsFollowOn));
        Raise(nameof(ParentTitle));
        Title = string.Empty;
        Prompt = string.Empty;
        WorkingDirectory = SuggestedWorkingDirectory();
        StartNow = true;
        ColourHex = _colours.Allocate(new ColourAllocationState(ColoursInUse(), []));

        Fill(Stages, _stages());
        Fill(Areas, _areas());
        Stage = Stages.FirstOrDefault() ?? string.Empty;
        Area = Areas.FirstOrDefault() ?? string.Empty;
        Raise(nameof(HasAreas));

        IsOpen = true;
    }

    private static void Fill(ObservableCollection<string> collection, IReadOnlyList<string> values)
    {
        collection.Clear();

        foreach (var value in values)
        {
            collection.Add(value);
        }
    }

    private string SuggestedWorkingDirectory()
    {
        var existing = _plans.Keys()
            .Select(key => _plans.TryRead(key)?.Cwd)
            .Where(cwd => !string.IsNullOrWhiteSpace(cwd))
            .GroupBy(cwd => cwd!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        return existing?.Key ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    private void Create()
    {
        var key = Key;
        var plan = new SessionPlan(_plans.Keys().Count + 1, Title.Trim(), ColourHex,
            WorkingDirectory.Trim(), [], null, DateTimeOffset.Now.ToString("s"), Prompt.Trim());

        _plans.Write(key, plan);

        IsOpen = false;
        _created(new NewWorkstream(key, Stage, Area, StartNow, _parent?.Key,
            _parent is not null && ReportBack ? ReportPath : null));
    }

    private IReadOnlyList<string> ColoursInUse() =>
        [.. _plans.Keys()
            .Select(key => _plans.TryRead(key)?.Color)
            .Where(colour => !string.IsNullOrWhiteSpace(colour))
            .Select(colour => colour!)];
}
