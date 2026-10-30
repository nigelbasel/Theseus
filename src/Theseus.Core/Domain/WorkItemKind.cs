namespace Theseus.Core.Domain;

/// <summary>What a work item recorded against a card is.</summary>
public enum WorkItemKind
{
    /// <summary>A Jira issue, by its key.</summary>
    Jira,

    /// <summary>A pull request, by its number.</summary>
    PullRequest,
}
