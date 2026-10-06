namespace Theseus.Core.Locks;

/// <summary>
/// The locks Theseus keeps, each guarding one thing sessions on this machine share (Nigel, 2026-10-06).
/// A name outside this list is refused rather than created, so a typo cannot open a lock nobody else
/// waits on.
/// </summary>
public static class LockNames
{
    /// <summary>Container-backed test suites, compose builds and whole-solution builds, as one lock.</summary>
    public const string Tests = "tests";

    /// <summary>The shared local NDC/AFE docker stack.</summary>
    public const string DevStack = "dev-stack";

    /// <summary>The local llama.cpp server on port 8090, which holds one model at a time.</summary>
    public const string Llama = "llama";

    /// <summary>Every lock, in the order the board shows them.</summary>
    public static readonly IReadOnlyList<string> All = [Tests, DevStack, Llama];

    /// <summary>What each lock guards, in words for the board and for sessions.</summary>
    public static string Describe(string name) => name switch
    {
        Tests => "Container-backed test suites, compose builds and whole-solution builds",
        DevStack => "The local NDC/AFE docker stack",
        Llama => "The local llama server (port 8090)",
        _ => name,
    };

    /// <summary>The known name matching <paramref name="name"/>, ignoring case, or null.</summary>
    public static string? Find(string? name) =>
        All.FirstOrDefault(known => string.Equals(known, name?.Trim(), StringComparison.OrdinalIgnoreCase));
}
