using System.Diagnostics;
using Theseus.Core.Diagnostics;
using Theseus.Core.Sessions;

namespace Theseus.Core.Locks;

/// <summary>
/// Clears up after a test run whose wrapper died without releasing (Nigel, 2026-10-06): its test hosts,
/// and the Testcontainers containers started while it held the lock. Only what can be proved to be that
/// run's is touched: a test host must still have the start time recorded for it, and a container must
/// carry the Testcontainers label and have been created inside the hold, when the lock kept every other
/// container-backed run out.
/// </summary>
public sealed class OrphanCleaner : IOrphanCleaner
{
    private readonly Func<int, string?> _startOf;
    private readonly Action<int> _kill;
    private readonly Func<IReadOnlyList<(string Id, DateTimeOffset CreatedUtc)>> _containers;
    private readonly Action<string> _remove;

    /// <summary>Creates a cleaner over seams for processes and containers.</summary>
    public OrphanCleaner(
        Func<int, string?> startOf,
        Action<int> kill,
        Func<IReadOnlyList<(string Id, DateTimeOffset CreatedUtc)>> containers,
        Action<string> remove)
    {
        _startOf = startOf;
        _kill = kill;
        _containers = containers;
        _remove = remove;
    }

    /// <summary>A cleaner that acts on this machine's processes and Docker.</summary>
    public static OrphanCleaner ForThisMachine(IProcessProbe probe, ITestContainers containers) =>
        new(probe.GetStartFileTime,
            KillTree,
            () => [.. containers.List().Select(container => (container.Id, container.CreatedUtc))],
            containers.Remove);

    /// <inheritdoc />
    public string? Clean(IReadOnlyList<HeldProcess> testHosts, DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        var ended = new List<string>();

        foreach (var host in testHosts.Where(host => _startOf(host.ProcessId) == host.Start))
        {
            try
            {
                _kill(host.ProcessId);
                ended.Add($"{host.Name} {host.ProcessId}");
                TheseusLog.Warning("Ended test host {Name} {Pid}, left running by a lock holder that died", host.Name, host.ProcessId);
            }
            catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or ArgumentException)
            {
                TheseusLog.Warning(exception, "Could not end test host {Name} {Pid}", host.Name, host.ProcessId);
            }
        }

        var removed = new List<string>();
        IReadOnlyList<(string Id, DateTimeOffset CreatedUtc)> containers;

        try
        {
            containers = _containers();
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or TimeoutException)
        {
            TheseusLog.Warning(exception, "Could not list Testcontainers containers, so none were removed");
            containers = [];
        }

        foreach (var (id, created) in containers.Where(container => container.CreatedUtc >= fromUtc && container.CreatedUtc <= toUtc))
        {
            try
            {
                _remove(id);
                removed.Add(id);
                TheseusLog.Warning("Removed container {Id}, left running by a lock holder that died", id);
            }
            catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or TimeoutException)
            {
                TheseusLog.Warning(exception, "Could not remove container {Id}", id);
            }
        }

        if (ended.Count == 0 && removed.Count == 0)
        {
            return null;
        }

        return $"Cleaned up after it: {ended.Count} test host(s){(ended.Count > 0 ? $" ({string.Join(", ", ended)})" : string.Empty)}, "
               + $"{removed.Count} container(s){(removed.Count > 0 ? $" ({string.Join(", ", removed)})" : string.Empty)}.";
    }

    private static void KillTree(int processId)
    {
        using var process = Process.GetProcessById(processId);
        process.Kill(entireProcessTree: true);
    }
}
