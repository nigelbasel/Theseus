namespace Theseus.Core.Projects;

/// <summary>
/// Runs read-only git commands in a folder. Theseus reads projects with git, from branches, so it does not matter which
/// branch a listed folder has checked out; it never fetches (spec AC 2).
/// </summary>
public interface IGitReader
{
    /// <summary>Runs <c>git -C &lt;folder&gt; &lt;arguments&gt;</c>.</summary>
    GitOutput Run(string folder, IReadOnlyList<string> arguments);
}
