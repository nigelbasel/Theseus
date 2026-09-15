using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Theseus.Core.Attention;
using Theseus.Core.Diagnostics;
using Theseus.Core.Domain;
using Theseus.Core.Sessions;

namespace Theseus.App.Services;

/// <summary>
/// Phase 1 of time tracking: look, every few seconds, at which workstream's tab is in front and which
/// sessions are working, and write down the intervals. Nothing is shown; the point is to find out,
/// over a few days, whether attended time agrees with Nigel's own sense of his day before anything
/// is built on it. See DESIGN-time-tracking.md.
///
/// What is on screen is read on the UI thread, where the board lives, and everything slow - the UI
/// Automation walk of the terminal and the session registry - off it. Nothing here listens for UI
/// Automation events or touches Theseus's own window, so it adds nothing to the notifications that
/// froze the board on 2026-09-15.
/// </summary>
public sealed class AttentionSampler : IDisposable
{
    /// <summary>How often to look.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long without a keystroke or mouse movement before a tab left in front stops counting. Theseus
    /// can see focus, not attention; this is the tea-break rule, and it is a threshold, not exactness.
    /// </summary>
    public static readonly TimeSpan IdleAfter = TimeSpan.FromMinutes(5);

    private readonly Func<IReadOnlyList<Workstream>> _workstreams;
    private readonly ISessionRegistryReader _registry;
    private readonly AttentionLog _log;
    private readonly AttentionRecorder _recorder = new(Interval * 4);
    private readonly DispatcherTimer _timer;
    private bool _sampling;
    private bool _disposed;

    /// <summary>Creates a sampler over the board's workstreams, the session registry and the log it writes.</summary>
    public AttentionSampler(Func<IReadOnlyList<Workstream>> workstreams, ISessionRegistryReader registry, AttentionLog log)
    {
        _workstreams = workstreams;
        _registry = registry;
        _log = log;
        _timer = new DispatcherTimer { Interval = Interval };
        _timer.Tick += async (_, _) => await SampleAsync();
    }

    /// <summary>Starts looking.</summary>
    public void Start()
    {
        _timer.Start();
        TheseusLog.Information("Sampling attention every {Seconds}s into {Path}", Interval.TotalSeconds, _log.FilePath);
    }

    /// <summary>Stops looking, and writes down whatever was still open.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        _log.Append(_recorder.Flush(DateTimeOffset.UtcNow));
    }

    private async Task SampleAsync()
    {
        if (_sampling || _disposed)
        {
            return;
        }

        _sampling = true;

        try
        {
            var at = DateTimeOffset.UtcNow;
            var workstreams = _workstreams();
            var foreground = GetForegroundWindow();
            var present = IdleFor() < IdleAfter;
            var terminalInFront = present && TerminalWindows.IsTerminal(foreground);

            var (tab, live) = await Task.Run(() => (
                terminalInFront ? TerminalWindows.SelectedTab(foreground) : null,
                _registry.Read().Live));

            if (_disposed)
            {
                return;
            }

            _log.Append(_recorder.Observe(AttentionSampling.Sample(at, terminalInFront, tab, workstreams, live)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            TheseusLog.Debug("An attention sample was skipped: {Reason}", exception.Message);
        }
        finally
        {
            _sampling = false;
        }
    }

    private static TimeSpan IdleFor()
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };

        return GetLastInputInfo(ref info)
            ? TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.Time))
            : TimeSpan.Zero;
    }

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint Time;
    }
}
