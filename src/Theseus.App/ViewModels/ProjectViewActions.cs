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
/// <param name="Notice">Says something in the status bar, such as that Properties is hidden (1.1d AC 52); null says nothing.</param>
/// <param name="WhyNot">
/// Why a button cannot run for a card now, by card key and action, or null when it can: it is then disabled with this as
/// its tooltip (1.1e AC 6a). Null asks nothing.
/// </param>
/// <param name="WayOn">The way on from a disabled button, offered beside it as a link (1.1e AC 6a), or null.</param>
/// <param name="AddToProject">
/// Add to project… for a piece no project file lists, by project and card key, with that project chosen (1.1e AC 6);
/// null offers none.
/// </param>
public sealed record ProjectViewActions(
    Action<SpecAtGate> OpenSpec,
    Action<SpecAtGate> AcceptSpec,
    Action<string> VerifyWithHoldouts,
    Action<string> Focus,
    Action<string> Start,
    Action<string> Stop,
    Action<string> OpenPath,
    Action<string>? Notice = null,
    Action<string, string>? AddToProject = null,
    Func<string, StageAction, string?>? WhyNot = null,
    Func<string, StageAction, PieceActionViewModel?>? WayOn = null);
