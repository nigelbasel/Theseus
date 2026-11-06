namespace Theseus.Core.Resources;

/// <summary>
/// One reading of the machine: what each process used since the reading before, and what any
/// session's process tree adds up to.
/// </summary>
public sealed class ResourceReading
{
    private const int BusiestShown = 5;

    private readonly IReadOnlyDictionary<int, ProcessCounters> _processes;
    private readonly IReadOnlyDictionary<int, List<ProcessCounters>> _children;
    private readonly IReadOnlyDictionary<int, (double Cpu, double Io)> _rates;

    internal ResourceReading(
        MachineResources machine,
        bool hasRates,
        IReadOnlyDictionary<int, ProcessCounters> processes,
        IReadOnlyDictionary<int, List<ProcessCounters>> children,
        IReadOnlyDictionary<int, (double Cpu, double Io)> rates)
    {
        Machine = machine;
        HasRates = hasRates;
        _processes = processes;
        _children = children;
        _rates = rates;
    }

    /// <summary>A reading with nothing in it, shown before the first one is taken.</summary>
    public static ResourceReading None { get; } = new(
        new MachineResources(0, 0, 0, 0), false,
        new Dictionary<int, ProcessCounters>(), new Dictionary<int, List<ProcessCounters>>(),
        new Dictionary<int, (double, double)>());

    /// <summary>The whole machine.</summary>
    public MachineResources Machine { get; }

    /// <summary>
    /// Whether rates are known. The first reading has nothing to compare with, so it can only say what
    /// exists and how much memory it holds.
    /// </summary>
    public bool HasRates { get; }

    /// <summary>What a session's tree is using, or nothing when its root process is not running.</summary>
    public SessionResources? Session(int? rootProcessId)
    {
        if (rootProcessId is not { } root || !_processes.ContainsKey(root))
        {
            return null;
        }

        var tree = Tree(root);
        var busiest = tree
            .GroupBy(process => process.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ProcessShare(
                group.Count() == 1 ? group.Key : $"{group.Key} ×{group.Count()}",
                group.Sum(process => Rate(process).Cpu),
                group.Sum(process => process.PrivateBytes)))
            .OrderByDescending(share => share.CpuPercent)
            .ThenByDescending(share => share.PrivateBytes)
            .Take(BusiestShown)
            .ToList();

        return new SessionResources(
            tree.Count,
            tree.Sum(process => Rate(process).Cpu),
            tree.Sum(process => process.PrivateBytes),
            tree.Sum(process => Rate(process).Io),
            busiest);
    }

    /// <summary>
    /// The root and everything descended from it. A child must have started no earlier than its
    /// parent: a parent id outlives the parent, and Windows reuses the number, so a process can
    /// name as its parent an unrelated later process with the same id.
    /// </summary>
    internal List<ProcessCounters> Tree(int root)
    {
        var tree = new List<ProcessCounters>();
        var seen = new HashSet<int>();
        var pending = new Stack<ProcessCounters>();

        pending.Push(_processes[root]);

        while (pending.TryPop(out var process))
        {
            if (!seen.Add(process.ProcessId))
            {
                continue;
            }

            tree.Add(process);

            if (!_children.TryGetValue(process.ProcessId, out var children))
            {
                continue;
            }

            foreach (var child in children.Where(child => child.CreateTime >= process.CreateTime))
            {
                pending.Push(child);
            }
        }

        return tree;
    }

    private (double Cpu, double Io) Rate(ProcessCounters process) =>
        _rates.TryGetValue(process.ProcessId, out var rate) ? rate : (0, 0);
}
