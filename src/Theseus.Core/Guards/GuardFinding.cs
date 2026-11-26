namespace Theseus.Core.Guards;

/// <summary>A guard that is not set up, and what is wrong with it.</summary>
/// <param name="State">The guard as it is now.</param>
/// <param name="Problem">What is wrong: not installed, out of date, or not run by Claude Code.</param>
public sealed record GuardFinding(GuardState State, GuardProblem Problem)
{
    /// <summary>The finding as a sentence, such as "The trunk guard is not installed."</summary>
    public string Line => Problem switch
    {
        GuardProblem.NotInstalled => $"The {State.Name} is not installed.",
        GuardProblem.OutOfDate => $"The {State.Name} is out of date.",
        _ => $"Claude Code's settings do not run the {State.Name}.",
    };

    /// <summary>The finding as a clause of the board's notice, such as "the trunk guard is not installed".</summary>
    public string Clause => Problem switch
    {
        GuardProblem.NotInstalled => $"the {State.Name} is not installed",
        GuardProblem.OutOfDate => $"the {State.Name} is out of date",
        _ => $"Claude Code's settings do not run the {State.Name}",
    };
}
