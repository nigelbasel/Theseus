using System.Runtime.Versioning;
using Theseus.Core.Sessions;

namespace Theseus.Core.Locks;

/// <inheritdoc />
[SupportedOSPlatform("windows")]
public sealed class WindowsProcessTree : IProcessTree
{
    private readonly IProcessProbe _probe;

    /// <summary>Creates a tree reader that dates each process with the probe.</summary>
    public WindowsProcessTree(IProcessProbe probe) => _probe = probe;

    /// <inheritdoc />
    public IReadOnlyList<HeldProcess> TestHostsUnder(int processId)
    {
        if (!long.TryParse(_probe.GetStartFileTime(processId), out var holderStart))
        {
            return [];
        }

        var snapshot = ProcessSnapshot.Capture();
        var children = snapshot.ToLookup(entry => entry.ParentProcessId);
        var found = new List<HeldProcess>();
        var visited = new HashSet<int> { processId };
        var pending = new Stack<int>([processId]);

        while (pending.Count > 0)
        {
            foreach (var child in children[pending.Pop()])
            {
                if (!visited.Add(child.ProcessId))
                {
                    continue;
                }

                pending.Push(child.ProcessId);

                if (TestHostRecogniser.IsTestHost(child.Name, ProcessCommandLine.Read(child.ProcessId))
                    && _probe.GetStartFileTime(child.ProcessId) is { } start
                    && long.TryParse(start, out var childStart)
                    && childStart >= holderStart)
                {
                    found.Add(new HeldProcess(child.ProcessId, start, child.Name));
                }
            }
        }

        return found;
    }
}
