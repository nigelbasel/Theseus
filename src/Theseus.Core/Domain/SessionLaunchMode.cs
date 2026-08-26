namespace Theseus.Core.Domain;

/// <summary>How a recorded run was started.</summary>
public enum SessionLaunchMode
{
    /// <summary>A new session under a freshly minted identifier.</summary>
    FirstRun,

    /// <summary>A continuation of an existing session and its transcript.</summary>
    Resume,

    /// <summary>A new session started because the resume it was asked for failed immediately.</summary>
    RemintedAfterResumeFailure,
}
