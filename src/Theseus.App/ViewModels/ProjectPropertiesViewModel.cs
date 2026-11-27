using Theseus.Core.Colour;

namespace Theseus.App.ViewModels;

/// <summary>
/// Project properties… on a project card's menu (1.1d AC 40): its title, which OK writes to its project.md (1.1e AC 12a;
/// Nigel, 2026-11-21, could not rename "Theseus bootstrap"); its colour, chosen from the project palette (decision 7), with
/// the colours other projects hold marked; and the path of its project.md, read only, saying why. Colours, places and the
/// run state stay keyed by the folder's name, so a new title changes nothing else.
/// </summary>
public sealed class ProjectPropertiesViewModel : ObservableObject
{
    private string _colourHex;
    private string _title;
    private string? _error;

    /// <summary>Creates the properties of a project card.</summary>
    /// <param name="name">The project's name.</param>
    /// <param name="title">Its title.</param>
    /// <param name="colourHex">Its colour now.</param>
    /// <param name="projectFile">The full path of its project.md.</param>
    /// <param name="others">The other projects' colours, by their titles.</param>
    public ProjectPropertiesViewModel(string name, string title, string colourHex, string projectFile,
        IReadOnlyDictionary<string, string>? others = null)
    {
        Name = name;
        ShownTitle = title;
        _title = title;
        ProjectFile = projectFile;
        _colourHex = colourHex;

        IReadOnlyList<string> colours = ProjectColourPalette.Contains(colourHex)
            ? ProjectColourPalette.All
            : [colourHex, .. ProjectColourPalette.All];

        Colours = [.. colours.Select(colour => new ColourSwatchViewModel(colour, string.Join(", ", (others ?? new Dictionary<string, string>())
            .Where(other => string.Equals(other.Value, colour, StringComparison.OrdinalIgnoreCase))
            .Select(other => other.Key)
            .Order(StringComparer.CurrentCultureIgnoreCase))))];
    }

    /// <summary>
    /// Whether the overnight queue resumes this project's idle cards: off unless the owner ticks it, since the queue acts on
    /// nobody's request (Nigel, 2026-11-24). Written at OK.
    /// </summary>
    public bool ResumesIdleCards { get; set; }

    /// <summary>The project card's Tags field (card-search-and-tags item 10), written at OK.</summary>
    public TagEditorViewModel? TagEditor { get; init; }

    /// <summary>Whether it has a Tags field.</summary>
    public bool HasTagEditor => TagEditor is not null;

    /// <summary>The project's name, which keys its colour.</summary>
    public string Name { get; }

    /// <summary>Its title as the file had it when the dialog opened.</summary>
    public string ShownTitle { get; }

    /// <summary>Its title, as typed: OK writes it to the file when it differs from <see cref="ShownTitle"/>.</summary>
    public string Title
    {
        get => _title;
        set
        {
            if (Set(ref _title, value))
            {
                Error = null;
            }
        }
    }

    /// <summary>Whether the title was changed.</summary>
    public bool TitleChanged => !string.Equals(_title.Trim(), ShownTitle, StringComparison.Ordinal);

    /// <summary>
    /// Writes a changed title, as the shell gives it: the file's refusal, such as the file having changed underneath, or
    /// null once written. Unset, a title is not written.
    /// </summary>
    public Func<string, string?>? WriteTitle { get; init; }

    /// <summary>Why the title was not written, shown under it; null when there is nothing to say.</summary>
    public string? Error
    {
        get => _error;
        private set
        {
            if (Set(ref _error, value))
            {
                Raise(nameof(HasError));
            }
        }
    }

    /// <summary>Whether a refusal is shown.</summary>
    public bool HasError => _error is not null;

    /// <summary>The project's primary branch, as its file names it now, or null.</summary>
    public string? PrimaryBranch { get; init; }

    /// <summary>
    /// Whether its cards may branch off the primary branch, push and merge back without asking (1.1e AC 12b): the owner's
    /// grant, kept in Theseus's settings, never in project.md.
    /// </summary>
    public bool GrantsBranchRules
    {
        get => _grantsBranchRules;
        set
        {
            if (Set(ref _grantsBranchRules, value))
            {
                if (!value)
                {
                    GrantsItemBranches = false;
                }

                Raise(nameof(BranchRulesValue));
                Raise(nameof(CanChooseItemBranches));
            }
        }
    }

    private bool _grantsBranchRules;

    /// <summary>
    /// Whether the grant is for a branch per item off its repository's trunk, with a pull request the owner completes,
    /// rather than for merging into the primary branch (2.4 W2). Only with the grant; off by default.
    /// </summary>
    public bool GrantsItemBranches
    {
        get => _grantsItemBranches;
        set
        {
            if (Set(ref _grantsItemBranches, value && _grantsBranchRules))
            {
                Raise(nameof(BranchRulesValue));
            }
        }
    }

