namespace Theseus.Core.Guards;

/// <summary>A guard Theseus ships and installs from Options, Guards: its files, the tools it must run before, and its check.</summary>
/// <param name="Name">What the owner calls it, in lower case, such as "trunk guard".</param>
/// <param name="Explanation">What it does, in a sentence or two for its row in Options.</param>
/// <param name="Guard">The guard itself, the file the hook command runs.</param>
/// <param name="Companions">Files installed beside it that it needs, such as the lock script the tests-lock guard sends sessions to.</param>
/// <param name="Tools">The tools it must run before; empty for every tool.</param>
/// <param name="Prove">Runs an installed guard at a path on a fixture and says whether it works.</param>
public sealed record GuardDefinition(
    string Name,
    string Explanation,
    GuardFile Guard,
    IReadOnlyList<GuardFile> Companions,
    IReadOnlyList<string> Tools,
    Func<string, GuardOutcome> Prove)
{
    /// <summary>Its name as a label starts, such as "Trunk guard".</summary>
    public string Title => char.ToUpperInvariant(Name[0]) + Name[1..];

    /// <summary>The hook entry's matcher: every tool, or the tools it runs before.</summary>
    public string Matcher => Tools.Count == 0 ? "*" : string.Join('|', Tools);

    /// <summary>The guard and its companions.</summary>
    public IReadOnlyList<GuardFile> Files => [Guard, .. Companions];
}
