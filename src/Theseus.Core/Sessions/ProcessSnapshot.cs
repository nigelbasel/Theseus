using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Theseus.Core.Sessions;

/// <summary>
/// Captures every process at one instant. One snapshot answers both "who is my parent" and "what is
/// that process called", which is the whole of what walking a tab's ancestry needs.
/// </summary>
[SupportedOSPlatform("windows")]
public static class ProcessSnapshot
{
    private const uint Snapprocess = 0x00000002;
    private const int MaxPath = 260;

    /// <summary>Takes a snapshot, returning an empty list if the operating system refuses.</summary>
    public static IReadOnlyList<ProcessSnapshotEntry> Capture()
    {
        var entries = new List<ProcessSnapshotEntry>();
        var snapshot = CreateToolhelp32Snapshot(Snapprocess, 0);

        if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1))
        {
            return entries;
        }

        try
        {
            var entry = new Processentry32 { DwSize = Marshal.SizeOf<Processentry32>() };

            if (!Process32First(snapshot, ref entry))
            {
                return entries;
            }

            do
            {
                entries.Add(new ProcessSnapshotEntry(
                    entry.Th32ProcessID,
                    entry.Th32ParentProcessID,
                    Path.GetFileNameWithoutExtension(entry.SzExeFile) ?? string.Empty));
            }
            while (Process32Next(snapshot, ref entry));
        }
        finally
        {
            CloseHandle(snapshot);
        }

        return entries;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "Process32FirstW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32First(IntPtr snapshot, ref Processentry32 entry);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "Process32NextW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32Next(IntPtr snapshot, ref Processentry32 entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Processentry32
    {
        public int DwSize;
        public int CntUsage;
        public int Th32ProcessID;
        public IntPtr Th32DefaultHeapID;
        public int Th32ModuleID;
        public int CntThreads;
        public int Th32ParentProcessID;
        public int PcPriClassBase;
        public int DwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxPath)]
        public string SzExeFile;
    }
}
