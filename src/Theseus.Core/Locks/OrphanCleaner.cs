using System.Diagnostics;
using System.Globalization;
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
    private static readonly TimeSpan DockerTimeout = TimeSpan.FromSeconds(30);

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
    public static OrphanCleaner ForThisMachine(IProcessProbe probe) =>
        new(probe.GetStartFileTime, KillTree, ListTestContainers, RemoveContainer);

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

    /// <summary>Reads the creation time Docker prints, such as <c>2026-10-06 11:21:51 +0200 CEST</c>.</summary>
    public static DateTimeOffset? ParseDockerCreated(string text)
    {
        var parts = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 3
            || !DateTime.TryParseExact($"{parts[0]} {parts[1]}", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)
            || parts[2].Length != 5
            || !int.TryParse(parts[2].AsSpan(1, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var hours)
            || !int.TryParse(parts[2].AsSpan(3, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var minutes))
        {
            return null;
        }

        var offset = new TimeSpan(hours, minutes, 0) * (parts[2][0] == '-' ? -1 : 1);

        return new DateTimeOffset(local, offset).ToUniversalTime();
    }

    private static void KillTree(int processId)
    {
        using var process = Process.GetProcessById(processId);
        process.Kill(entireProcessTree: true);
    }

    private static IReadOnlyList<(string Id, DateTimeOffset CreatedUtc)> ListTestContainers()
    {
        var output = Docker("ps", "-a", "--filter", "label=org.testcontainers", "--format", "{{.ID}}|{{.CreatedAt}}");
        var containers = new List<(string Id, DateTimeOffset CreatedUtc)>();

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var bar = line.IndexOf('|');

            if (bar > 0 && ParseDockerCreated(line[(bar + 1)..]) is { } created)
            {
                containers.Add((line[..bar], created));
            }
        }

        return containers;
    }

    private static void RemoveContainer(string id) => Docker("rm", "-f", id);

    private static string Docker(params string[] arguments)
    {
        var start = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("docker could not be started.");
        var output = process.StandardOutput.ReadToEndAsync();

        if (!process.WaitForExit(DockerTimeout))
        {
            process.Kill();
            throw new TimeoutException($"docker {string.Join(' ', arguments)} did not finish in {DockerTimeout.TotalSeconds}s.");
        }

        return output.Result;
    }
}
