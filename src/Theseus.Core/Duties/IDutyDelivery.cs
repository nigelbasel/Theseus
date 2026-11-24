namespace Theseus.Core.Duties;

/// <summary>
/// Gives a card an instruction from a duty or the overnight queue: the one seam between deciding a run and starting one.
/// The board carries it out today, handing it in to an idle session or starting a stopped card. Once the headless runner
/// of objective 2.2 exists, the board's implementation asks the runner instead, which owns starting, budgets and breakers.
/// </summary>
public interface IDutyDelivery
{
    /// <summary>Gives <paramref name="card"/> the instruction.</summary>
    /// <param name="card">The card's key.</param>
    /// <param name="reason">Duty or Queue: why the run is asked for.</param>
    /// <param name="instruction">What the card is told.</param>
    DutyDelivery Deliver(string card, DutyRunReason reason, string instruction);
}
