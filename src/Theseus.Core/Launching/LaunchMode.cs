namespace Theseus.Core.Launching;

/// <summary>How a workstream's next session should be started.</summary>
public enum LaunchMode
{
    /// <summary>Start a new session under a freshly minted identifier.</summary>
    FirstRun,

    /// <summary>Continue the workstream's existing session and its transcript.</summary>
    Resume,
}
