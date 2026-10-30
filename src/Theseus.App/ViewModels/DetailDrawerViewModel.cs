using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using Theseus.Core.Abstractions;
using Theseus.Core.Domain;
using Theseus.Core.Launching;
using Theseus.Core.Sessions;
using Theseus.Core.Stopping;

namespace Theseus.App.ViewModels;

/// <summary>
/// The editor for one workstream. It is the plan file's editor as much as the card's: everything it
/// writes goes back to <c>sessions\&lt;key&gt;.json</c>, which the Claude Code sessions also write,
/// so edits made here and edits made by a session are the same kind of change.
///
/// It is a slim panel in tabs (Nigel, 2026-10-22). What is going on with the card - its title, Blocked,
/// Reopen tomorrow, Jira issues and links - applies the moment it changes, so the panel has no Save.
/// How its sessions start is edited in a dialog of its own, with Save and Cancel side by side. One
/// Save for everything, which also closed the drawer, once lost a half-made link without a word.
/// </summary>
public sealed class DetailDrawerViewModel : ObservableObject
{
    private readonly IPlanStore _plans;
    private readonly IRunLog _runs;
    private readonly IWorkflowRunReader _workflows;
    private readonly Action<WorkstreamCardViewModel> _saved;
    private readonly Action<WorkstreamCardViewModel, bool>? _archived;
    private readonly Action<WorkstreamCardViewModel>? _removed;
    private bool _isConfirmingDelete;

    private WorkstreamCardViewModel? _card;
    private string _title = string.Empty;
    private string _workingDirectory = string.Empty;
    private string _prompt = string.Empty;
    private string _everyStartPrompt = string.Empty;
    private string _newDirectory = string.Empty;
    private string _statusDocumentPath = string.Empty;
    private string _newJiraKey = string.Empty;
    private string _windowGroup = string.Empty;
    private int _boardOrder;
    private bool _isBlocked;
    private bool _isPaused;
    private bool _reopenTomorrow;
    private bool _isOpen;
    private bool _loading;
    private DrawerTab _selectedTab;
    private bool _isEditingSetup;

    /// <summary>Creates the drawer over the stores it reads and writes.</summary>
    /// <param name="archived">Puts the workstream away, or brings it back.</param>
    /// <param name="removed">Destroys the workstream.</param>
    public DetailDrawerViewModel(IPlanStore plans, IRunLog runs, IWorkflowRunReader workflows,
        Action<WorkstreamCardViewModel> saved,
        Action<WorkstreamCardViewModel, bool>? archived = null,
        Action<WorkstreamCardViewModel>? removed = null)
    {
        _plans = plans;
        _runs = runs;
        _workflows = workflows;
        _saved = saved;
        _archived = archived;
        _removed = removed;

        ArchiveCommand = new RelayCommand(_ => Archive(), _ => _card is not null && !IsLive);
        DeleteCommand = new RelayCommand(_ => IsConfirmingDelete = true, _ => _card is not null && !IsLive);
        CancelDeleteCommand = new RelayCommand(_ => IsConfirmingDelete = false);
        ConfirmDeleteCommand = new RelayCommand(_ => Delete(), _ => _card is not null && !IsLive);

        CloseCommand = new RelayCommand(_ => IsOpen = false);
        EditSetupCommand = new RelayCommand(_ => IsEditingSetup = true, _ => _card is not null);
        SaveCommand = new RelayCommand(_ =>
        {
            SaveSetup();
            IsEditingSetup = false;
        }, _ => _card is not null && IsSetupDirty);
        CancelSetupCommand = new RelayCommand(_ =>
        {
            if (_card is not null)
            {
                LoadSetup(_card.Workstream);
            }

            IsEditingSetup = false;
        });
        ShowLinksCommand = new RelayCommand(_ => SelectedTab = DrawerTab.Links);
        AddDirectoryCommand = new RelayCommand(_ => AddDirectory(), _ => CanAddDirectory);
        RemoveDirectoryCommand = new RelayCommand(RemoveDirectory, parameter => parameter is string);
        OpenWorkingDirectoryCommand = new RelayCommand(_ => Reveal(WorkingDirectory), _ => Directory.Exists(WorkingDirectory));
        OpenStatusDocumentCommand = new RelayCommand(_ => Reveal(HandoffDestination), _ => File.Exists(HandoffDestination));
        AddJiraKeyCommand = new RelayCommand(_ => AddJiraKey(), _ => CanAddJiraKey);
        RemoveJiraKeyCommand = new RelayCommand(RemoveJiraKey, parameter => parameter is string);
        RescanPromptCommand = new RelayCommand(_ => RescanPrompt(), _ => UnclaimedKeys.Count > 0);
    }

