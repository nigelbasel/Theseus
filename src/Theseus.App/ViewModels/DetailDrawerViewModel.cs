using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using Theseus.Core.Abstractions;
using Theseus.Core.Domain;
using Theseus.Core.Stopping;

namespace Theseus.App.ViewModels;

/// <summary>
/// The editor for one workstream. It is the plan file's editor as much as the card's: everything it
/// writes goes back to <c>sessions\&lt;key&gt;.json</c>, which the Claude Code sessions also write,
/// so edits made here and edits made by a session are the same kind of change.
/// </summary>
public sealed class DetailDrawerViewModel : ObservableObject
{
    private readonly IPlanStore _plans;
    private readonly IRunLog _runs;
    private readonly Action<WorkstreamCardViewModel> _saved;

    private WorkstreamCardViewModel? _card;
    private string _title = string.Empty;
    private string _workingDirectory = string.Empty;
    private string _prompt = string.Empty;
    private string _newDirectory = string.Empty;
    private string _statusDocumentPath = string.Empty;
    private string _newJiraKey = string.Empty;
    private int _boardOrder;
    private bool _isBlocked;
    private bool _reopenTomorrow;
    private bool _isOpen;

    /// <summary>Creates the drawer over the stores it reads and writes.</summary>
    public DetailDrawerViewModel(IPlanStore plans, IRunLog runs, Action<WorkstreamCardViewModel> saved)
    {
        _plans = plans;
        _runs = runs;
        _saved = saved;

        CloseCommand = new RelayCommand(_ => IsOpen = false);
        SaveCommand = new RelayCommand(_ => Save(), _ => _card is not null);
        AddDirectoryCommand = new RelayCommand(_ => AddDirectory(), _ => CanAddDirectory);
        RemoveDirectoryCommand = new RelayCommand(RemoveDirectory, parameter => parameter is string);
        OpenWorkingDirectoryCommand = new RelayCommand(_ => Reveal(WorkingDirectory), _ => Directory.Exists(WorkingDirectory));
        OpenStatusDocumentCommand = new RelayCommand(_ => Reveal(HandoffDestination), _ => File.Exists(HandoffDestination));
        AddJiraKeyCommand = new RelayCommand(_ => AddJiraKey(), _ => CanAddJiraKey);
        RemoveJiraKeyCommand = new RelayCommand(RemoveJiraKey, parameter => parameter is string);
        RescanPromptCommand = new RelayCommand(_ => RescanPrompt(), _ => UnclaimedKeys.Count > 0);
    }

    /// <summary>Hides the drawer, discarding unsaved edits.</summary>
    public RelayCommand CloseCommand { get; }

    /// <summary>Writes the edits back to the plan file.</summary>
    public RelayCommand SaveCommand { get; }

    /// <summary>Adds the typed directory to the session's scope.</summary>
    public RelayCommand AddDirectoryCommand { get; }

    /// <summary>Removes one directory from the session's scope.</summary>
    public RelayCommand RemoveDirectoryCommand { get; }

    /// <summary>Opens the working directory in Explorer.</summary>
    public RelayCommand OpenWorkingDirectoryCommand { get; }

    /// <summary>Opens the handover document.</summary>
    public RelayCommand OpenStatusDocumentCommand { get; }

    /// <summary>Adds the typed Jira key to the workstream.</summary>
    public RelayCommand AddJiraKeyCommand { get; }

    /// <summary>Removes one Jira key from the workstream.</summary>
    public RelayCommand RemoveJiraKeyCommand { get; }

    /// <summary>Adds every key the prompt mentions that the workstream does not already have.</summary>
    public RelayCommand RescanPromptCommand { get; }

    /// <summary>The extra directories added to each session's scope.</summary>
    public ObservableCollection<string> AddDirectories { get; } = [];

    /// <summary>This workstream's past launches, newest first.</summary>
    public ObservableCollection<RunHistoryEntryViewModel> Runs { get; } = [];

