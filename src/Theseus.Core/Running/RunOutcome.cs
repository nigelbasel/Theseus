namespace Theseus.Core.Running;

/// <summary>How a headless run ended.</summary>
public enum RunOutcome
{
    /// <summary>It exited cleanly after a successful turn.</summary>
    Completed,

    /// <summary>It was stopped at a budget limit.</summary>
    StoppedAtLimit,

    /// <summary>It exited with an error and no result.</summary>
    Crashed,

    /// <summary>It never started: no session began within the start-up allowance.</summary>
    FailedStart,

    /// <summary>Its turn ended in an error, reported by Claude Code.</summary>
    Failed,

    /// <summary>It ran past its time limit and the grace after it, and was interrupted.</summary>
    OverTime,

    /// <summary>The owner opened it in a tab, which interrupted it.</summary>
    Interrupted,
}
