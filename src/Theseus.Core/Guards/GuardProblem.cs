namespace Theseus.Core.Guards;

/// <summary>What keeps a guard from protecting this machine's sessions.</summary>
public enum GuardProblem
{
    /// <summary>No guard is where the hook would run it.</summary>
    NotInstalled,

    /// <summary>The installed guard, or a file it needs, is not this Theseus's.</summary>
    OutOfDate,

    /// <summary>Claude Code's settings do not run it before the tools it must see.</summary>
    NotRun,
}
