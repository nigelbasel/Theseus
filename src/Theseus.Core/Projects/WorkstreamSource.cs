namespace Theseus.Core.Projects;

/// <summary>Where a slug's work was read (spec AC 5): the branch and commit, or nowhere on this machine.</summary>
/// <param name="Branch">The branch read, or null when neither the workstream's branch nor the primary has its work.</param>
/// <param name="Commit">That branch's commit when read.</param>
/// <param name="IsMerged">Whether it was read from the primary because the work is merged there.</param>
public sealed record WorkstreamSource(string? Branch, string? Commit, bool IsMerged)
{
    /// <summary>Nothing of the slug's work is on this machine.</summary>
    public static WorkstreamSource NotOnMachine { get; } = new(null, null, false);

    /// <summary>Whether its work was found.</summary>
    public bool IsOnMachine => Branch is not null;

    /// <summary>For the card's tooltip: the branch and the commit, short.</summary>
    public string Describe => Branch is null
        ? "not on this machine"
        : $"read from {Branch} at {(Commit is { Length: >= 7 } commit ? commit[..7] : Commit ?? "an unknown commit")}"
          + (IsMerged ? ", where it is merged" : string.Empty);
}
