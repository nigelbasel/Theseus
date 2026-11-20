using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// What the project view's buttons run (1.1d AC 27): the shell's existing Open spec, Accept spec, Verify with holdouts,
/// Focus, Start and Stop, each for one card by its key, and opening a records folder or file.
/// </summary>
/// <param name="OpenSpec">Opens a spec as it was read.</param>
/// <param name="AcceptSpec">Accepts a spec, after a confirmation naming the piece of work and the commit.</param>
/// <param name="VerifyWithHoldouts">Creates a verifier card for a card's current piece of work.</param>
/// <param name="Focus">Brings a card's terminal forward.</param>
/// <param name="Start">Starts a card.</param>
/// <param name="Stop">Asks a card's session to write its handover and stop.</param>
/// <param name="OpenPath">Opens a records folder or a record in its editor.</param>
public sealed record ProjectViewActions(
    Action<SpecAtGate> OpenSpec,
    Action<SpecAtGate> AcceptSpec,
    Action<string> VerifyWithHoldouts,
    Action<string> Focus,
    Action<string> Start,
    Action<string> Stop,
    Action<string> OpenPath);