    /// <summary>Whether the drawer is showing.</summary>
    public bool IsOpen
    {
        get => _isOpen;
        set => Set(ref _isOpen, value);
    }

    /// <summary>The workstream's key, shown so the plan file it edits is never in doubt.</summary>
    public string Key => _card?.Key ?? string.Empty;

    /// <summary>Where the edits are written.</summary>
    public string PlanPath => _card is null ? string.Empty : _plans.PathFor(_card.Key);

    /// <summary>The workstream's colour, which is permanent and applies from the next launch.</summary>
    public string ColorHex => _card?.ColorHex ?? "#000000";

    /// <summary>
    /// Every Jira issue this workstream touches. Read from the prompt to begin with, and editable
    /// afterwards: prompts name requirement numbers and build identifiers that read like keys, and
    /// issues move between projects long after the prompt that named them was written.
    /// </summary>
    public ObservableCollection<string> JiraKeys { get; } = [];

    /// <summary>A key being typed, before it is added.</summary>
    public string NewJiraKey
    {
        get => _newJiraKey;
        set
        {
            if (Set(ref _newJiraKey, value))
            {
                Raise(nameof(CanAddJiraKey));
            }
        }
    }

    /// <summary>Whether what has been typed is a key this workstream does not already have.</summary>
    public bool CanAddJiraKey =>
        JiraKeyExtractor.Normalise(_newJiraKey) is { } key && !JiraKeys.Contains(key, StringComparer.Ordinal);

    /// <summary>The keys the prompt mentions that the workstream is not already carrying.</summary>
    public IReadOnlyList<string> UnclaimedKeys =>
        [.. JiraKeyExtractor.Extract(Prompt).Where(key => !JiraKeys.Contains(key, StringComparer.Ordinal))];

    /// <summary>
    /// The handover document, when the usual one is not the right one. Empty means Theseus looks for
    /// the notes the sessions have been keeping, which is right often enough that this is an override
    /// rather than something to fill in.
    /// </summary>
    public string StatusDocumentPath
    {
        get => _statusDocumentPath;
        set
        {
            if (Set(ref _statusDocumentPath, value))
            {
                Raise(nameof(HandoffDestination));
            }
        }
    }

    /// <summary>The file a stop request will actually name, whether chosen or found.</summary>
    public string HandoffDestination => _card is null
        ? string.Empty
        : HandoffPath.For(_card.Workstream with { StatusDocumentPath = Blank(_statusDocumentPath) });

    /// <summary>The tab title and card heading.</summary>
    public string Title
    {
        get => _title;
        set => Set(ref _title, value);
    }

    /// <summary>Where sessions start.</summary>
    public string WorkingDirectory
    {
        get => _workingDirectory;
        set
        {
            if (Set(ref _workingDirectory, value))
            {
                Raise(nameof(WorkingDirectoryExists));
            }
        }
    }

    /// <summary>Whether the working directory is still there.</summary>
    public bool WorkingDirectoryExists => Directory.Exists(_workingDirectory);

    /// <summary>The opening prompt.</summary>
    public string Prompt
    {
        get => _prompt;
        set
        {
            if (Set(ref _prompt, value))
            {
                Raise(nameof(UnclaimedKeys));
            }
        }
    }

    /// <summary>A directory being typed, before it is added.</summary>
    public string NewDirectory
    {
        get => _newDirectory;
        set
        {
            if (Set(ref _newDirectory, value))
            {
                Raise(nameof(CanAddDirectory));
            }
        }
    }

    /// <summary>Whether the typed directory can be added.</summary>
    public bool CanAddDirectory =>
        !string.IsNullOrWhiteSpace(_newDirectory)
        && !AddDirectories.Contains(_newDirectory.Trim(), StringComparer.OrdinalIgnoreCase);

    /// <summary>Position within the lane, and the order the morning's tabs open in.</summary>
    public int BoardOrder
    {
        get => _boardOrder;
        set => Set(ref _boardOrder, value);
    }