    private bool _grantsItemBranches;

    /// <summary>Whether the kind can be chosen: only while the grant is ticked.</summary>
    public bool CanChooseItemBranches => _grantsBranchRules;

    /// <summary>
    /// Whether the project runs unattended (2.4 W2): its cards are refused the question tool and told to park, end once
    /// they have reported back, and are resumed after a network error. The owner's setting, off until he ticks it; written
    /// at OK.
    /// </summary>
    public bool IsUnattended { get; set; }

    /// <summary>
    /// Whether the project's cards run headless unless a card says otherwise (Nigel, 2026-11-24); off leaves each card to
    /// its role: verifiers and scenario writers headless, the rest in a tab.
    /// </summary>
    public bool RunsHeadless { get; set; }

    /// <summary>Why its project.md does not read, or null (1.1e AC 18): shown here, not on the card's face.</summary>
    public string? Problem { get; init; }

    /// <summary>Whether there is a reason to show.</summary>
    public bool HasProblem => !string.IsNullOrWhiteSpace(Problem);

    /// <summary>The primary branch the standing grant covers, or null when none is given.</summary>
    public string? GrantedPrimary { get; init; }

    /// <summary>Whether the grant can be given: only while the file names a plain primary branch that is not a trunk.</summary>
    public bool CanGrantBranchRules => Theseus.Core.Projects.ProjectBranchRules.HasPrimary(PrimaryBranch);

    /// <summary>Whether the box can be changed: to grant, or always to withdraw a grant given (review of PR 2c, finding 4).</summary>
    public bool CanChangeBranchRules => CanGrantBranchRules || GrantedPrimary is not null;

    /// <summary>The check box's words, naming the primary branch.</summary>
    public string BranchRulesLabel => $"Workstreams may branch off {(CanGrantBranchRules ? PrimaryBranch!.Trim() : "its primary branch")}, push, and merge back into it without asking";

    /// <summary>What the grant means, or why it cannot be given.</summary>
    public string BranchRulesToolTip => GrantedPrimary is { } granted
                                        && !string.Equals(granted, PrimaryBranch?.Trim(), StringComparison.Ordinal)
        ? $"Granted for {granted}, but its project.md now names {(string.IsNullOrWhiteSpace(PrimaryBranch) ? "no primary branch" : PrimaryBranch.Trim())}, so it gives nothing now. OK with it ticked grants it for that one; untick it to withdraw it"
        : CanGrantBranchRules
        ? "Its cards are told at each start that they may make their own branch off the primary branch and its worktree, commit, push and merge back once their suite passes and their review is done. They still ask before any Jira item, pull request into the trunk, force push, branch deletion, or anything outside the project's repositories."
        : "Its project.md names no primary branch that can be granted (none, a trunk such as main, or not a branch name), so there is nothing to branch off";

    /// <summary>Why the file's path cannot be edited here.</summary>
    public string FileToolTip => $"{ProjectFile}. Read only: the file is where the project's folder is; move the folder to move it";

    /// <summary>The cards that may lead it, by key, each shown as "Title (key)".</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Leads { get; init; } = [];

    /// <summary>The lead card's key.</summary>
    public string? Lead
    {
        get => _lead;
        set
        {
            if (Set(ref _lead, value))
            {
                Raise(nameof(LeadValue));
            }
        }
    }

    private string? _lead;

    /// <summary>Whether a lead can be chosen: only once the project has workstreams (baseline C11).</summary>
    public bool CanChooseLead => Leads.Count > 0;

    /// <summary>What Change does, or why it is off.</summary>
    public string LeadChangeToolTip => CanChooseLead
        ? "Choose the project's lead from its workstreams"
        : "The project has no workstreams yet, so there is no lead to choose";

    /// <summary>The lead as the Lead and branches tab shows it.</summary>
    public string LeadValue => Leads.FirstOrDefault(each => string.Equals(each.Key, _lead, StringComparison.OrdinalIgnoreCase)) is { Key: not null } chosen
        ? chosen.Value
        : Leads.Count == 0 ? "None: the project has no workstreams yet" : "None chosen";

    /// <summary>What the branch rules mean, as a sentence under them.</summary>
    public string BranchRulesExplanation => $"{BranchRulesLabel}.";

    /// <summary>The branch rules as the Lead and branches tab shows them.</summary>
    public string BranchRulesValue => GrantsBranchRules
        ? GrantsItemBranches
            ? $"Granted for {(CanGrantBranchRules ? PrimaryBranch!.Trim() : GrantedPrimary)}: a branch per item off its repository's trunk, with a pull request you complete"
            : $"Granted for {(CanGrantBranchRules ? PrimaryBranch!.Trim() : GrantedPrimary)}"
        : CanGrantBranchRules ? "Not granted: workstreams ask before branching or pushing" : "Cannot be granted";

