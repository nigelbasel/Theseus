namespace Theseus.Core.Projects;

/// <summary>
/// Which projects the owner set unattended (2.4 W2; Nigel, 2026-11-26): a project is unattended only while Theseus's
/// settings name it, which only the owner's Project properties write. Folder names compare as Windows compares them.
/// </summary>
public static class UnattendedProjects
{
    /// <summary>Whether a card's project is unattended; a card in no project never is.</summary>
    public static bool IsOn(string? project, IReadOnlyList<string> unattended)
    {
        ArgumentNullException.ThrowIfNull(unattended);

        return project is not null && unattended.Contains(project, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The list with one project switched on or off, the others kept.</summary>
    public static IReadOnlyList<string> Set(IReadOnlyList<string> unattended, string project, bool on)
    {
        ArgumentNullException.ThrowIfNull(unattended);
        ArgumentException.ThrowIfNullOrWhiteSpace(project);

        var others = unattended.Where(name => !string.Equals(name, project, StringComparison.OrdinalIgnoreCase));

        return on ? [.. others, project] : [.. others];
    }
}
