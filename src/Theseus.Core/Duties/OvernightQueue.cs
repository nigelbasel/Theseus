using System.Globalization;
using Theseus.Core.Settings;

namespace Theseus.Core.Duties;

/// <summary>
/// The overnight queue: a project's built-in duty that wakes a card left idle with its piece of work unfinished and nothing
/// waiting on the owner, so unattended hours are not lost (objective 2.3's evidence: a card idle from 22:04 to 07:15 on
/// 2026-11-21). It is off unless the owner switches it on for the project, and it only ever wakes an open session: a
/// stopped card is the owner's, or its project's Start's, to start (Nigel, 2026-11-24).
/// </summary>
public static class OvernightQueue
{
    /// <summary>The cards to resume now.</summary>
    /// <param name="candidates">Every card of every project.</param>
    /// <param name="records">The queue's memory of each card, by key.</param>
    /// <param name="settings">The board's limits, with which projects have the queue on.</param>
    /// <param name="nowUtc">The time now.</param>
    /// <param name="zone">The local time zone.</param>
    public static IReadOnlyList<QueueResume> Choose(IReadOnlyList<QueueCandidate> candidates, Func<string, QueueRecord?> records,
        DutySettings settings, DateTimeOffset nowUtc, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(settings);

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(nowUtc, zone).DateTime);

        return [.. candidates
            .Where(candidate => Refusal(candidate, records(candidate.Card.Key), settings, nowUtc, today) is null)
            .Select(candidate => new QueueResume(candidate.Card.Key, candidate.Card.Title, candidate.Status, Instruction(candidate, zone)))];
    }

    /// <summary>Why the queue does not resume this card now, or null when it does.</summary>
    public static string? Refusal(QueueCandidate candidate, QueueRecord? record, DutySettings settings, DateTimeOffset nowUtc, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(settings);

        var card = candidate.Card;

        return candidate switch
        {
            _ when !settings.QueueIsOn(candidate.Project) => "the queue is off for its project",
            { ProjectRunning: false } => "its project is stopped",
            { HasUnfinishedPiece: false } => "it has no unfinished piece of work",
            { IsVerifier: true } => "it is a verifier",
            { WaitsForOwner: true } => "its piece waits for you",
            _ when card.IsArchived => "it is archived",
            _ when card.PausedByOwner || card.PausedByCard is not null => "it is paused",
            _ when card.IsBlocked => "it is blocked",
            _ when card.StoppedByOwner => "you stopped it",
            _ when card.IsStopping => "it is handing over",
            { IsQuietlyIdle: false } => "it is not open and idle",
            { StatusSinceUtc: null } => "Theseus does not know how long it has been idle",
            { StatusSinceUtc: { } since } when nowUtc - since < settings.QueueIdleThreshold => "it has not been idle long enough",
            _ when record is not null && string.Equals(record.StatusAtResume, candidate.Status, StringComparison.Ordinal)
                => "its piece has not moved since the queue last resumed it",
            _ when (record?.ResumesOn(today) ?? 0) >= settings.QueueDailyCapPerCard => "the queue's daily cap for it is reached",
            _ => null,
        };
    }

    /// <summary>What a resumed card is told.</summary>
    public static string Instruction(QueueCandidate candidate, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var since = candidate.StatusSinceUtc is { } at
            ? TimeZoneInfo.ConvertTime(at, zone).ToString("HH:mm", CultureInfo.InvariantCulture)
            : "a while";

        return $"Theseus's overnight queue: your piece \"{candidate.PieceTitle}\" is at {candidate.Status}, and you have been idle "
               + $"since {since} with nothing waiting on the owner. Carry on with its next step. If it is done, or blocked on someone, "
               + "say so with now_at or ask_owner and end your turn.";
    }
}
