namespace Theseus.Core.Projects;

/// <summary>
/// A project's session asking to start a card the owner paused (sessions-run-the-project items 12 to 15): his pause is
/// his, so the start waits for his Allow. Requests are kept once decided, so the history shows what was asked.
/// </summary>
/// <param name="Id">A short random identifier.</param>
/// <param name="CardKey">The paused card.</param>
/// <param name="RequesterKey">The session's card that asked.</param>
/// <param name="Instruction">The instruction for the start, if the session gave one.</param>
/// <param name="Reason">Why the session wants it started, if it said.</param>
/// <param name="CreatedUtc">When it was first asked.</param>
/// <param name="Status">Where it stands.</param>
/// <param name="DecidedUtc">When it left pending, or null.</param>
/// <param name="Note">The owner's note on a refusal, or why it lapsed.</param>
public sealed record StartRequest(
    string Id,
    string CardKey,
    string RequesterKey,
    string? Instruction,
    string? Reason,
    DateTimeOffset CreatedUtc,
    StartRequestStatus Status = StartRequestStatus.Pending,
    DateTimeOffset? DecidedUtc = null,
    string? Note = null)
{
    /// <summary>Whether it still waits for the owner.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsPending => Status == StartRequestStatus.Pending;
}
