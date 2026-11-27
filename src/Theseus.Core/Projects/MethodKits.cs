namespace Theseus.Core.Projects;

/// <summary>The method kit versions a project file's <c>Kit:</c> line can name.</summary>
public static class MethodKits
{
    /// <summary>The version a new project's file names.</summary>
    public const string Current = "method-v0.2";

    /// <summary>The version before project files had a readable shape: shown as pinned (AC 3).</summary>
    public const string Pinned = "method-v0.1";

    /// <summary>
    /// The versions this Theseus reads, all in the project file's <c>method-v0.2</c> shape: <c>method-v0.3</c> changed the
    /// method's rules, not the file.
    /// </summary>
    public static IReadOnlyList<string> Read { get; } = [Current, "method-v0.3"];

    /// <summary>The version as this Theseus names it, or null when it is not one it reads.</summary>
    public static string? Readable(string kit) =>
        Read.FirstOrDefault(version => string.Equals(version, kit, StringComparison.OrdinalIgnoreCase));
}
