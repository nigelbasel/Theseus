namespace Theseus.Core.Resources;

/// <summary>
/// Turns process tables taken a few seconds apart into rates: how much processor time and how many
/// I/O requests each process used between the two tables.
/// </summary>
public sealed class ResourceSampler
{
    /// <summary>The processes that are Microsoft Defender's scanning engine.</summary>
    public static readonly IReadOnlySet<string> Defender =
        new HashSet<string>(["MsMpEng", "MpDefenderCoreService"], StringComparer.OrdinalIgnoreCase);

    /// <summary>The processes that are the virtual machine Docker Desktop's containers run in.</summary>
    public static readonly IReadOnlySet<string> Docker =
        new HashSet<string>(["vmmemWSL", "vmmem"], StringComparer.OrdinalIgnoreCase);

    private readonly int _processors;
    private ProcessTable? _previous;
    private Dictionary<(int, long), ProcessCounters> _previousByKey = [];

    /// <summary>Creates a sampler for a machine with the given number of logical processors.</summary>
    public ResourceSampler(int processors) => _processors = Math.Max(1, processors);

    /// <summary>
    /// Reads a new table against the last one. <paramref name="sessionRoots"/> are the Claude Code
    /// processes of the running sessions, needed for the total the sessions use together.
    /// </summary>
    public ResourceReading Take(ProcessTable table, IEnumerable<int> sessionRoots)
    {
        var previous = _previous;
        var wall = previous is null ? 0 : (table.CapturedUtc - previous.CapturedUtc).Ticks;
        var hasRates = wall > 0;
        var since = previous?.CapturedUtc.ToFileTime() ?? long.MaxValue;
        var rates = new Dictionary<int, (double Cpu, double Io)>();
        var processes = new Dictionary<int, ProcessCounters>();
        var children = new Dictionary<int, List<ProcessCounters>>();

        foreach (var process in table.Processes)
        {
            processes[process.ProcessId] = process;

            if (process.ProcessId != process.ParentProcessId)
            {
                if (!children.TryGetValue(process.ParentProcessId, out var siblings))
                {
                    siblings = [];
                    children[process.ParentProcessId] = siblings;
                }

                siblings.Add(process);
            }

            if (hasRates)
            {
                rates[process.ProcessId] = RateOf(process, since, wall);
            }
        }

        var reading = new ResourceReading(
            new MachineResources(0, 0, 0, 0), hasRates, processes, children, rates);
        var machine = hasRates
            ? new MachineResources(
                Busy(previous!, table),
                Share(table, rates, Defender),
                Share(table, rates, Docker),
                Sessions(reading, sessionRoots, rates))
            : reading.Machine;

        _previous = table;
        _previousByKey = table.Processes
            .GroupBy(process => (process.ProcessId, process.CreateTime))
            .ToDictionary(group => group.Key, group => group.First());

        return new ResourceReading(machine, hasRates, processes, children, rates);
    }

    private (double Cpu, double Io) RateOf(ProcessCounters process, long since, long wall)
    {
        long cpu;
        long io;

        if (_previousByKey.TryGetValue((process.ProcessId, process.CreateTime), out var before))
        {
            cpu = Math.Max(0, process.CpuTicks - before.CpuTicks);
            io = Math.Max(0, process.IoOperations - before.IoOperations);
        }
        else if (process.CreateTime > since)
        {
            cpu = process.CpuTicks;
            io = process.IoOperations;
        }
        else
        {
            return (0, 0);
        }

        return (100.0 * cpu / ((double)wall * _processors), io / TimeSpan.FromTicks(wall).TotalSeconds);
    }

    private static double Busy(ProcessTable before, ProcessTable now)
    {
        var total = now.BusyAndIdleTicks - before.BusyAndIdleTicks;

        return total <= 0 ? 0 : Math.Clamp(100.0 * (1 - (double)(now.IdleTicks - before.IdleTicks) / total), 0, 100);
    }

    private static double Share(ProcessTable table, Dictionary<int, (double Cpu, double Io)> rates, IReadOnlySet<string> names) =>
        table.Processes
            .Where(process => names.Contains(process.Name))
            .Sum(process => rates.TryGetValue(process.ProcessId, out var rate) ? rate.Cpu : 0);

    private static double Sessions(
        ResourceReading reading, IEnumerable<int> roots, Dictionary<int, (double Cpu, double Io)> rates) =>
        roots
            .Distinct()
            .Where(root => reading.Session(root) is not null)
            .SelectMany(reading.Tree)
            .DistinctBy(process => process.ProcessId)
            .Sum(process => rates.TryGetValue(process.ProcessId, out var rate) ? rate.Cpu : 0);
}
