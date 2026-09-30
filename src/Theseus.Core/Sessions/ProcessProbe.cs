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
        catch (System.ComponentModel.Win32Exception exception)
        {
            TheseusLog.Debug(
                "Process {Pid} cannot be opened ({Reason}), so it is not a session of this user's and its registry entry is a corpse",
                pid, exception.Message);

            return null;
        }
    }
}
