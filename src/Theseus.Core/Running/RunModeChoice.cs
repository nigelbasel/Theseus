using Theseus.Core.Domain;

namespace Theseus.Core.Running;

/// <summary>
/// The mode a card runs in: its own, else its project's default, else headless for a verifier or scenario writer, whose
/// work nobody watches, else a tab (Nigel, 2026-11-24).
/// </summary>
public static class RunModeChoice
{
    /// <summary>The card's mode.</summary>
    public static RunMode For(Workstream workstream, IReadOnlyDictionary<string, RunMode> projectModes)
    {
        ArgumentNullException.ThrowIfNull(workstream);
        ArgumentNullException.ThrowIfNull(projectModes);

        if (workstream.RunMode is { } own)
        {
            return own;
        }

        if (workstream.Project is { } project && projectModes.TryGetValue(project, out var projectMode))
        {
            return projectMode;
        }

        return workstream.Role is WorkstreamRole.Verifier or WorkstreamRole.ScenarioWriter ? RunMode.Headless : RunMode.Tab;
    }
}
