using Theseus.Core.Domain;

namespace Theseus.App.ViewModels;

/// <summary>Builds a card's drop-down rows from what the board knows and what its sessions recorded.</summary>
public static class CardItemRows
{
    /// <summary>
    /// Every Jira key the card carries, then any a session recorded that it does not, each with the
    /// title a session gave it.
    /// </summary>
    public static IReadOnlyList<CardItemRow> Jira(IReadOnlyList<string> keys, IReadOnlyList<WorkItem> recorded, string? jiraBaseUrl,
        Func<string, string?>? titleOf = null)
    {
        var mine = recorded.Where(item => item.Kind == WorkItemKind.Jira)
            .ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var all = keys.Concat(mine.Keys).Distinct(StringComparer.OrdinalIgnoreCase);

        return [.. all.Select(key =>
        {
            var item = mine.GetValueOrDefault(key);

            return new CardItemRow(key, item?.Title ?? titleOf?.Invoke(key), item?.Url ?? JiraSite.IssueUrl(jiraBaseUrl, key));
        })];
    }

    /// <summary>The pull requests the card's prompt names, then any a session recorded, with titles.</summary>
    public static IReadOnlyList<CardItemRow> PullRequests(IReadOnlyList<PullRequestLink> mentioned, IReadOnlyList<WorkItem> recorded,
        Func<string, string?>? titleOf = null)
    {
        var titles = recorded.Where(item => item.Kind == WorkItemKind.PullRequest)
            .ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var rows = mentioned.Select(link =>
        {
            var number = link.Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var item = titles.GetValueOrDefault(number);

            return new CardItemRow($"PR {link.Number}", item?.Title ?? titleOf?.Invoke(number), item?.Url ?? link.Url);
        }).ToList();

        var named = mentioned.Select(link => link.Number.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        rows.AddRange(titles.Values.Where(item => !named.Contains(item.Id))
            .Select(item => new CardItemRow($"PR {item.Id}", item.Title, item.Url)));

        return rows;
    }
}
