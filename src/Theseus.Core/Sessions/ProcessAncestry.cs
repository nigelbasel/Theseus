namespace Theseus.Core.Sessions;

/// <summary>
/// Walks a process's ancestors within one snapshot. A Claude Code session's tab is not its own
/// process: the chain runs claude → the launcher shim or shell → <c>WindowsTerminal.exe</c>, and
/// only the last of those owns a window that can be brought to the front.
/// </summary>
public static class ProcessAncestry
{
    /// <summary>The process name that hosts Windows Terminal's windows.</summary>
    public const string TerminalProcessName = "WindowsTerminal";

    /// <summary>
    /// Walks up from <paramref name="processId"/> looking for an ancestor with the given name.
    /// </summary>
    /// <param name="snapshot">Every process, captured at one instant.</param>
    /// <param name="processId">Where to start.</param>
    /// <param name="ancestorName">The executable name to stop at.</param>
    /// <param name="maximumDepth">
    /// How far to walk. A bound is required, not defensive: parent ids are reused, so a stale chain
    /// can genuinely form a cycle.
    /// </param>
    public static int? FindAncestor(
        IReadOnlyList<ProcessSnapshotEntry> snapshot,
        int processId,
        string ancestorName,
        int maximumDepth = 8)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var byId = snapshot.ToDictionary(entry => entry.ProcessId);
        var seen = new HashSet<int> { processId };
        var current = processId;

        for (var depth = 0; depth < maximumDepth; depth++)
        {
            if (!byId.TryGetValue(current, out var entry) || entry.ParentProcessId <= 0)
            {
                return null;
            }

            if (!byId.TryGetValue(entry.ParentProcessId, out var parent) || !seen.Add(parent.ProcessId))
            {
                return null;
            }

            if (string.Equals(parent.Name, ancestorName, StringComparison.OrdinalIgnoreCase))
            {
                return parent.ProcessId;
            }

            current = parent.ProcessId;
        }

        return null;
    }

    /// <summary>Finds the terminal hosting a session's tab.</summary>
    public static int? TerminalOf(IReadOnlyList<ProcessSnapshotEntry> snapshot, int processId) =>
        FindAncestor(snapshot, processId, TerminalProcessName);
}