    /// <summary>The tab the window opens at: <see cref="HoldoutsTab"/> from Set the holdout folder…, else the first.</summary>
    public string? OpenAt { get; init; }

    /// <summary>What <see cref="OpenAt"/> names for the Holdouts tab.</summary>
    public const string HoldoutsTab = "Holdouts";

    /// <summary>
    /// The project's holdout folder in force when the dialog opened, and how it was found; null when the dialog was opened
    /// without one, as in tests of the other tabs.
    /// </summary>
    public Theseus.Core.Links.HoldoutLocation? Holdout { get; init; }

    /// <summary>The folder found by convention, which Reset goes back to.</summary>
    public Theseus.Core.Links.HoldoutLocation? HoldoutByConvention { get; init; }

    /// <summary>The folder the owner chose for this project when the dialog opened, or null.</summary>
    public string? ChosenHoldout { get; init; }

    private string? _holdoutChoice;
    private bool _holdoutChoiceMade;

    /// <summary>Whether Change… or Reset changed the holdout folder, which OK then writes.</summary>
    public bool HoldoutChanged => _holdoutChoiceMade && !string.Equals(_holdoutChoice, ChosenHoldout, StringComparison.OrdinalIgnoreCase);

    /// <summary>The folder to choose for the project at OK, or null to find it by convention.</summary>
    public string? HoldoutChoice => _holdoutChoiceMade ? _holdoutChoice : ChosenHoldout;

    /// <summary>The holdout folder as the Holdouts tab shows it.</summary>
    public string HoldoutValue => _holdoutChoiceMade
        ? _holdoutChoice ?? Folder(HoldoutByConvention)
        : Holdout is { IsMissing: true, Folder.Length: > 0 } missing ? $"{missing.Folder} (not there)" : Folder(Holdout);

    /// <summary>How the folder shown was found, and where else Theseus looks.</summary>
    public string HoldoutExplanation
    {
        get
        {
            var how = !_holdoutChoiceMade
                ? Holdout is null or { IsMissing: true, Source: Theseus.Core.Links.HoldoutSource.Recorded }
                    ? null
                    : Sentence(Theseus.Core.Links.HoldoutFolderEntry.HowFoundOf(Holdout.Source))
                : _holdoutChoice is null
                    ? $"Found by convention once you press OK: {Theseus.Core.Links.HoldoutFolderEntry.HowFoundOf(HoldoutByConvention?.Source ?? Theseus.Core.Links.HoldoutSource.TheseusOwn)}."
                    : "You chose it, once you press OK.";
            var missing = !_holdoutChoiceMade && Holdout is { IsMissing: true }
                ? Holdout.Source == Theseus.Core.Links.HoldoutSource.Recorded
                    ? "project.md records a holdout repository, but no clone of it is there: clone it beside the home repo, or choose a folder."
                    : "The folder is not there: choose it again, or Reset."
                : null;
            const string order = "Looked for in this order: the folder you choose here; the holdout repository project.md records; "
                                 + "<repo>-holdout beside the home repo's clone; Theseus's own.";

            return string.Join(" ", new[] { how, missing, order }.Where(part => part is not null));
        }
    }

    /// <summary>Whether Reset applies: a folder is chosen, or about to be.</summary>
    public bool CanResetHoldout => HoldoutChoice is not null;

    /// <summary>Whether the folder in force is Theseus's own, which can be kept in a private repository.</summary>
    public bool HoldoutIsTheseusOwn => !_holdoutChoiceMade && Holdout is { IsDefault: true };

    /// <summary>
    /// Change…: chooses the project's holdout folder, written at OK. Why it cannot be, or null; the reason is also shown
    /// under the holdout row (baseline A19), not in a message box.
    /// </summary>
    public string? ChooseHoldout(string folder)
    {
        if (WhyNotHoldout(folder) is { } why)
        {
            HoldoutError = why;

            return why;
        }

        SetHoldoutChoice(folder);

        return null;
    }

    /// <summary>Reset: the folder is found by convention again, at OK.</summary>
    public void ResetHoldout() => SetHoldoutChoice(null);

    /// <summary>
    /// Adds the private repository's remote to Theseus's own holdout folder, as the shell gives it; the folder's own
    /// <see cref="Theseus.Core.Links.HoldoutLocation.AddRemote"/> when unset. Why it could not, or null.
    /// </summary>
    public Func<string, string?>? AddRemote { get; init; }

    private string? _pendingRemote;

