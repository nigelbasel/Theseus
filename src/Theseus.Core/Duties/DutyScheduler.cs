using System.Globalization;
using Theseus.Core.Domain;
using Theseus.Core.Settings;

namespace Theseus.Core.Duties;

/// <summary>
/// Decides, for every live duty, what to do now from its card's state, its limits and the time. Pure: the board gives it
/// what it sees and carries out what it says.
/// </summary>
public static class DutyScheduler
{
    /// <summary>How long a given run may go without the card being seen working before it counts as not delivered.</summary>
    public static readonly TimeSpan TakeUpLimit = TimeSpan.FromMinutes(10);

    /// <summary>What to do with each live duty now.</summary>
    /// <param name="duties">Every duty; ended ones are left out of the answer.</param>
    /// <param name="cards">The board's cards, by key.</param>
    /// <param name="settings">The board's limits.</param>
    /// <param name="nowUtc">The time now.</param>
    /// <param name="zone">The local time zone.</param>
    /// <param name="criterionTicked">Whether a duty's end criterion is ticked.</param>
    public static IReadOnlyList<DutyDecision> Decide(IReadOnlyList<Duty> duties, IReadOnlyDictionary<string, DutyCard> cards,
        DutySettings settings, DateTimeOffset nowUtc, TimeZoneInfo zone, Func<Duty, bool>? criterionTicked = null)
    {
        ArgumentNullException.ThrowIfNull(duties);
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(zone);

        return [.. duties.Where(duty => !duty.IsEnded)
            .Select(duty => Decide(duty, cards.GetValueOrDefault(duty.Card), settings, nowUtc, zone, criterionTicked))];
    }

    private static DutyDecision Decide(Duty duty, DutyCard? card, DutySettings settings, DateTimeOffset nowUtc, TimeZoneInfo zone,
        Func<Duty, bool>? criterionTicked)
    {
        var local = TimeZoneInfo.ConvertTime(nowUtc, zone);
        var today = DateOnly.FromDateTime(local.DateTime);

        // A card missing from the board is more often a board still loading than a card gone, so nothing ends for it.
        if (card is null)
        {
            return None(duty);
        }

        if (card.IsArchived)
        {
            return new DutyDecision(duty, DutyAction.End, "its card is archived");
        }

        if (duty.End is { Kind: DutyEndKind.OnDate, Date: { } date } && today >= date)
        {
            return new DutyDecision(duty, DutyAction.End, $"its end date, {date:yyyy-MM-dd}, came");
        }

        if (duty.End.Kind == DutyEndKind.OnCriterion && criterionTicked?.Invoke(duty) == true)
        {
            return new DutyDecision(duty, DutyAction.End, $"its criterion was ticked: {duty.End.Criterion}");
        }

        var state = duty.State;
        var runNow = state.RunNowRequested;

        if (state.DeliveredUtc is not null || (duty.IsPaused && !runNow) || (!runNow && nowUtc < state.NextDueUtc))
        {
            return None(duty);
        }

        if (card.PausedByOwner)
        {
            return Skip(duty, "paused by you");
        }

        if (card.PausedByCard is { } by)
        {
            return Skip(duty, $"paused by {by}");
        }

        if (card.IsBlocked)
        {
            return Skip(duty, "blocked");
        }

        if (card.StoppedByOwner)
        {
            return Skip(duty, "stopped by you");
        }

        var cap = duty.DailyRunCap ?? settings.DailyRunCap;

        if (state.RunsOn(today) >= cap)
        {
            return Skip(duty, $"its daily cap of {cap} runs is reached");
        }

        if (!duty.ThroughQuietHours && !runNow && settings.IsQuiet(TimeOnly.FromDateTime(local.DateTime)))
        {
            return Wait(duty, $"quiet hours until {settings.QuietUntil.ToString("HH:mm", CultureInfo.InvariantCulture)}");
        }

        if (card.IsStopping)
        {
            return Wait(duty, "its session is handing over");
        }

        if (card.IsAsking || card.Lifecycle == SessionLifecycle.NeedsAttention)
        {
            return Wait(duty, "it is asking you something");
        }

        if (card.IsBusy)
        {
            return Wait(duty, "card busy");
        }

        if (card.TellWaiting)
        {
            return Wait(duty, "its last instruction is not yet taken up");
        }

        if (card.IsLive && card.Lifecycle == SessionLifecycle.Ghost)
        {
            return Wait(duty, "its session is in a state Theseus cannot read");
        }

        var missed = nowUtc < state.NextDueUtc ? 0 : duty.Cadence.CountBetween(duty.SetUtc, state.NextDueUtc, nowUtc, zone);

        return new DutyDecision(duty, DutyAction.Run, string.Empty, missed);
    }

    /// <summary>
    /// How the run in flight stands, from the card as it is now. A card counts as having taken the run once it is seen
    /// working, and the run finishes when it is no longer working: idle again, asking, or its session ended.
    /// </summary>
    /// <returns>The outcome and its detail once there is one, otherwise null; and whether the card has been seen working.</returns>
    public static (DutyOutcome? Outcome, string? Detail, bool SeenWorking) Observe(Duty duty, DutyCard? card, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(duty);

        var state = duty.State;

        if (state.DeliveredUtc is not { } given)
        {
            return (null, null, false);
        }

        if (card is null)
        {
            return (null, null, state.SeenWorking);
        }

        if (card.IsLive && card.Lifecycle == SessionLifecycle.Ghost)
        {
            return (DutyOutcome.Crashed, "its session died during the run", state.SeenWorking);
        }

        // A short turn can start and end between two looks, so a status change since the run was given counts as taking it.
        // A status change alone is not enough: an idle notice changes it too. The tell must also have been taken.
        var seen = state.SeenWorking || card.IsBusy || (card.StatusSinceUtc > given && !card.TellWaiting);

        if (seen && !card.IsBusy)
        {
            return (DutyOutcome.Finished, null, true);
        }

        if (!seen && nowUtc - given > TakeUpLimit)
        {
            return (DutyOutcome.NotDelivered, $"the card did not take it up within {TakeUpLimit.TotalMinutes:0} minutes", false);
        }

        return (null, null, seen);
    }

    private static DutyDecision None(Duty duty) => new(duty, DutyAction.None, string.Empty);

    private static DutyDecision Skip(Duty duty, string why) => new(duty, DutyAction.Skip, why);

    private static DutyDecision Wait(Duty duty, string why) => new(duty, DutyAction.Wait, why);
}
