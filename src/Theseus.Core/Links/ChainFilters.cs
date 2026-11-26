namespace Theseus.Core.Links;

/// <summary>
/// The Chains panel's filters (chains-panel-at-scale criterion 8). They combine: a row shows only if every one ticked keeps
/// it. Remembered between runs, with Hide completed on by default.
/// </summary>
/// <param name="ActiveOnly">Keep cards with a session or something waiting on the owner.</param>
/// <param name="WaitingOnMe">Keep cards with something waiting on the owner.</param>
/// <param name="HideCompleted">Hide a whole chain once every card in it is completed.</param>
public sealed record ChainFilters(bool ActiveOnly = false, bool WaitingOnMe = false, bool HideCompleted = true)
{
    /// <summary>How many are on.</summary>
    public int Count => (ActiveOnly ? 1 : 0) + (WaitingOnMe ? 1 : 0) + (HideCompleted ? 1 : 0);
}
