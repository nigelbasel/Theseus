using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Theseus.Core.Diagnostics;

namespace Theseus.Core.Resources;

/// <summary>
/// Core parking and per-processor busy from Windows' performance counters, through PDH, without admin (parked-cores,
/// 2026-11-19). One query is kept open: opening it costs about 0.4 s, each read under 1 ms. The busy counters are rates,
/// so the first read after opening has nothing to give. A failure is logged once and gives no reading from then on.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class PdhCoreCounters : ICoreCounters
{
    private const uint FormatDouble = 0x00000200;
    private const uint FormatLarge = 0x00000400;
    private const uint ValidData = 0;
    private const uint NewData = 1;

    private readonly object _gate = new();
    private IntPtr _query;
    private IntPtr _parking;
    private IntPtr _busy;
    private IntPtr _clock;
    private bool _primed;
    private bool _broken;

    /// <inheritdoc />
    public CoreSample? Read()
    {
        // The board reads on a worker thread and closes the query on its own when it shuts down; one at a time.
        lock (_gate)
        {
            return ReadLocked();
        }
    }

    private CoreSample? ReadLocked()
    {
        if (_broken)
        {
            return null;
        }

        try
        {
            if (_query == IntPtr.Zero && !Open())
            {
                return null;
            }

            if (PdhCollectQueryData(_query) != 0)
            {
                return Fail("could not be collected");
            }

            if (!_primed)
            {
                _primed = true;

                return null;
            }

            var parked = Values(_parking, FormatLarge);
            var busy = Values(_busy, FormatDouble);

            if (parked is null || busy is null)
            {
                return Fail("could not be read");
            }

            if (parked.Count == 0)
            {
                return null;
            }

            var running = parked.Where(pair => pair.Value != 1).Select(pair => busy.GetValueOrDefault(pair.Key)).ToList();

            return new CoreSample(
                parked.Count,
                parked.Count(pair => pair.Value == 1),
                running.Count == 0 ? 0 : Math.Clamp(running.Average(), 0, 100),
                Clock());
        }
        catch (Exception exception)
        {
            // Whatever goes wrong reading the counters, the board carries on without the warning: logged once, then
            // not tried again (parked-cores pass 2, F2).
            return Fail(exception.Message);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            Close();
        }
    }

    private void Close()
    {
        if (_query != IntPtr.Zero)
        {
            PdhCloseQuery(_query);
            _query = IntPtr.Zero;
        }
    }

    private bool Open()
    {
        if (PdhOpenQuery(null, IntPtr.Zero, out _query) != 0
            || PdhAddEnglishCounter(_query, @"\Processor Information(*)\Parking Status", IntPtr.Zero, out _parking) != 0
            || PdhAddEnglishCounter(_query, @"\Processor Information(*)\% Processor Time", IntPtr.Zero, out _busy) != 0)
        {
            Fail("could not be opened");

            return false;
        }

        // The clock is extra: without it the reading still stands.
        if (PdhAddEnglishCounter(_query, @"\Processor Information(_Total)\% Processor Performance", IntPtr.Zero, out _clock) != 0)
        {
            _clock = IntPtr.Zero;
        }

        return true;
    }

    private CoreSample? Fail(string reason)
    {
        if (!_broken)
        {
            TheseusLog.Warning("The processor counters {Reason}, so cores held back will not be shown", reason);
        }

        _broken = true;
        Close();

        return null;
    }

    /// <summary>
    /// Each processor's value by its instance name ("group,index"), leaving out the totals; null when the array could
    /// not be read, which the caller treats as a failure (pass 2, F3).
    /// </summary>
    private static Dictionary<string, double>? Values(IntPtr counter, uint format)
    {
        var values = new Dictionary<string, double>(StringComparer.Ordinal);
        uint size = 0;

        PdhGetFormattedCounterArray(counter, format, ref size, out _, IntPtr.Zero);

        if (size == 0)
        {
            return null;
        }

        var buffer = Marshal.AllocHGlobal((int)size);

        try
        {
            if (PdhGetFormattedCounterArray(counter, format, ref size, out var count, buffer) != 0)
            {
                return null;
            }

            // PDH_FMT_COUNTERVALUE_ITEM_W: the name's pointer, then the status (4 bytes, padded to 8), then the value.
            var stride = IntPtr.Size + 16;

            for (var index = 0; index < count; index++)
            {
                var item = buffer + (index * stride);
                var name = Marshal.PtrToStringUni(Marshal.ReadIntPtr(item)) ?? string.Empty;
                var status = (uint)Marshal.ReadInt32(item + IntPtr.Size);

                if (name.Contains("_Total", StringComparison.Ordinal) || status is not (ValidData or NewData))
                {
                    continue;
                }

                var raw = Marshal.ReadInt64(item + IntPtr.Size + 8);
                values[name] = format == FormatLarge ? raw : BitConverter.Int64BitsToDouble(raw);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return values;
    }

    private double? Clock()
    {
        if (_clock == IntPtr.Zero)
        {
            return null;
        }

        return PdhGetFormattedCounterValue(_clock, FormatDouble, out _, out var value) == 0 && value.Status is ValidData or NewData
            ? value.Double
            : null;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct CounterValue
    {
        [FieldOffset(0)]
        public uint Status;

        [FieldOffset(8)]
        public double Double;
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhOpenQuery(string? dataSource, IntPtr userData, out IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhAddEnglishCounter(IntPtr query, string path, IntPtr userData, out IntPtr counter);

    [DllImport("pdh.dll")]
    private static extern uint PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhGetFormattedCounterArray(IntPtr counter, uint format, ref uint bufferSize, out uint itemCount, IntPtr items);

    [DllImport("pdh.dll")]
    private static extern uint PdhGetFormattedCounterValue(IntPtr counter, uint format, out uint type, out CounterValue value);

    [DllImport("pdh.dll")]
    private static extern uint PdhCloseQuery(IntPtr query);
}
