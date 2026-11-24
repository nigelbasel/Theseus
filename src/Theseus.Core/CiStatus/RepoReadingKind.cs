namespace Theseus.Core.CiStatus;

/// <summary>What the last attempt to read a repository from GitHub came to.</summary>
public enum RepoReadingKind
{
    /// <summary>Both lists were read.</summary>
    Read,

    /// <summary>GitHub's limit for calls without a token was used up.</summary>
    Limited,

    /// <summary>GitHub answered 404: the repository is private or missing, which without a token look the same.</summary>
    NotShown,

    /// <summary>GitHub could not be reached, or answered with something else.</summary>
    Failed,
}
