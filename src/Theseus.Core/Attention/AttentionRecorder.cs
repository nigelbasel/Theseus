namespace Theseus.Core.Attention;

/// <summary>
/// Turns a stream of samples into intervals, one clock per kind.
///
/// This is the whole of the design's defence against another forty-nine-hour day. Nobody reports a
/// duration: Theseus looks, every few seconds, at which tab is in front and which sessions are
/// working, and the time between looks is what gets counted. Attended time follows a single focused
/// tab, so two attended intervals cannot overlap by construction; machine time is kept per workstream
/// and may overlap freely, which is why the two are recorded apart and never summed.
///
/// A gap longer than <c>maxGap</c> between samples - the machine asleep, Theseus closed, the sampler
/// stalled - is not counted: whatever was open is closed at the last moment it was seen.
/// </summary>
public sealed class AttentionRecorder(TimeSpan maxGap)
{
    private readonly Dictionary<string, Open> _machine = new(StringComparer.OrdinalIgnoreCase);
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
        }

        _last = sample.At;

        if (_attended is { } attending && !string.Equals(attending.Workstream, sample.Attending, StringComparison.OrdinalIgnoreCase))
        {
            closed.AddRange(Close(AttentionKind.Attended, attending, sample.At));
            _attended = null;
        }

        if (sample.Attending is { Length: > 0 } focused)
        {
            _attended ??= new Open(focused, sample.At);
            _attended.LastSeen = sample.At;
        }

        foreach (var workstream in _machine.Keys.Where(key => !sample.Busy.Contains(key, StringComparer.OrdinalIgnoreCase)).ToList())
        {
            closed.AddRange(Close(AttentionKind.Machine, _machine[workstream], sample.At));
            _machine.Remove(workstream);
        }

        foreach (var workstream in sample.Busy.Where(key => !string.IsNullOrWhiteSpace(key)))
        {
            if (!_machine.TryGetValue(workstream, out var open))
            {
                open = new Open(workstream, sample.At);
                _machine[workstream] = open;
            }

            open.LastSeen = sample.At;
        }

        return closed;
    }

    /// <summary>Closes everything still open, as of now: the sampler is stopping.</summary>
    public IReadOnlyList<AttentionInterval> Flush(DateTimeOffset at) => CloseAll(atLastSighting: false, at);

    private List<AttentionInterval> CloseAll(bool atLastSighting, DateTimeOffset at)
    {
        var closed = new List<AttentionInterval>();

        if (_attended is { } attending)
        {
            closed.AddRange(Close(AttentionKind.Attended, attending, atLastSighting ? attending.LastSeen : at));
            _attended = null;
        }

        foreach (var open in _machine.Values)
        {
            closed.AddRange(Close(AttentionKind.Machine, open, atLastSighting ? open.LastSeen : at));
        }

        _machine.Clear();

        return closed;
    }

    private static IEnumerable<AttentionInterval> Close(AttentionKind kind, Open open, DateTimeOffset end)
    {
        if (end > open.Start)
        {
            yield return new AttentionInterval(kind, open.Workstream, open.Start, end);
        }
    }

    private sealed class Open(string workstream, DateTimeOffset start)
    {
        public string Workstream { get; } = workstream;

        public DateTimeOffset Start { get; } = start;

        public DateTimeOffset LastSeen { get; set; } = start;
    }
}
