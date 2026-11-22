namespace Theseus.Core.Finding;

/// <summary>
/// The fields of a card the search reads (item 2): its title, key, project, area, Jira keys, tags, branches and repos.
/// Branches and repos are included so a card can be found by the code it works on (decision 2, Nigel 2026-11-21).
/// </summary>
/// <param name="Key">The card's key.</param>
/// <param name="Title">Its title.</param>
/// <param name="Project">The project it belongs to, or empty.</param>
/// <param name="Area">Its area, or empty.</param>
/// <param name="JiraKeys">Its Jira keys.</param>
/// <param name="Tags">Its tags.</param>
/// <param name="Branches">The branches its repos have checked out.</param>
/// <param name="Repos">Its repos' folder names.</param>
public sealed record SearchableCard(
    string Key,
    string Title,
    string Project,
    string Area,
    IReadOnlyList<string> JiraKeys,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Branches,
    IReadOnlyList<string> Repos)
{
    /// <summary>Every field's text, for a word to be found in.</summary>
    public IEnumerable<string> Fields =>
        new[] { Key, Title, Project, Area }.Concat(JiraKeys).Concat(Tags).Concat(Branches).Concat(Repos)
            .Where(text => !string.IsNullOrEmpty(text));
}
