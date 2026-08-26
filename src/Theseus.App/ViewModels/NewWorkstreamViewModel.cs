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
    private readonly Action<string, bool> _created;

    private string _title = string.Empty;
    private string _workingDirectory = string.Empty;
    private string _prompt = string.Empty;
    private string _colourHex = string.Empty;
    private bool _startNow = true;
    private bool _isOpen;

    /// <summary>Creates the composer.</summary>
    /// <param name="plans">Where the new plan file is written.</param>
    /// <param name="colours">Chooses the workstream's permanent colour.</param>
    /// <param name="created">Called with the new key and whether it should be started immediately.</param>
    public NewWorkstreamViewModel(IPlanStore plans, ITabColourAllocator colours, Action<string, bool> created)
    {
        _plans = plans;
        _colours = colours;
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
        IsOpen = true;
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
        _created(key, StartNow);
    }

    private IReadOnlyList<string> ColoursInUse() =>
        [.. _plans.Keys()
            .Select(key => _plans.TryRead(key)?.Color)
            .Where(colour => !string.IsNullOrWhiteSpace(colour))
            .Select(colour => colour!)];
}
