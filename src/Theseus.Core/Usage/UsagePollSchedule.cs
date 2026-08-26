using Theseus.Core.Abstractions;

namespace Theseus.Core.Usage;

/// <summary>
/// Decides when usage may next be read. The endpoint refuses far more readily than it serves, so
/// the floor between calls is enforced here rather than trusted to whatever is driving the timer,
/// and a refusal backs off rather than being retried on the next tick.
/// </summary>
public sealed class UsagePollSchedule
{
    /// <summary>The shortest interval the endpoint tolerates.</summary>
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromSeconds(180);

    /// <summary>How long to wait after the first refusal.</summary>
    public static readonly TimeSpan FirstBackoff = TimeSpan.FromMinutes(5);

    /// <summary>The longest the backoff is allowed to grow.</summary>
    public static readonly TimeSpan MaximumBackoff = TimeSpan.FromMinutes(30);

    private readonly ISystemClock _clock;
    private DateTimeOffset _nextAllowed = DateTimeOffset.MinValue;
    private TimeSpan _backoff = TimeSpan.Zero;

    /// <summary>Creates a schedule.</summary>
    public UsagePollSchedule(ISystemClock clock) => _clock = clock;

    /// <summary>Whether a read is allowed right now.</summary>
    public bool MayPollNow => _clock.UtcNow >= _nextAllowed;

    /// <summary>When the next read becomes allowed.</summary>
    public DateTimeOffset NextAllowedUtc => _nextAllowed;

    /// <summary>Records a successful read and returns to the ordinary interval.</summary>
    public void RecordSuccess()
    {
        _backoff = TimeSpan.Zero;
        _nextAllowed = _clock.UtcNow + MinimumInterval;
    }

    /// <summary>Records a refusal and doubles the wait, up to the ceiling.</summary>
    public void RecordThrottled()
    {
        _backoff = _backoff == TimeSpan.Zero
            ? FirstBackoff
            : TimeSpan.FromTicks(Math.Min(_backoff.Ticks * 2, MaximumBackoff.Ticks));

        _nextAllowed = _clock.UtcNow + _backoff;
    }

    /// <summary>Records an ordinary failure, which waits the usual interval rather than backing off.</summary>
    public void RecordFailure() => _nextAllowed = _clock.UtcNow + MinimumInterval;
}
