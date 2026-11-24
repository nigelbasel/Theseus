namespace Theseus.Core.Projects;

/// <summary>
/// A criterion a wait covers, as the file had it when the view was drawn: what an answer, a done or a reassignment edits,
/// found again by objective, place and text, since the file may have moved on.
/// </summary>
/// <param name="Objective">The objective's number.</param>
/// <param name="ObjectiveTitle">The objective's title.</param>
/// <param name="Index">The criterion's place in the objective, from 0.</param>
/// <param name="Text">The criterion's text.</param>
/// <param name="Ask">The ask of its note this wait stands for, as written after <c>owner:</c>.</param>
public sealed record WaitCriterion(string Objective, string ObjectiveTitle, int Index, string Text, string Ask)
{
    /// <summary>Whether the ask is written <c>owner:</c>, so an edit takes off that ask and not another of the same words.</summary>
    public bool IsOwnerAsk { get; init; } = true;

    /// <summary>Whom the ask names in the <c>on</c> form, or null.</summary>
    public string? On { get; init; }

    /// <summary>What it asks without any whom, in the criterion's own words: what a reassignment writes.</summary>
    public string Asked { get; init; } = string.Empty;

    /// <summary>Whether an ask in the file is the one this stands for.</summary>
    public bool Is(WaitingAsk ask) =>
        ask is not null && !ask.IsWork && ask.IsOwner == IsOwnerAsk && string.Equals(ask.On, On, StringComparison.Ordinal)
        && string.Equals(ask.What, Ask, StringComparison.Ordinal);
}
