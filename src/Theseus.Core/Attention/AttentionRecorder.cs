namespace Theseus.Core.Attention;

/// <summary>
/// Turns a stream of samples into intervals, one clock per kind.
///
/// This is the whole of the design's defence against another forty-nine-hour day. Nobody reports a
/// duration: Theseus looks, every few seconds, at which tab is in front and which sessions are
/// working, and the time between looks is what gets counted. Attended time follows a single focused
/// tab, so two attended intervals cannot overlap by construction; machine time is kept per session
/// (7.3: every busy session of a card counts) and may overlap freely, which is why the two are
/// recorded apart and never summed.
///
/// An interval is closed and a new one opened whenever what it is stamped with changes (its session, project, piece,
/// stage, source or UTC offset), so every line written carries one stamp that was true for all of it.
///
/// A gap longer than <c>maxGap</c> between samples - the machine asleep, Theseus closed, the sampler
/// stalled - is not counted: whatever was open is closed at the last moment it was seen, and the gap is kept
/// (<see cref="DrainGaps"/>) so it reads as not measured rather than as idle.
/// </summary>
public sealed class AttentionRecorder(TimeSpan maxGap)
{
    private readonly Dictionary<string, Open> _machine = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<AttentionGap> _gaps = [];
    private Open? _attended;
    private DateTimeOffset? _last;

    /// <summary>Takes one sample, and gives back every interval it closed.</summary>
    public IReadOnlyList<AttentionInterval> Observe(AttentionSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        var closed = new List<AttentionInterval>();

        if (_last is { } last && sample.At - last > maxGap)
        {
            closed.AddRange(CloseAll(atLastSighting: true, sample.At));
            _gaps.Add(new AttentionGap(last, sample.At, AttentionGap.SamplerGap) { Offset = sample.Offset });
        }

        _last = sample.At;

        var attending = sample.Attending is { Length: > 0 } focused
            ? Shape(AttentionKind.Attended, focused, sample.AttendingSession, sample.AttendingStamp, sample.Offset, sample.AttendingSource)
            : null;

        if (_attended is { } attended && attended.Shape != attending)
        {
            closed.AddRange(Close(attended, sample.At));
            _attended = null;
        }

        if (attending is not null)
        {
            _attended ??= new Open(attending, sample.At);
            _attended.LastSeen = sample.At;
        }

        var busy = new Dictionary<string, AttentionInterval>(StringComparer.OrdinalIgnoreCase);

        foreach (var session in BusyOf(sample))
        {
            busy.TryAdd(Key(session.Workstream, session.SessionId),
                Shape(AttentionKind.Machine, session.Workstream, session.SessionId, session.Stamp, sample.Offset, source: null));
        }

        foreach (var (key, open) in _machine.Where(entry => !busy.TryGetValue(entry.Key, out var shape) || shape != entry.Value.Shape).ToList())
        {
            closed.AddRange(Close(open, sample.At));
            _machine.Remove(key);
        }

        foreach (var (key, shape) in busy)
        {
            if (!_machine.TryGetValue(key, out var open))
            {
                open = new Open(shape, sample.At);
                _machine[key] = open;
            }

            open.LastSeen = sample.At;
        }

        return closed;
    }

    /// <summary>Closes everything still open, as of now: the sampler is stopping.</summary>
    public IReadOnlyList<AttentionInterval> Flush(DateTimeOffset at) => CloseAll(atLastSighting: false, at);

    /// <summary>
    /// What is open now, each closed at the last moment it was seen: what a crash would lose, and so what the checkpoint
    /// keeps (7.3). Nothing is closed by asking.
    /// </summary>
    public IReadOnlyList<AttentionInterval> OpenIntervals() =>
    [
        .. new[] { _attended }.Concat(_machine.Values)
            .OfType<Open>()
            .Where(open => open.LastSeen > open.Start)
            .Select(open => open.Shape with { Start = open.Start, End = open.LastSeen }),
    ];

    /// <summary>The gaps seen since last asked, oldest first; each is given once.</summary>
    public IReadOnlyList<AttentionGap> DrainGaps()
    {
        var gaps = _gaps.ToList();
        _gaps.Clear();

        return gaps;
    }

    private static IEnumerable<BusySession> BusyOf(AttentionSample sample) =>
        sample.BusySessions.Count > 0
            ? sample.BusySessions.Where(session => !string.IsNullOrWhiteSpace(session.Workstream))
            : sample.Busy.Where(key => !string.IsNullOrWhiteSpace(key)).Select(key => new BusySession(key, null));

    private static string Key(string workstream, string? session) => workstream + "\n" + (session ?? string.Empty);

    private static AttentionInterval Shape(AttentionKind kind, string workstream, string? session, AttentionStamp? stamp, string? offset,
        string? source) =>
        new(kind, workstream, default, default)
        {
            Session = session,
            Project = stamp?.Project,
            PieceId = stamp?.PieceId,
            Stage = stamp?.Stage,
            Offset = offset,
            Source = source,
        };

    private List<AttentionInterval> CloseAll(bool atLastSighting, DateTimeOffset at)
    {
        var closed = new List<AttentionInterval>();

        if (_attended is { } attending)
        {
            closed.AddRange(Close(attending, atLastSighting ? attending.LastSeen : at));
            _attended = null;
        }

        foreach (var open in _machine.Values)
        {
            closed.AddRange(Close(open, atLastSighting ? open.LastSeen : at));
        }

        _machine.Clear();

        return closed;
    }

    private static IEnumerable<AttentionInterval> Close(Open open, DateTimeOffset end)
    {
        if (end > open.Start)
        {
            yield return open.Shape with { Start = open.Start, End = end };
        }
    }

    private sealed class Open(AttentionInterval shape, DateTimeOffset start)
    {
        public AttentionInterval Shape { get; } = shape;

        public DateTimeOffset Start { get; } = start;

        public DateTimeOffset LastSeen { get; set; } = start;
    }
}
