namespace Theseus.Core.Running;

/// <summary>
/// The one way anything other than a tab's own Start asks for a card to run: duties and the overnight queue
/// (objective 2.3) call it, and it starts the card in its mode, inside its budget, under the machine-wide cap.
/// </summary>
public interface IRunRequests
{
    /// <summary>Starts, queues or refuses a run of one card.</summary>
    RunRequestOutcome Request(RunRequest request);
}
