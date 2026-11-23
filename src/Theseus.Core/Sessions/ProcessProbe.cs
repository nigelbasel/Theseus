using System.Diagnostics;
using Theseus.Core.Diagnostics;

namespace Theseus.Core.Sessions;

/// <inheritdoc />
public sealed class ProcessProbe : IProcessProbe
{
    // Shared by every probe: the app makes several, and each would otherwise say the same process is gone.
    private static readonly OnceEachProcess Told = new();

    /// <inheritdoc />
    public string? GetStartFileTime(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);

            // An exited process someone still holds open keeps its id; it is as gone as one nobody holds.
            if (process.HasExited)
            {
                Gone(pid, "it has exited, though a handle to it is still open");

                return null;
            }

            return process.StartTime.ToFileTimeUtc().ToString();
        }
        catch (ArgumentException)
        {
            Gone(pid, "it is gone");

            return null;
        }
        catch (InvalidOperationException exception)
        {
            Gone(pid, exception.Message);

            return null;
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            if (Told.First(pid))
            {
                TheseusLog.Debug(
                    "Process {Pid} cannot be opened ({Reason}), so it is not a session of this user's and its registry entry is a corpse",
                    pid, exception.Message);
            }

            return null;
        }
    }

    private static void Gone(int pid, string reason)
    {
        if (Told.First(pid))
        {
            TheseusLog.Debug("Process {Pid} is not running ({Reason}), so its registry entry is a corpse", pid, reason);
        }
    }
}
