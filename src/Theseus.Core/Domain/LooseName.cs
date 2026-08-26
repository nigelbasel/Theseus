namespace Theseus.Core.Domain;

/// <summary>
/// Compares names the way a person would: "In flight" and "in-flight" are the same name, and so are
/// "Investigate Nexus MCP Server" and "investigate-nexus-mcp-server". Names here are typed by people
/// and mangled by machines on the way through - a lane renamed in settings, a session named from a
/// workstream title - so matching them exactly loses the match for a space.
/// </summary>
public static class LooseName
{
    /// <summary>Whether two names mean the same thing, ignoring case and punctuation.</summary>
    public static bool Same(string? name, string? other) =>
        name is not null
        && other is not null
        && string.Equals(Key(name), Key(other), StringComparison.OrdinalIgnoreCase)
        && Key(name).Length > 0;

    private static string Key(string name) => string.Concat(name.Where(char.IsLetterOrDigit));
}
