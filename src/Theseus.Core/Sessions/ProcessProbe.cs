using System.Diagnostics;
using Theseus.Core.Diagnostics;

namespace Theseus.Core.Sessions;

/// <inheritdoc />
public sealed class ProcessProbe : IProcessProbe
{
    /// <inheritdoc />
    public string? GetStartFileTime(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return process.StartTime.ToFileTimeUtc().ToString();
        }
        catch (ArgumentException)
        {
            TheseusLog.Debug("Process {Pid} is gone, so its registry entry is a corpse", pid);

            return null;
        }
        catch (InvalidOperationException exception)
        {
            TheseusLog.Debug("Could not read the start time of process {Pid}: {Reason}", pid, exception.Message);

            return null;
        }
    }
}
