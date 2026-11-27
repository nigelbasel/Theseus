using System.Windows.Input;
using Theseus.Core.Kanban;
using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// One card of the projects kanban: a piece of work, which is the project view's own box (<see cref="PieceNodeViewModel"/>)
/// with the links of its board card (projects-kanban spec AC 16 to 19). It works out no state; its button and menu are
/// the box's.
/// </summary>
public sealed class KanbanCardViewModel
{
    internal KanbanCardViewModel(PieceNodeViewModel box, KanbanColumn column, string projectTitle, KanbanCardLinks links,
        Action open)
    {
        Box = box;
        Column = column;
        ProjectTitle = projectTitle;
        Links = links;
        OpenCommand = new RelayCommand(_ => open());
    }

    /// <summary>The project view's box for the piece.</summary>
    public PieceNodeViewModel Box { get; }

    /// <summary>The column it sits in.</summary>
    public KanbanColumn Column { get; }

    /// <summary>Its project's title.</summary>
    public string ProjectTitle { get; }

    /// <summary>Its board card's links.</summary>
    public KanbanCardLinks Links { get; }

    /// <summary>Its title.</summary>
    public string Title => Box.Title.Length > 0 ? Box.Title : Box.Name;

    /// <summary>Its state, which orders the cards in a cell (AC 20).</summary>
    public StageNodeState State => Box.State;

    /// <summary>"⑂ nigel/feature", or empty: the piece's branch, else its board card's branch line.</summary>
    public string BranchText => PieceBranch is { } branch ? $"⑂ {branch}" : Links.Branch.Length > 0 ? $"⑂ {Links.Branch}" : string.Empty;

    /// <summary>Whether the branch shows.</summary>
    public bool HasBranch => BranchText.Length > 0;

    /// <summary>The branch's tooltip.</summary>
    public string BranchToolTip => PieceBranch is { } branch ? $"Its branch: {branch}" : Links.BranchToolTip;

    private string? PieceBranch => Box.Piece.Branch is { Length: > 0 } branch && !string.Equals(branch, "none", StringComparison.OrdinalIgnoreCase)
        ? branch
        : null;

    /// <summary>Whether its CI status shows.</summary>
    public bool HasCi => Links.CiText.Length > 0;

    /// <summary>Whether it has Jira items.</summary>
    public bool HasJira => Links.Jira.Count > 0;

    /// <summary>The Jira button's words: the key, or "Jira n".</summary>
    public string JiraText => Links.Jira.Count == 1 ? Links.Jira[0].Id : $"Jira {Links.Jira.Count}";

    /// <summary>Whether it has pull requests that its CI rows do not already list.</summary>
    public bool HasPullRequests => !HasCi && Links.PullRequests.Count > 0;

    /// <summary>The pull request button's words.</summary>
    public string PullRequestText => Links.PullRequests.Count == 1 ? Links.PullRequests[0].Id : $"PRs {Links.PullRequests.Count}";

    /// <summary>Open records, as the box offers it; null when it has no records folder.</summary>
    public PieceActionViewModel? Records => Box.Actions.FirstOrDefault(action => action.Label == "Open records" && action.IsEnabled);

    /// <summary>Whether ▤ Records shows.</summary>
    public bool HasRecords => Records is not null;

    /// <summary>
    /// The verifiers checking it, on its card rather than as cards of their own (2.4d): "✓ verifier: name · state", or
    /// "✓ checked by name" once one has reported; empty with none.
    /// </summary>
    public string VerifierText
    {
        get
        {
            var piece = Box.Piece;

            if (piece.PendingVerifiers.Count > 0)
            {
                return string.Join(Environment.NewLine, piece.PendingVerifiers.Select(verifier =>
                    $"✓ verifier: {verifier.Title} · {(verifier.HasSession ? Theseus.Core.Domain.SessionMark.Of(verifier.Lifecycle) : "not started")}"));
            }

            return piece.CheckReported is { } checker ? $"✓ checked by {checker.Title}" : string.Empty;
        }
    }

    /// <summary>Whether a verifier line shows.</summary>
    public bool HasVerifier => VerifierText.Length > 0;

    /// <summary>Its button: the folded box's primary action (AC 19).</summary>
    public PieceActionViewModel? PrimaryAction => Box.PrimaryAction;

    /// <summary>Its right-click menu: every action of the open box, in the same order (AC 19).</summary>
    public IReadOnlyList<MenuEntryViewModel> MenuEntries => MenuAccessKeys.For(Box.Actions);

    /// <summary>Opens its project's tab with the piece selected (AC 3).</summary>
    public ICommand OpenCommand { get; }

    /// <summary>What a screen reader says (AC 24): "title, state word, in column, project".</summary>
    public string AutomationName => $"{Title}, {Box.Status.Word}, in {KanbanColumns.Name(Column)}, {ProjectTitle}";

    /// <summary>Its text, for the kanban's signature.</summary>
    internal string Signature => string.Join("|", Box.Name, Title, BranchText, Column, Box.StatusText, Box.WhoActs, Box.SessionMark, Box.ReportBackText,
        VerifierText, PrimaryAction?.Label, PrimaryAction?.IsEnabled, Links.Signature,
        string.Join(",", Box.Actions.Select(action => action.Label + action.IsEnabled)));
}
