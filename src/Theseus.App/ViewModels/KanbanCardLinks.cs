using Theseus.Core.CiStatus;

namespace Theseus.App.ViewModels;

/// <summary>
/// What a kanban card links to from its board card (projects-kanban spec AC 17): the branch line, the CI status with its
/// rows, the pull requests and the Jira items, all as the main board's card already has them, so nothing is read twice.
/// </summary>
/// <param name="Branch">The board card's branch line, or empty.</param>
/// <param name="BranchToolTip">Its tooltip.</param>
/// <param name="CiText">The status button's words, such as "PR 41 · ✓ CI passed"; empty with none.</param>
/// <param name="CiState">The worst CI state, which colours it.</param>
/// <param name="CiRows">The status rows, each opening its pull request or run.</param>
/// <param name="PullRequests">The pull requests.</param>
/// <param name="Jira">The Jira items.</param>
public sealed record KanbanCardLinks(
    string Branch,
    string BranchToolTip,
    string CiText,
    CiState CiState,
    IReadOnlyList<CardItemRow> CiRows,
    IReadOnlyList<CardItemRow> PullRequests,
    IReadOnlyList<CardItemRow> Jira)
{
    /// <summary>A card with no board card behind it.</summary>
    public static KanbanCardLinks None { get; } = new(string.Empty, string.Empty, string.Empty, CiState.NotRead, [], [], []);

    /// <summary>The links of a board card.</summary>
    public static KanbanCardLinks Of(WorkstreamCardViewModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        return new KanbanCardLinks(card.Branch, card.BranchTooltip, card.StatusButtonText, card.StatusState, card.StatusItems,
            card.PullRequestItems, card.JiraItems);
    }

    /// <summary>Its text, for the kanban's signature: a change in any link redraws it.</summary>
    public string Signature => string.Join("|", Branch, CiText, CiState,
        string.Join(",", CiRows.Select(row => row.Display + row.Url)),
        string.Join(",", PullRequests.Select(row => row.Display + row.Url)),
        string.Join(",", Jira.Select(row => row.Display + row.Url)));
}
