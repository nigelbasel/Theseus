using System.Globalization;
using System.Text;

namespace Theseus.Core.Resources;

/// <summary>Writes resource readings at the size a person scans them.</summary>
public static class ResourceText
{
    /// <summary>
    /// The card's line, for example "CPU 12% · 1.4 GB · 230 I/O/s · 2 agents". The first reading has no
    /// rates yet, so it shows only what can be counted.
    /// </summary>
    public static string Card(SessionResources session, bool hasRates, int agents)
    {
        var parts = new List<string>();

        if (hasRates)
        {
            parts.Add($"CPU {Percent(session.CpuPercent)}");
        }

        parts.Add(Bytes(session.PrivateBytes));

        if (hasRates)
        {
            parts.Add($"{Count(session.IoPerSecond)} I/O/s");
        }

        if (agents > 0)
        {
            parts.Add(agents == 1 ? "1 agent" : $"{agents} agents");
        }

        return string.Join(" · ", parts);
    }

    /// <summary>The card's tooltip: the processes behind the line, busiest first.</summary>
    public static string Tooltip(SessionResources session, bool hasRates)
    {
        var text = new StringBuilder();

        text.Append(CultureInfo.InvariantCulture,
            $"{session.ProcessCount} process{(session.ProcessCount == 1 ? string.Empty : "es")} in this session's tree:");

        foreach (var share in session.Busiest)
        {
            text.AppendLine();
            text.Append(hasRates
                ? $"{share.Name}  CPU {Percent(share.CpuPercent)}  {Bytes(share.PrivateBytes)}"
                : $"{share.Name}  {Bytes(share.PrivateBytes)}");
        }

        text.AppendLine();
        text.Append("CPU is a share of the whole machine, as Task Manager counts it. Containers run in Docker's own virtual machine, so they show in the status bar, not here.");

        return text.ToString();
    }

    /// <summary>The status bar's line, for example "CPU 48% · sessions 30% · Defender 6% · Docker 12%".</summary>
    public static string Machine(MachineResources machine, bool hasRates) => hasRates
        ? $"CPU {Percent(machine.CpuPercent)} · sessions {Percent(machine.SessionsPercent)} · Defender {Percent(machine.DefenderPercent)} · Docker {Percent(machine.DockerPercent)}"
        : string.Empty;

    /// <summary>A percentage, keeping a small but non-zero share visible.</summary>
    public static string Percent(double percent) => percent switch
    {
        <= 0 => "0%",
        < 1 => "<1%",
        _ => string.Create(CultureInfo.InvariantCulture, $"{percent:0}%"),
    };

    /// <summary>Memory in the unit that keeps it short: 1.4 GB, 350 MB.</summary>
    public static string Bytes(long bytes) => bytes >= 1L << 30
        ? string.Create(CultureInfo.InvariantCulture, $"{bytes / (double)(1L << 30):0.0} GB")
        : string.Create(CultureInfo.InvariantCulture, $"{bytes / (double)(1L << 20):0} MB");

    private static string Count(double value) => value >= 1000
        ? string.Create(CultureInfo.InvariantCulture, $"{value / 1000:0.#}k")
        : string.Create(CultureInfo.InvariantCulture, $"{value:0}");
}
