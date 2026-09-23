using System.Collections.ObjectModel;
using System.IO;
using Theseus.Core.Colour;
using Theseus.Core.Abstractions;
using Theseus.Core.Domain;

namespace Theseus.App.ViewModels;

/// <summary>Creates a workstream from scratch and optionally starts it straight away.</summary>
public sealed class NewWorkstreamViewModel : ObservableObject
{
    private readonly IPlanStore _plans;
    private readonly ITabColourAllocator _colours;
    private readonly Func<IReadOnlyList<string>> _stages;
    private readonly Func<IReadOnlyList<string>> _areas;
    private readonly Action<NewWorkstream> _created;

    private string _title = string.Empty;
    private string _workingDirectory = string.Empty;
    private string _prompt = string.Empty;
    private string _colourHex = string.Empty;
    private string _stage = string.Empty;
    private string _area = string.Empty;
    private bool _startNow = true;
    private bool _isOpen;

    /// <summary>Creates the composer.</summary>
    /// <param name="plans">Where the new plan file is written.</param>
    /// <param name="colours">Chooses the workstream's permanent colour.</param>
    /// <param name="stages">The board's columns, offered as the cell to create it in.</param>
    /// <param name="areas">The board's rows, likewise.</param>
    /// <param name="created">Called with the new workstream, where it goes, and whether to start it.</param>
    public NewWorkstreamViewModel(
        IPlanStore plans,
        ITabColourAllocator colours,
        Func<IReadOnlyList<string>> stages,
        Func<IReadOnlyList<string>> areas,
        Action<NewWorkstream> created)
    {
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
                Raise(nameof(CanCreate));
            }
        }
    }

    /// <summary>The key this title will produce, shown so the plan file is never a surprise.</summary>
    public string Key => WorkstreamKey.Unique(_title, _plans.Keys());

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
        _created(new NewWorkstream(key, Stage, Area, StartNow));
    }

    private IReadOnlyList<string> ColoursInUse() =>
        [.. _plans.Keys()
            .Select(key => _plans.TryRead(key)?.Color)
            .Where(colour => !string.IsNullOrWhiteSpace(colour))
            .Select(colour => colour!)];
}