    /// <summary>Whether the work is blocked. A flag on the card, deliberately not a lane.</summary>
    public bool IsBlocked
    {
        get => _isBlocked;
        set => Set(ref _isBlocked, value);
    }

    /// <summary>Whether a whole-board Start should include this workstream.</summary>
    public bool ReopenTomorrow
    {
        get => _reopenTomorrow;
        set => Set(ref _reopenTomorrow, value);
    }

    /// <summary>Shows a workstream, loading its plan and its run history.</summary>
    public void Show(WorkstreamCardViewModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        _card = card;
        var workstream = card.Workstream;

        Title = workstream.Title;
        WorkingDirectory = workstream.WorkingDirectory;
        Prompt = workstream.Prompt;
        StatusDocumentPath = workstream.StatusDocumentPath ?? string.Empty;
        BoardOrder = workstream.BoardOrder;
        IsBlocked = workstream.IsBlocked;
        ReopenTomorrow = workstream.ReopenTomorrow;
        NewDirectory = string.Empty;

        AddDirectories.Clear();
        foreach (var directory in workstream.AddDirectories)
        {
            AddDirectories.Add(directory);
        }

        JiraKeys.Clear();
        foreach (var key in workstream.JiraKeys)
        {
            JiraKeys.Add(key);
        }

        NewJiraKey = string.Empty;

        Runs.Clear();
        foreach (var run in _runs.Read()
                     .Where(run => string.Equals(run.WorkstreamKey, card.Key, StringComparison.OrdinalIgnoreCase))
                     .OrderByDescending(run => run.StartedUtc))
        {
            Runs.Add(new RunHistoryEntryViewModel(run));
        }

        Raise(nameof(Key));
        Raise(nameof(PlanPath));
        Raise(nameof(ColorHex));
        Raise(nameof(UnclaimedKeys));
        Raise(nameof(HandoffDestination));

        IsOpen = true;
    }

    private void AddJiraKey()
    {
        JiraKeys.Add(JiraKeyExtractor.Normalise(NewJiraKey)!);
        NewJiraKey = string.Empty;
        Raise(nameof(UnclaimedKeys));
    }

    private void RemoveJiraKey(object? parameter)
    {
        if (parameter is string key)
        {
            JiraKeys.Remove(key);
            Raise(nameof(CanAddJiraKey));
            Raise(nameof(UnclaimedKeys));
        }
    }

    private void RescanPrompt()
    {
        foreach (var key in UnclaimedKeys)
        {
            JiraKeys.Add(key);
        }

        Raise(nameof(UnclaimedKeys));
    }

    private void AddDirectory()
    {
        AddDirectories.Add(NewDirectory.Trim());
        NewDirectory = string.Empty;
    }

    private void RemoveDirectory(object? parameter)
    {
        if (parameter is string directory)
        {
            AddDirectories.Remove(directory);
            Raise(nameof(CanAddDirectory));
        }
    }

    private void Save()
    {
        if (_card is null)
        {
            return;
        }

        var key = _card.Key;

        var plan = _plans.TryRead(key)
                   ?? new SessionPlan(BoardOrder, Title, ColorHex, WorkingDirectory, [],
                       _card.Workstream.LastSessionId, DateTimeOffset.Now.ToString("s"), Prompt);

        _plans.Write(key, plan with
        {
            Order = BoardOrder,
            Title = Title,
            Cwd = WorkingDirectory,
            AddDirs = [.. AddDirectories],
            Prompt = Prompt,
            WrittenAt = DateTimeOffset.Now.ToString("s"),
        });

        _card.Workstream = _card.Workstream with
        {
            IsBlocked = IsBlocked,
            ReopenTomorrow = ReopenTomorrow,
            StatusDocumentPath = Blank(StatusDocumentPath),
            JiraKeys = [.. JiraKeys],
        };

        IsOpen = false;
        _saved(_card);
    }

    private static string? Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void Reveal(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
}
