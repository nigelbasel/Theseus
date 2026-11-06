using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Theseus.Core.Diagnostics;

namespace Theseus.Core.Resources;

/// <summary>
/// Reads every process's processor time, memory and I/O in one call, as Task Manager does.
///
/// Opening each process in turn would not work for the two processes that matter most here:
/// Defender's engine, and the virtual machine Docker's containers run in. Both belong to the system,
/// and an unelevated process cannot open either (measured 2026-11-06: error 5 for both). This call
/// needs no process handle, so it sees them. It also costs one system call for the whole machine
/// instead of one per process.
/// </summary>
[SupportedOSPlatform("windows")]
public static class SystemProcessTable
{
    private const int SystemProcessInformation = 5;
    private const int InfoLengthMismatch = unchecked((int)0xC0000004);

    private const int NextEntryOffset = 0;
    private const int WorkingSetPrivateSize = 8;
    private const int CreateTimeOffset = 32;
    private const int UserTime = 40;
    private const int KernelTime = 48;
    private const int ImageNameLength = 56;
    private const int ImageNameBuffer = 64;
    private const int UniqueProcessId = 80;
    private const int InheritedFromUniqueProcessId = 88;
    private const int ReadOperationCount = 208;
    private const int WriteOperationCount = 216;
    private const int OtherOperationCount = 224;

    private static int _bufferSize = 1 << 20;

    /// <summary>Takes the table, or returns an empty one if the operating system refuses.</summary>
    public static ProcessTable Capture()
    {
        var now = DateTimeOffset.UtcNow;

        if (IntPtr.Size != 8)
        {
            return ProcessTable.Empty(now);
        }

        var (idle, total) = ProcessorTimes();
        var buffer = IntPtr.Zero;

        try
        {
            while (true)
            {
                buffer = Marshal.AllocHGlobal(_bufferSize);
                var status = NtQuerySystemInformation(SystemProcessInformation, buffer, _bufferSize, out var needed);

                if (status == InfoLengthMismatch)
                {
                    Marshal.FreeHGlobal(buffer);
                    buffer = IntPtr.Zero;
                    _bufferSize = Math.Max(_bufferSize * 2, needed + (1 << 16));

                    continue;
                }

                if (status != 0)
                {
                    TheseusLog.Debug("Reading the process table failed with status 0x{Status:X8}", status);

                    return ProcessTable.Empty(now);
                }

                return new ProcessTable(now, Walk(buffer), idle, total);
            }
        }
        finally
        {
            if (buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }

    private static List<ProcessCounters> Walk(IntPtr buffer)
    {
        var processes = new List<ProcessCounters>();
        var offset = 0;

        while (true)
        {
            var entry = buffer + offset;
            var nameLength = (ushort)Marshal.ReadInt16(entry, ImageNameLength);
            var nameBuffer = Marshal.ReadIntPtr(entry, ImageNameBuffer);
            var image = nameBuffer == IntPtr.Zero ? "Idle" : Marshal.PtrToStringUni(nameBuffer, nameLength / 2);

            processes.Add(new ProcessCounters(
                (int)Marshal.ReadInt64(entry, UniqueProcessId),
                (int)Marshal.ReadInt64(entry, InheritedFromUniqueProcessId),
                Path.GetFileNameWithoutExtension(image),
                Marshal.ReadInt64(entry, CreateTimeOffset),
                Marshal.ReadInt64(entry, UserTime) + Marshal.ReadInt64(entry, KernelTime),
                Marshal.ReadInt64(entry, WorkingSetPrivateSize),
                Marshal.ReadInt64(entry, ReadOperationCount)
                + Marshal.ReadInt64(entry, WriteOperationCount)
                + Marshal.ReadInt64(entry, OtherOperationCount)));

            var next = Marshal.ReadInt32(entry, NextEntryOffset);

            if (next == 0)
            {
                return processes;
            }

            offset += next;
        }
    }

    private static (long Idle, long Total) ProcessorTimes() =>
        GetSystemTimes(out var idle, out var kernel, out var user) ? (idle, kernel + user) : (0, 0);

    [DllImport("ntdll.dll")]
    private static extern int NtQuerySystemInformation(int informationClass, IntPtr buffer, int length, out int needed);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out long idle, out long kernel, out long user);
}
