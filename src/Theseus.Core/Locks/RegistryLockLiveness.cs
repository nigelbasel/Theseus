using Theseus.Core.Abstractions;
using Theseus.Core.Sessions;

namespace Theseus.Core.Locks;

/// <summary>
/// Liveness from the operating system for processes and from Claude Code's session registry for
/// sessions. The registry is read at most once every few seconds, because every poll from a waiting
/// script asks, and reading it means probing every registered process.
/// </summary>
public sealed class RegistryLockLiveness : ILockLiveness
{
    private static readonly TimeSpan RegistryFreshFor = TimeSpan.FromSeconds(5);

    private readonly IProcessProbe _probe;
    private readonly ISessionRegistryReader _registry;
    private readonly ISystemClock _clock;
    private readonly Lock _gate = new();
    private SessionRegistrySnapshot _snapshot = SessionRegistrySnapshot.Empty;
    private DateTimeOffset _readUtc = DateTimeOffset.MinValue;

    /// <summary>Creates liveness over the process probe and the session registry.</summary>
    public RegistryLockLiveness(IProcessProbe probe, ISessionRegistryReader registry, ISystemClock clock)
    {
        _probe = probe;
        _registry = registry;
        _clock = clock;
    }

    /// <inheritdoc />
    public string? StartOf(int processId) => _probe.GetStartFileTime(processId);

    /// <inheritdoc />
    public bool IsSessionAlive(string sessionId)
    {
        lock (_gate)
        {
            var now = _clock.UtcNow;

            if (now - _readUtc > RegistryFreshFor)
            {
                _snapshot = _registry.Read();
                _readUtc = now;
            }

            return _snapshot.WithSessionId(sessionId).Count > 0;
        }
    }
}
