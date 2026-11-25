namespace Theseus.Core.Mcp;

/// <summary>
/// What a project's session may do to its descendants' cards through Theseus (1.1e AC 14), carried out by the board the
/// way the owner's own buttons are. The handler has already checked the card is a descendant and in a state to act on;
/// the board checks the state again as it acts. Each call returns why it could not be done, or null when it was.
/// <c>caller</c> is the calling session's card key.
/// </summary>
public interface IWorkstreamControl
{
    /// <summary>Starts a card, resuming its conversation, with an instruction for this start only, or none.</summary>
    string? Start(string key, string? instruction, string caller);

    /// <summary>Sends a card's session the stop request; it ends after its current turn.</summary>
    string? Stop(string key, string caller);

    /// <summary>Stops a card if it runs and marks it paused by the caller, with the reason.</summary>
    string? Pause(string key, string caller, string? reason);

    /// <summary>Clears a pause the caller made and starts the card, with an instruction for this start only, or none.</summary>
    string? Resume(string key, string? instruction, string caller);
}
