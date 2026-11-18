namespace Theseus.Core.Domain;

/// <summary>
/// The branch line on a card (card-branches, spec approved by Nigel 2026-11-18). One line however many repos, with
/// the list in the tooltip, so a card does not grow with them (rubric R18). A card that declares no repos says
/// exactly what it said before.
/// </summary>
public static class BranchLines
{
    private const string Heading = "Branches of this card's repos:";

    /// <summary>The line for a card with this working directory and these declared repos.</summary>
    public static BranchLine For(string? workingDirectory, IReadOnlyList<CardRepo> repos)
    {
        ArgumentNullException.ThrowIfNull(repos);

        if (repos.Count == 0)
        {
            return GitBranchReader.Read(workingDirectory) is { } branch
                ? new BranchLine($"on {branch}", $"The branch checked out in this working directory: {branch}", false)
                : new BranchLine(string.Empty, string.Empty, false);
        }

        if (repos.Count == 1)
        {
            var only = repos[0];

            return new BranchLine($"on {Shown(only)}", $"The branch checked out in {only.Name}: {Shown(only)}", false);
        }

        var list = string.Join("\n", [Heading, .. repos.Select(repo => $"{repo.Name}: {Shown(repo)}")]);
        var first = repos[0].Branch;
        var same = first is not null
                   && !first.StartsWith("detached at ", StringComparison.Ordinal)
                   && repos.All(repo => string.Equals(repo.Branch, first, StringComparison.Ordinal));

        return same
            ? new BranchLine($"on {first} · {repos.Count} repos", list, false)
            : new BranchLine("branches differ", list, true);
    }

    private static string Shown(CardRepo repo) => repo.Branch ?? "branch not readable";
}
