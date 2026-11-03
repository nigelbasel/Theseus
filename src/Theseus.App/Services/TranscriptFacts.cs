using System.Collections.Concurrent;
using Theseus.Core.Diagnostics;

namespace Theseus.App.Services;

/// <summary>
/// What the board shows from each session's transcript: what a resume would replay, and the model.
///
/// Read on a worker thread and kept, so a refresh only ever shows what is already known. Reading on
/// the refresh itself held the window blank: on 2026-11-03 the board's 34 stopped transcripts came
/// to 808 MB, the first refresh after a start spent 44 s measuring them, and on this machine even an
/// empty transcript took half a second to open. A card shows its restart cost and model a little
/// after the board appears instead of the board appearing late.
/// </summary>
public sealed class TranscriptFacts
{
    private readonly ConcurrentDictionary<string, TranscriptFact> _known = new(StringComparer.OrdinalIgnoreCase);
    private readonly SynchronizationContext? _owner = SynchronizationContext.Current;
    private readonly Func<string, bool, TranscriptFact> _read;
    private IReadOnlyList<(string Id, bool Live)> _wanted = [];
    private int _running;
    private int _again;

    /// <summary>Reads transcripts with the board's own readers.</summary>
    public TranscriptFacts()
        : this(ReadFromDisk)
    {
    }

    /// <summary>Reads transcripts with the given reader. For tests, which need no files.</summary>
    public TranscriptFacts(Func<string, bool, TranscriptFact> read) => _read = read;

    /// <summary>Raised on the thread that created this, when a refresh has learned something new.</summary>
    public event EventHandler? Changed;

    /// <summary>The last answer for a session, or nothing when it has not been read yet.</summary>
    public TranscriptFact Known(string? sessionId) =>
        !string.IsNullOrWhiteSpace(sessionId) && _known.TryGetValue(sessionId, out var fact) ? fact : default;

    /// <summary>
    /// Reads these sessions' transcripts in the background. A call while a read is under way is
    /// folded into one more pass when it finishes, so a burst of refreshes costs one extra pass.
    /// </summary>
    public void Refresh(IEnumerable<(string? Id, bool Live)> sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);

        Volatile.Write(ref _wanted, [.. sessions
            .Where(session => !string.IsNullOrWhiteSpace(session.Id))
            .Select(session => (session.Id!, session.Live))]);
        Volatile.Write(ref _again, 1);

        if (Interlocked.CompareExchange(ref _running, 1, 0) == 0)
        {
            _ = Task.Run(Work);
        }
    }

    /// <summary>Waits until no read is under way. For tests.</summary>
    internal async Task SettleAsync()
    {
        while (Volatile.Read(ref _running) == 1)
        {
            await Task.Delay(10);
        }
    }

    private void Work()
    {
        while (true)
        {
            while (Interlocked.Exchange(ref _again, 0) == 1)
            {
                Pass(Volatile.Read(ref _wanted));
            }

            Volatile.Write(ref _running, 0);

            if (Volatile.Read(ref _again) == 0 || Interlocked.CompareExchange(ref _running, 1, 0) != 0)
            {
                return;
            }
        }
    }

    private void Pass(IReadOnlyList<(string Id, bool Live)> sessions)
    {
        var changed = false;

        foreach (var (id, live) in sessions)
        {
            TranscriptFact fact;

            try
            {
                fact = _read(id, live);
            }
            catch (Exception exception)
            {
                TheseusLog.Warning("Reading the transcript of {Session} failed: {Reason}", id, exception.Message);

                continue;
            }

            if (!_known.TryGetValue(id, out var before) || before != fact)
            {
                _known[id] = fact;
                changed = true;
            }
        }

        if (changed)
        {
            Raise();
        }
    }

    private void Raise()
    {
        if (_owner is null)
        {
            Changed?.Invoke(this, EventArgs.Empty);

            return;
        }

        _owner.Post(_ => Changed?.Invoke(this, EventArgs.Empty), null);
    }

    // Only a stopped session is measured: a running one is not offering Restart.
    private static TranscriptFact ReadFromDisk(string sessionId, bool live) =>
        new(live ? default : TranscriptTail.Measure(sessionId), TranscriptModel.Read(sessionId));
}
