using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Theseus.Core.Sessions;

/// <summary>
/// Reads another process's command line, which is the only way to tell a <c>dotnet</c> running a test
/// host from a <c>dotnet</c> running a shared build server.
/// </summary>
[SupportedOSPlatform("windows")]
public static class ProcessCommandLine
{
    private const uint QueryLimitedInformation = 0x1000;
    private const int CommandLineInformation = 60;
    private const int InfoLengthMismatch = unchecked((int)0xC0000004);

    /// <summary>The process's command line, or null when it cannot be read.</summary>
    public static string? Read(int processId)
    {
        var handle = OpenProcess(QueryLimitedInformation, false, processId);

        if (handle == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var status = NtQueryInformationProcess(handle, CommandLineInformation, IntPtr.Zero, 0, out var length);

            if (status != InfoLengthMismatch || length <= 0)
            {
                return null;
            }

            var buffer = Marshal.AllocHGlobal(length);

            try
            {
                if (NtQueryInformationProcess(handle, CommandLineInformation, buffer, length, out _) != 0)
                {
                    return null;
                }

                var text = Marshal.PtrToStructure<UnicodeString>(buffer);

                return text.Buffer == IntPtr.Zero ? null : Marshal.PtrToStringUni(text.Buffer, text.Length / 2);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(IntPtr handle, int informationClass, IntPtr information, int length, out int returnLength);

    [StructLayout(LayoutKind.Sequential)]
    private struct UnicodeString
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }
}
