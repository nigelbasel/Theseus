namespace Theseus.Core.Duties;

/// <summary>What to do with one duty now, and why.</summary>
/// <param name="Duty">The duty.</param>
/// <param name="Action">Run, wait, skip, end or nothing.</param>
/// <param name="Reason">Why, in the words the card and its History show; empty for None and Run.</param>
/// <param name="Missed">For a run: how many due times passed beyond the one it runs for, folded into it.</param>
public sealed record DutyDecision(Duty Duty, DutyAction Action, string Reason, int Missed = 0);
