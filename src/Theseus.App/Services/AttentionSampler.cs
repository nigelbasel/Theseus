using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Theseus.Core.Attention;
using Theseus.Core.Diagnostics;
using Theseus.Core.Domain;
using Theseus.Core.Persistence;
using Theseus.Core.Sessions;

namespace Theseus.App.Services;

/// <summary>
/// Look, every few seconds, at which workstream is in front and which sessions are working, and write down the
/// intervals. See DESIGN-time-tracking.md.
///
/// In front means a terminal tab or, since 7.3, one of Theseus's own windows: the owner now works mostly in Theseus, and a
/// sampler that saw only the terminal recorded almost no attended time from late October. Every busy session of a card
/// counts, each line is stamped with its piece, project, stage, session and UTC offset, what is open is checkpointed every
/// minute so a crash loses at most that, and a gap in sampling is written down so it reads as not measured.
///
/// What is on screen is read on the UI thread, where the board lives, and everything slow - the UI
/// Automation walk of the terminal, the session registry and the run log - off it. Nothing here listens for UI
/// Automation events, so it adds nothing to the notifications that froze the board on 2026-09-15.
/// </summary>
public sealed class AttentionSampler : IDisposable
{
    /// <summary>How often to look.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long without a keystroke or mouse movement before a window left in front stops counting. Theseus
    /// can see focus, not attention; this is the tea-break rule, and it is a threshold, not exactness.
    /// </summary>
    public static readonly TimeSpan IdleAfter = TimeSpan.FromMinutes(5);

    private readonly Func<IReadOnlyList<Workstream>> _workstreams;
    private readonly ISessionRegistryReader _registry;
    private readonly AttentionLog _log;
    private readonly AttentionCheckpoint _checkpoint;
    private readonly JsonLinesFile<AttentionGap> _gaps;
    private readonly SessionCards _sessionCards;
    private readonly Func<nint, TheseusFocus?> _theseus;
    private readonly Func<Workstream, AttentionStamp?> _stampOf;
    private readonly AttentionRecorder _recorder = new(Interval * 4);
    private readonly DispatcherTimer _timer;
    private bool _sampling;
    private bool _disposed;

    /// <summary>Creates a sampler.</summary>
    /// <param name="workstreams">The board's workstreams.</param>
    /// <param name="registry">The session registry.</param>
    /// <param name="log">attention.jsonl.</param>
    /// <param name="checkpoint">attention-open.json.</param>
    /// <param name="gaps">attention-gaps.jsonl.</param>
    /// <param name="sessionCards">Every session the run log knows, with its card.</param>
    /// <param name="theseus">What one of Theseus's windows shows, by its handle, or null when the handle is not Theseus's.</param>
    /// <param name="stampOf">What a card is part of now.</param>
    public AttentionSampler(Func<IReadOnlyList<Workstream>> workstreams, ISessionRegistryReader registry, AttentionLog log,
        AttentionCheckpoint checkpoint, JsonLinesFile<AttentionGap> gaps, SessionCards sessionCards, Func<nint, TheseusFocus?> theseus,
        Func<Workstream, AttentionStamp?> stampOf)
    {
        _workstreams = workstreams;
        _registry = registry;
        _log = log;
        _checkpoint = checkpoint;
        _gaps = gaps;
        _sessionCards = sessionCards;
        _theseus = theseus;
        _stampOf = stampOf;
        _timer = new DispatcherTimer { Interval = Interval };
        _timer.Tick += async (_, _) => await SampleAsync();
    }

    /// <summary>Told the board's workstreams after each sample, on the UI thread.</summary>
    public event Action<IReadOnlyList<Workstream>>? Sampled;

    /// <summary>Starts looking, first writing down what the last run left open and the time nothing was measured.</summary>
    public void Start()
    {
        var now = DateTimeOffset.UtcNow;
        var (left, gap) = _checkpoint.Recover(now, AttentionOffset.Of(now, TimeZoneInfo.Local));
        _log.Append(left);

        if (gap is not null)
        {
            _gaps.Append(gap);
        }

        if (left.Count > 0 || gap is not null)
        {
            TheseusLog.Information("Attention recovered {Count} interval(s) left open; nothing measured {From} to {To} ({Reason})",
                left.Count, gap?.From, gap?.To, gap?.Reason);
        }

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
        var now = DateTimeOffset.UtcNow;
        _log.Append(_recorder.Flush(now));
        _checkpoint.Close(now);
    }

    /// <summary>What one of this process's windows shows, by its handle: a run's output window its card, any other the main window's view.</summary>
    /// <param name="handle">The foreground window.</param>
    /// <param name="shell">What the main window shows.</param>
    public static TheseusFocus? FocusOf(nint handle, Func<TheseusFocus> shell)
    {
        ArgumentNullException.ThrowIfNull(shell);

        var window = Application.Current?.Windows.OfType<Window>().FirstOrDefault(each => new WindowInteropHelper(each).Handle == handle);

        return window switch
        {
            null => null,
            RunOutputWindow output => new TheseusFocus(output.CardTitle, null),
            _ => shell(),
        };
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
            var theseus = present && !terminalInFront ? Resolve(_theseus(foreground), workstreams) : null;

            var (tab, live, cards) = await Task.Run(() => (
                terminalInFront ? TerminalWindows.SelectedTab(foreground) : null,
                _registry.Read().Live,
                _sessionCards.Current()));

            if (_disposed)
            {
                return;
            }

            var context = new AttentionContext(theseus, cards, _stampOf, TimeZoneInfo.Local);
            _log.Append(_recorder.Observe(AttentionSampling.Sample(at, terminalInFront, tab, workstreams, live, context)));
            _gaps.Append(_recorder.DrainGaps());

            if (_checkpoint.IsDue(at))
            {
                _checkpoint.Write(_recorder.OpenIntervals(), at);
            }

            Sampled?.Invoke(workstreams);
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

    // A run output window names its card by title; everything else names it by key.
    private static TheseusFocus? Resolve(TheseusFocus? focus, IReadOnlyList<Workstream> workstreams)
    {
        if (focus?.Card is not { } named || workstreams.Any(workstream => string.Equals(workstream.Key, named, StringComparison.OrdinalIgnoreCase)))
        {
            return focus;
        }

        return focus with { Card = workstreams.FirstOrDefault(workstream => LooseName.Same(workstream.Title, named))?.Key };
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
