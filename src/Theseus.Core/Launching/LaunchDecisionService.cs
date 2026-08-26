using Theseus.Core.Sessions;

namespace Theseus.Core.Launching;

/// <inheritdoc />
public sealed class LaunchDecisionService : ILaunchDecisionService
{
    private readonly ISessionIdFactory _sessionIds;

    /// <summary>Creates the launch gate.</summary>
    public LaunchDecisionService(ISessionIdFactory sessionIds) => _sessionIds = sessionIds;

    /// <inheritdoc />
    public LaunchDecision Decide(LaunchRequest request, SessionRegistrySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!string.IsNullOrWhiteSpace(request.ExistingSessionId))
        {
            var live = snapshot.WithSessionId(request.ExistingSessionId);
            if (live.Count > 0)
            {
                return LaunchDecision.Block(LaunchBlockedReason.AlreadyLive, live);
            }
        }

        if (!request.WorkingDirectoryExists)
        {
            return LaunchDecision.Block(LaunchBlockedReason.WorkingDirectoryMissing, []);
        }

        if (!string.IsNullOrWhiteSpace(request.ExistingSessionId) && request.TranscriptExists)
        {
            return LaunchDecision.Allow(LaunchMode.Resume, request.ExistingSessionId);
        }

        return LaunchDecision.Allow(LaunchMode.FirstRun, _sessionIds.Create());
    }
}