    /// <summary>The private repository's address chosen, which OK adds as the folder's remote; null when none was chosen.</summary>
    public string? PendingRemote
    {
        get => _pendingRemote;
        private set
        {
            if (Set(ref _pendingRemote, value))
            {
                Raise(nameof(HasPendingRemote));
                Raise(nameof(PendingRemoteText));
            }
        }
    }

    /// <summary>Whether an address waits for OK.</summary>
    public bool HasPendingRemote => _pendingRemote is not null;

    /// <summary>What happens to the chosen address, shown under Use a private repository….</summary>
    public string PendingRemoteText => _pendingRemote is null ? string.Empty : $"Its remote becomes {_pendingRemote} once you press OK.";

    /// <summary>
    /// Use a private repository…: keeps the address until OK, so Cancel adds nothing (baseline W6 and A2: a properties
    /// dialog holds its changes until OK).
    /// </summary>
    public void KeepInPrivateRepository(string address)
    {
        HoldoutError = null;
        PendingRemote = address.Trim();
    }

    /// <summary>Why an address cannot be a repository's, or null: asked in the prompt, before it closes.</summary>
    public static string? WhyNotRemote(string address) =>
        string.IsNullOrWhiteSpace(address) || address.Trim().StartsWith('-')
            ? "Give the repository's address, such as https://host/owner/repo.git."
            : null;

    private string? _holdoutError;

    /// <summary>Why the holdout folder or its repository was refused, shown under the holdout row; null when nothing was.</summary>
    public string? HoldoutError
    {
        get => _holdoutError;
        private set
        {
            if (Set(ref _holdoutError, value))
            {
                Raise(nameof(HasHoldoutError));
            }
        }
    }

    /// <summary>Whether a holdout refusal is shown.</summary>
    public bool HasHoldoutError => _holdoutError is not null;

    /// <summary>
    /// Why a folder cannot be a project's holdout, or null (review of PR 7, finding 9): the guard protects neither a whole
    /// drive nor a relative path.
    /// </summary>
    public static string? WhyNotHoldout(string folder) =>
        string.IsNullOrWhiteSpace(folder) || !System.IO.Path.IsPathFullyQualified(folder)
            ? "Choose a full path, such as C:\\src\\Repo-holdout."
            : string.Equals(System.IO.Path.GetPathRoot(folder)?.TrimEnd('\\', '/'), folder.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)
                ? "A whole drive cannot be a holdout folder: the guard would protect nothing."
                : null;

    private void SetHoldoutChoice(string? folder)
    {
        // A staged remote belongs to the folder in force when it was staged, which is no longer the one OK keeps.
        PendingRemote = null;
        HoldoutError = null;
        _holdoutChoice = folder;
        _holdoutChoiceMade = true;
        Raise(nameof(HoldoutValue));
        Raise(nameof(HoldoutExplanation));
        Raise(nameof(CanResetHoldout));
        Raise(nameof(HoldoutIsTheseusOwn));
    }

    private static string Sentence(string text) => $"{char.ToUpperInvariant(text[0])}{text[1..]}.";

    private static string Folder(Theseus.Core.Links.HoldoutLocation? location) =>
        location is null ? "Not known" : location.Folder.Length == 0 ? "Not found" : location.Folder;

    /// <summary>The project.md path as the General tab shows it.</summary>
    public string ProjectFileExplanation => "Read only: the file is where the project's folder is. Move the folder to move it.";

    /// <summary>OK: writes a changed title and says whether the dialog may close; a refusal stays, shown under the title.</summary>
    public bool Apply()
    {
        // What can be refused without writing is checked first, so a refusal leaves nothing written. A title the file
        // refuses after the remote was added leaves that remote in place; Cancel then keeps it.
        if (TitleChanged && _title.Trim().Length == 0)
        {
            Error = "A project needs a title.";

            return false;
        }

        if (_pendingRemote is { } address)
        {
            Func<string, string?>? add = AddRemote ?? (Holdout is { IsDefault: true } own ? own.AddRemote : null);

            if (add?.Invoke(address) is { } why)
            {
                HoldoutError = why;

                return false;
            }

            PendingRemote = null;
        }

        if (!TitleChanged)
        {
            return true;
        }

        Error = WriteTitle?.Invoke(_title.Trim());

        return Error is null;
    }

    /// <summary>The full path of its project.md.</summary>
    public string ProjectFile { get; }

    /// <summary>The colours to choose from: the project palette, and the project's own first when it is not in it.</summary>
    public IReadOnlyList<ColourSwatchViewModel> Colours { get; }

    /// <summary>The colour chosen, as <c>#rrggbb</c>.</summary>
    public string ColourHex
    {
        get => _colourHex;
        set => Set(ref _colourHex, value);
    }
}
