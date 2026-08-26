namespace Theseus.Launcher;

/// <summary>Where the shim reports what it did. Reports are sent as they happen, not on exit.</summary>
public interface ILaunchReporter
{
    /// <summary>Claude Code started under <paramref name="sessionId"/>.</summary>
    void Started(string sessionId, int claudeProcessId);

    /// <summary>A resume failed immediately and was retried under a new identifier.</summary>
    void Reminted(string previousSessionId, string newSessionId, string reason);

    /// <summary>Claude Code exited.</summary>
    void Exited(string sessionId, int exitCode, long durationMs);
}
