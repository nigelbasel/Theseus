namespace Theseus.Core.Projects;

/// <summary>
/// One line of "Waits for you": what to do, where, and what it unblocks, in plain words, with the button that does it
/// (1.1f G36, G37a). Nothing that waits on the owner appears only inside a box or only in the file.
/// </summary>
/// <param name="Kind">What he does, which picks the button.</param>
/// <param name="Text">The line, in plain words.</param>
/// <param name="Pieces">The pieces it concerns, by name; several for a round's independent checks.</param>
public sealed record WaitItem(WaitKind Kind, string Text, IReadOnlyList<string> Pieces)
{
    /// <summary>The card it concerns, for a report, a question, a block or a proposal; null otherwise.</summary>
    public string? CardKey { get; init; }

    /// <summary>The objective it concerns, for a criterion waiting on the owner; null otherwise.</summary>
    public string? Objective { get; init; }

    /// <summary>The proposal it concerns, for a proposal; null otherwise.</summary>
    public string? ProposalId { get; init; }

    /// <summary>The start request it concerns, for a start request; null otherwise.</summary>
    public string? StartRequestId { get; init; }

    /// <summary>The button's label, or null for something outside Theseus.</summary>
    public string? Button => Kind switch
    {
        WaitKind.AcceptSpec => "Accept spec…",
        WaitKind.ConfirmAcceptance => "Confirm…",
        WaitKind.Answer => "Answer it",
        WaitKind.Unblock => "Focus",
        WaitKind.StartVerifier => "Start again",
        WaitKind.AddToObjective => "Add to project…",
        WaitKind.ConfirmReport => "Confirm the report…",
        WaitKind.Proposal => "Review the proposal…",
        WaitKind.StartRequest => "Allow or refuse…",
        WaitKind.ClearVerifiers => "Clear up verifiers…",
        _ => null,
    };
}
