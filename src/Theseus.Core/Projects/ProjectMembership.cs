using System.Text.RegularExpressions;
using Theseus.Core.Domain;

namespace Theseus.Core.Projects;

/// <summary>
/// Which project a card belongs to and the slug it works under (decision 2, Nigel 2026-11-20; spec AC 4 and 10). A card
/// belongs to at most one project and holds one current slug; two cards share a slug only when one is a verifier.
/// </summary>
public static partial class ProjectMembership
{
    /// <summary>Whether text has a slug's shape: lower-case letters, digits and hyphens.</summary>
    public static bool IsSlug(string? text) => text is not null && SlugShape().IsMatch(text);

    /// <summary>Why <paramref name="card"/> cannot take this membership, or null when it can.</summary>
    /// <param name="cards">Every card on the board.</param>
    /// <param name="card">The card being changed.</param>
    /// <param name="project">The project, or null to take it out of any.</param>
    /// <param name="slug">The slug, or null for none yet.</param>
    public static string? Refusal(IEnumerable<Workstream> cards, Workstream card, string? project, string? slug)
    {
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(card);

        if (project is null || slug is null || card.Role == WorkstreamRole.Verifier)
        {
            return null;
        }

        var holder = cards.FirstOrDefault(other =>
            !string.Equals(other.Key, card.Key, StringComparison.OrdinalIgnoreCase)
            && !other.IsArchived
            && other.Role != WorkstreamRole.Verifier
            && string.Equals(other.Project, project, StringComparison.OrdinalIgnoreCase)
            && string.Equals(other.Slug, slug, StringComparison.OrdinalIgnoreCase));

        return holder is null
            ? null
            : $"{holder.Title} already works under {slug} in {project}. Two cards share a slug only when one is a verifier.";
    }

    /// <summary>The card in a project under a slug, or out of every project when <paramref name="project"/> is null.</summary>
    public static Workstream Join(Workstream card, string? project, string? slug)
    {
        ArgumentNullException.ThrowIfNull(card);

        return card with { Project = project, Slug = project is null ? null : slug };
    }

    /// <summary>
    /// A card made from a proposal inherits its proposer's project, with the slug the proposal named; a proposal from
    /// an ad hoc card makes an ad hoc card (AC 4).
    /// </summary>
    public static Workstream FromProposal(Workstream created, Workstream proposer, string? slug)
    {
        ArgumentNullException.ThrowIfNull(created);
        ArgumentNullException.ThrowIfNull(proposer);

        return proposer.Project is null ? created : Join(created, proposer.Project, slug);
    }

    /// <summary>A verifier card takes its subject's project and slug (AC 10).</summary>
    public static Workstream ForVerifier(Workstream verifier, Workstream subject)
    {
        ArgumentNullException.ThrowIfNull(verifier);
        ArgumentNullException.ThrowIfNull(subject);

        return verifier with { Project = subject.Project, Slug = subject.Slug };
    }

    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugShape();
}
