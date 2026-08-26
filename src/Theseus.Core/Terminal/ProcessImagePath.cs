using System.Runtime.InteropServices;
using System.Text;

namespace Theseus.Core.Terminal;

/// <summary>
/// Reads another process's executable path.
///
/// <see cref="System.Diagnostics.Process.MainModule"/> is the obvious way and the wrong one: it
/// enumerates the target's loaded modules, which needs rights a normal application does not have
/// over a packaged or elevated process. Windows Terminal is both, so the obvious way returns
/// "Access is denied" for the very processes this needs to see. Asking the kernel for the image
/// name needs only limited-information rights and succeeds where reading modules does not.
/// </summary>
public static class ProcessImagePath
{
    private const int QueryLimitedInformation = 0x1000;

    /// <summary>The full path of a running process's executable, or null when it cannot be read.</summary>
    public static string? For(int processId)
    {
        var handle = OpenProcess(QueryLimitedInformation, false, processId);

        if (handle == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var buffer = new StringBuilder(1024);
            var size = buffer.Capacity;

            return QueryFullProcessImageName(handle, 0, buffer, ref size)
                ? buffer.ToString(0, size)
                : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(int desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder name, ref int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