    /// <summary>Puts this workstream away, or brings it back when it is already away.</summary>
    public RelayCommand ArchiveCommand { get; }

    /// <summary>Asks whether this workstream should really be destroyed.</summary>
    public RelayCommand DeleteCommand { get; }

    /// <summary>Destroys it.</summary>
    public RelayCommand ConfirmDeleteCommand { get; }

    /// <summary>Thinks better of it.</summary>
    public RelayCommand CancelDeleteCommand { get; }

    /// <summary>Whether the delete confirmation is showing.</summary>
    public bool IsConfirmingDelete
    {
        get => _isConfirmingDelete;
        private set => Set(ref _isConfirmingDelete, value);
    }

    /// <summary>
    /// The terminal window this workstream's sessions start in, when the lane is not the right
    /// answer. Blank means the lane decides.
    /// </summary>
    public string WindowGroup
    {
        get => _windowGroup;
        set => SetSetup(ref _windowGroup, value);
    }

    /// <summary>The tab showing.</summary>
    public DrawerTab SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (Set(ref _selectedTab, value))
            {
                Raise(nameof(SelectedTabIndex));
            }
        }
    }

    /// <summary>The tab showing, as the tab control counts them.</summary>
    public int SelectedTabIndex
    {
        get => (int)_selectedTab;
        set => SelectedTab = (DrawerTab)value;
    }

    /// <summary>Whether the setup dialog holds edits that have not been saved.</summary>
    public bool IsSetupDirty =>
        _card is { Workstream: var saved }
        && (!string.Equals(_workingDirectory, saved.WorkingDirectory, StringComparison.Ordinal)
            || !AddDirectories.SequenceEqual(saved.AddDirectories, StringComparer.Ordinal)
            || !string.Equals(_prompt, saved.Prompt, StringComparison.Ordinal)
            || Blank(_everyStartPrompt) != saved.EveryStartPrompt
            || Blank(_statusDocumentPath) != saved.StatusDocumentPath
            || Blank(_windowGroup) != saved.WindowGroup
            || _boardOrder != saved.BoardOrder);

    /// <summary>Whether the setup dialog is open.</summary>
    public bool IsEditingSetup
    {
        get => _isEditingSetup;
        private set => Set(ref _isEditingSetup, value);
    }

    /// <summary>Opens the setup dialog.</summary>
    public RelayCommand EditSetupCommand { get; }

    /// <summary>Closes the setup dialog, putting back what was there.</summary>
    public RelayCommand CancelSetupCommand { get; }

    /// <summary>Shows the Links tab, from the chain line on Overview.</summary>
    public RelayCommand ShowLinksCommand { get; }

    /// <summary>Whether this workstream is put away.</summary>
    public bool IsArchived => _card?.Workstream.IsArchived ?? false;

    /// <summary>What the archive button says, which is the opposite thing when it is already away.</summary>
    public string ArchiveLabel => IsArchived ? "Restore" : "Archive";

    /// <summary>Whether a session is running, which is what stops either from being possible.</summary>
    public bool IsLive => _card?.IsLive ?? false;

    /// <summary>Why neither can be done, or empty when both can.</summary>
    public string RemovalRefusal => WorkstreamRemoval.RefusalToArchive(IsLive) ?? string.Empty;

    /// <summary>What deleting will destroy, said before it is done.</summary>
    public string DeleteConsequence => WorkstreamRemoval.Consequence(Key);

    /// <summary>Hides the drawer.</summary>
    public RelayCommand CloseCommand { get; }

    /// <summary>The card the drawer is showing, for the commands that act on it from the board.</summary>
    public WorkstreamCardViewModel? Card => _card;

    /// <summary>Writes the setup back to the plan and board files and closes the setup dialog.</summary>
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

    /// <summary>The workflows this workstream's last session ran, newest first.</summary>
    public ObservableCollection<WorkflowRunViewModel> Workflows { get; } = [];

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
            if (SetSetup(ref _statusDocumentPath, value))
            {
                Raise(nameof(HandoffDestination));
            }
        }
    }

    /// <summary>The file a stop request will actually name, whether chosen or found.</summary>
    public string HandoffDestination => _card is null
        ? string.Empty
        : HandoffPath.For(_card.Workstream with { StatusDocumentPath = Blank(_statusDocumentPath) });

    /// <summary>The tab title and card heading. Saved as soon as it changes.</summary>
    public string Title
    {
        get => _title;
        set
        {
            if (Set(ref _title, value))
            {
                ApplyOverview();
            }
        }
    }

    /// <summary>Where sessions start.</summary>
    public string WorkingDirectory
    {
        get => _workingDirectory;
        set
        {
            if (SetSetup(ref _workingDirectory, value))
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
            if (SetSetup(ref _prompt, value))
            {
                Raise(nameof(UnclaimedKeys));
            }
        }
    }

    /// <summary>What this workstream's sessions are told on every start, after the board's own prompts.</summary>
    public string EveryStartPrompt
    {
        get => _everyStartPrompt;
        set => SetSetup(ref _everyStartPrompt, value);
    }

    /// <summary>
    /// Whether the workstream has run before, which is when its opening prompt stops being sent. Judged
    /// the way a launch judges it, so the box is locked exactly when the prompt in it would be ignored.
    /// </summary>
    public bool HasRun =>
        _card is not null && SessionOpening.HasHistory(_card.Workstream, File.Exists(HandoffDestination));

    /// <summary>What the opening prompt is for, now.</summary>
    public string FirstRunHint => HasRun
        ? "Sent when this workstream first started, and never again. Kept here to read; to tell its sessions something from now on, use Every start."
        : "Sent once, on this workstream's first start. After that it is replaced by the continuation in Settings.";

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
        set => SetSetup(ref _boardOrder, value);
    }

    /// <summary>Whether the work is blocked. A flag on the card, deliberately not a lane. Saved at once.</summary>
    public bool IsBlocked
    {
        get => _isBlocked;
        set
        {
            if (Set(ref _isBlocked, value))
            {
                ApplyOverview();
            }
        }
    }

    /// <summary>Whether the work is paused: set aside on purpose, keeping its stage. Saved at once.</summary>
    public bool IsPaused
    {
        get => _isPaused;
        set
        {
            if (Set(ref _isPaused, value))
            {
                ApplyOverview();
            }
        }
    }

    /// <summary>Whether a whole-board Start should include this workstream. Saved at once.</summary>
    public bool ReopenTomorrow
    {
        get => _reopenTomorrow;
        set
        {
            if (Set(ref _reopenTomorrow, value))
            {
                ApplyOverview();
            }
        }
    }

    /// <summary>Shows a workstream, loading its plan and its run history, on the tab asked for.</summary>
    public void Show(WorkstreamCardViewModel card, DrawerTab tab = DrawerTab.Overview)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (IsOpen && ReferenceEquals(_card, card))
        {
            SelectedTab = tab;

            return;
        }

        Load(card, tab);
    }

    /// <summary>
    /// Brings Blocked and Reopen tomorrow into line with the card after they were changed from its
    /// menu, without touching anything being edited.
    /// </summary>
    public void Reload(WorkstreamCardViewModel card)
    {
        if (!ReferenceEquals(_card, card))
        {
            return;
        }

        _loading = true;
        IsBlocked = card.Workstream.IsBlocked;
        IsPaused = card.Workstream.IsPaused;
        ReopenTomorrow = card.Workstream.ReopenTomorrow;
        _loading = false;
    }

    private void Load(WorkstreamCardViewModel card, DrawerTab tab)
    {
        _loading = true;
        _card = card;
        Raise(nameof(Card));
        var workstream = card.Workstream;

        Title = workstream.Title;
        IsBlocked = workstream.IsBlocked;
        IsPaused = workstream.IsPaused;
        ReopenTomorrow = workstream.ReopenTomorrow;
        LoadSetup(workstream);
        IsEditingSetup = false;

        JiraKeys.Clear();
        foreach (var key in workstream.JiraKeys)
        {
            JiraKeys.Add(key);
        }

        NewJiraKey = string.Empty;
        IsConfirmingDelete = false;

        Raise(nameof(IsArchived));
        Raise(nameof(ArchiveLabel));
        Raise(nameof(IsLive));
        Raise(nameof(RemovalRefusal));
        Raise(nameof(DeleteConsequence));

        Runs.Clear();
        foreach (var run in _runs.Read()
                     .Where(run => string.Equals(run.WorkstreamKey, card.Key, StringComparison.OrdinalIgnoreCase))
                     .OrderByDescending(run => run.StartedUtc))
        {
            Runs.Add(new RunHistoryEntryViewModel(run));
        }

        Workflows.Clear();
        foreach (var workflow in _workflows.Read(workstream.LastSessionId))
        {
            Workflows.Add(new WorkflowRunViewModel(workflow));
        }

        Raise(nameof(Key));
        Raise(nameof(PlanPath));
        Raise(nameof(ColorHex));
        Raise(nameof(UnclaimedKeys));
        Raise(nameof(HandoffDestination));

        _loading = false;
        SelectedTab = tab;
        IsOpen = true;
    }

    private void LoadSetup(Workstream workstream)
    {
        WorkingDirectory = workstream.WorkingDirectory;
        Prompt = workstream.Prompt;
        EveryStartPrompt = workstream.EveryStartPrompt ?? string.Empty;
        Raise(nameof(HasRun));
        Raise(nameof(FirstRunHint));
        StatusDocumentPath = workstream.StatusDocumentPath ?? string.Empty;
        WindowGroup = workstream.WindowGroup ?? string.Empty;
        BoardOrder = workstream.BoardOrder;
        NewDirectory = string.Empty;

        AddDirectories.Clear();
        foreach (var directory in workstream.AddDirectories)
        {
            AddDirectories.Add(directory);
        }

        Raise(nameof(IsSetupDirty));
    }

    private void AddJiraKey()
    {
        JiraKeys.Add(JiraKeyExtractor.Normalise(NewJiraKey)!);
        NewJiraKey = string.Empty;
        Raise(nameof(UnclaimedKeys));
        ApplyOverview();
    }

    private void RemoveJiraKey(object? parameter)
    {
        if (parameter is string key)
        {
            JiraKeys.Remove(key);
            Raise(nameof(CanAddJiraKey));
            Raise(nameof(UnclaimedKeys));
            ApplyOverview();
        }
    }

    private void RescanPrompt()
    {
        foreach (var key in UnclaimedKeys)
        {
            JiraKeys.Add(key);
        }

        Raise(nameof(UnclaimedKeys));
        ApplyOverview();
    }

    private void AddDirectory()
    {
        AddDirectories.Add(NewDirectory.Trim());
        NewDirectory = string.Empty;
        Raise(nameof(IsSetupDirty));
    }

    private void RemoveDirectory(object? parameter)
    {
        if (parameter is string directory)
        {
            AddDirectories.Remove(directory);
            Raise(nameof(CanAddDirectory));
            Raise(nameof(IsSetupDirty));
        }
    }

    private void ApplyOverview()
    {
        if (_loading || _card is null)
        {
            return;
        }

        var key = _card.Key;

        if (_plans.TryRead(key) is { } plan && !string.Equals(plan.Title, Title, StringComparison.Ordinal))
        {
            _plans.Write(key, plan with { Title = Title, WrittenAt = DateTimeOffset.Now.ToString("s") });
        }

        _card.Workstream = _card.Workstream with
        {
            Title = Title,
            IsBlocked = IsBlocked,
            IsPaused = IsPaused,
            ReopenTomorrow = ReopenTomorrow,
            JiraKeys = [.. JiraKeys],
            RemovedJiraKeys = JiraKeySelection.Removed(
                _card.Workstream.JiraKeys, JiraKeys, _card.Workstream.RemovedJiraKeys),
        };

        _saved(_card);
    }

    private void SaveSetup()
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
            Cwd = WorkingDirectory,
            AddDirs = [.. AddDirectories],
            Prompt = Prompt,
            WrittenAt = DateTimeOffset.Now.ToString("s"),
        });

        _card.Workstream = _card.Workstream with
        {
            WorkingDirectory = WorkingDirectory,
            AddDirectories = [.. AddDirectories],
            Prompt = Prompt,
            BoardOrder = BoardOrder,
            StatusDocumentPath = Blank(StatusDocumentPath),
            WindowGroup = Blank(WindowGroup),
            EveryStartPrompt = Blank(EveryStartPrompt),
        };

        Raise(nameof(IsSetupDirty));
        _saved(_card);
    }

    private bool SetSetup<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (!Set(ref field, value, name))
        {
            return false;
        }

        Raise(nameof(IsSetupDirty));

        return true;
    }

    private void Archive()
    {
        if (_card is not { } card)
        {
            return;
        }

        var archived = !IsArchived;

        _card = null;
        IsOpen = false;
        _archived?.Invoke(card, archived);
    }

    private void Delete()
    {
        if (_card is not { } card)
        {
            return;
        }

        IsConfirmingDelete = false;
        _card = null;
        IsOpen = false;
        _removed?.Invoke(card);
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
