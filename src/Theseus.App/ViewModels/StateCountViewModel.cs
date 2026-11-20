using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>One count chip of the project's summary, such as "◆ 3 waiting on you" (1.1d AC 19), zeros included.</summary>
/// <param name="State">The state it counts, which colours it.</param>
/// <param name="Count">How many pieces of work have that status.</param>
/// <param name="Words">What they are, such as "waiting on you".</param>
public sealed record StateCountViewModel(StageNodeState State, int Count, string Words)
{
    /// <summary>The chip's text: mark, number and words.</summary>
    public string Text => $"{StageNodeStates.Mark(State)} {Count} {Words}";
}
