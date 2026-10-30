using Theseus.Core.Domain;

namespace Theseus.App.ViewModels;

/// <summary>Builds a card's drop-down rows from what the board knows and what its sessions recorded.</summary>
public static class CardItemRows
{
    /// <summary>
    /// Every Jira key the card carries, then any a session recorded that it does not, each with the
    /// title a session gave it.
    /// </summary>
    public static IReadOnlyList<CardItemRow> Jira(IReadOnlyList<string> keys, IReadOnlyList<WorkItem> recorded, string? jiraBaseUrl)
    {
        var titles = recorded.Where(item => item.Kind == WorkItemKind.Jira)
            .ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var all = keys.Concat(titles.Keys).Distinct(StringComparer.OrdinalIgnoreCase);

        return [.. all.Select(key => titles.GetValueOrDefault(key) is { } item
            ? new CardItemRow(key, item.Title, item.Url ?? JiraSite.IssueUrl(jiraBaseUrl, key))
            : new CardItemRow(key, null, JiraSite.IssueUrl(jiraBaseUrl, key)))];
    }

    /// <summary>The pull requests the card's prompt names, then any a session recorded, with titles.</summary>
    public static IReadOnlyList<CardItemRow> PullRequests(IReadOnlyList<PullRequestLink> mentioned, IReadOnlyList<WorkItem> recorded)
    {
        var titles = recorded.Where(item => item.Kind == WorkItemKind.PullRequest)
            .ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var rows = mentioned.Select(link =>
        {
            var item = titles.GetValueOrDefault(link.Number.ToString(System.Globalization.CultureInfo.InvariantCulture));

            return new CardItemRow($"PR {link.Number}", item?.Title, item?.Url ?? link.Url);
        }).ToList();

        var named = mentioned.Select(link => link.Number.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        rows.AddRange(titles.Values.Where(item => !named.Contains(item.Id))
            .Select(item => new CardItemRow($"PR {item.Id}", item.Title, item.Url)));

        return rows;
    }
}
