using System.Diagnostics;
using System.Globalization;

namespace Theseus.Core.Locks;

/// <summary>
/// Testcontainers containers through the <c>docker</c> command line. Every call is given 30 seconds,
/// because under load <c>docker ps</c> has taken minutes on this machine.
/// </summary>
public sealed class DockerTestContainers : ITestContainers
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public IReadOnlyList<TestContainer> List()
    {
        var output = Docker("ps", "-a", "--filter", "label=org.testcontainers", "--format", "{{.ID}}|{{.CreatedAt}}|{{.Names}}|{{.Image}}|{{.Status}}");
        var containers = new List<TestContainer>();

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split('|');

            if (parts.Length >= 5 && ParseCreated(parts[1]) is { } created)
            {
                containers.Add(new TestContainer(parts[0], parts[2], parts[3], created, parts[4]));
            }
        }

        return containers;
    }

    /// <inheritdoc />
    public void Remove(string id) => Docker("rm", "-f", id);

    /// <summary>Reads the creation time Docker prints, such as <c>2026-10-06 11:21:51 +0200 CEST</c>.</summary>
    public static DateTimeOffset? ParseCreated(string text)
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

        if (!process.WaitForExit(Timeout))
        {
            process.Kill();
            throw new TimeoutException($"docker {string.Join(' ', arguments)} did not finish in {Timeout.TotalSeconds}s.");
        }

        return output.Result;
    }
}
