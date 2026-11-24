using Theseus.Core.Abstractions;

namespace Theseus.Core.Duties;

/// <summary>
/// Every duty and the overnight queue's memory, kept in <see cref="DutyStore"/> and changed only through these methods, so
/// each change is saved at once and a restart picks up exactly where the board left off.
/// </summary>
public sealed class DutyBook
{
    /// <summary>How long an ended duty is kept, so the History can still name it.</summary>
    public static readonly TimeSpan EndedKeptFor = TimeSpan.FromDays(30);

    private readonly DutyStore _store;
    private readonly ISystemClock _clock;
    private readonly TimeZoneInfo _zone;
    private readonly object _gate = new();
    private List<Duty> _duties;
    private List<QueueRecord> _queue;

    /// <summary>Opens the book over its store, dropping duties ended longer ago than <see cref="EndedKeptFor"/>.</summary>
    public DutyBook(DutyStore store, ISystemClock clock, TimeZoneInfo? zone = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(clock);

        _store = store;
        _clock = clock;
        _zone = zone ?? TimeZoneInfo.Local;

        var file = store.Load();
        var now = clock.UtcNow;

        _duties = [.. file.Duties.Where(duty => duty.EndedUtc is not { } ended || now - ended <= EndedKeptFor)];
        _queue = [.. file.Queue];
    }

    /// <summary>Raised after any change is saved.</summary>
    public event EventHandler? Changed;

    /// <summary>Every duty kept, ended ones included.</summary>
    public IReadOnlyList<Duty> All
    {
        get
        {
            lock (_gate)
            {
                return [.. _duties];
            }
        }
    }

    /// <summary>The duties that have not ended.</summary>
    public IReadOnlyList<Duty> Live => [.. All.Where(duty => !duty.IsEnded)];

    /// <summary>The live duties of one card.</summary>
    public IReadOnlyList<Duty> For(string card) =>
        [.. Live.Where(duty => string.Equals(duty.Card, card, StringComparison.OrdinalIgnoreCase))];

    /// <summary>A duty by its id, ended or not.</summary>
    public Duty? Get(string id) => All.FirstOrDefault(duty => string.Equals(duty.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>A card's live duty by name.</summary>
    public Duty? Find(string card, string name) =>
        For(card).FirstOrDefault(duty => string.Equals(duty.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Sets a duty on a card, or changes the card's live duty of that name. A new or changed cadence counts from now.
    /// </summary>
    /// <param name="setBy">The calling session's card key, or null for the owner.</param>
    /// <returns>The duty and whether it was an existing one changed; or why it could not be set.</returns>
    public (Duty? Duty, bool Updated, string? Refusal) Set(string card, string name, string instruction, DutyCadence cadence, DutyEnd end,
        string? setBy, int? dailyRunCap = null, bool throughQuietHours = false)
    {
        ArgumentNullException.ThrowIfNull(cadence);
        ArgumentNullException.ThrowIfNull(end);

        if (string.IsNullOrWhiteSpace(name))
        {
            return (null, false, "Give the duty a name.");
        }

        if (string.IsNullOrWhiteSpace(instruction))
        {
            return (null, false, "Give the duty an instruction.");
        }

        if (cadence.Refusal() is { } badCadence)
        {
            return (null, false, badCadence);
        }

        if (dailyRunCap is < 1)
        {
            return (null, false, "A daily cap is at least 1 run.");
        }

        var now = _clock.UtcNow;

        lock (_gate)
        {
            var existing = _duties.FirstOrDefault(duty => !duty.IsEnded
                && string.Equals(duty.Card, card, StringComparison.OrdinalIgnoreCase)
                && string.Equals(duty.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));

            if (existing is not null && setBy is not null && existing.IsOwners)
            {
                return (null, false, $"The duty \"{existing.Name}\" on this card was set by the user: only they change it.");
            }

            var cadenceChanged = existing is null || existing.Cadence != cadence;
            var anchor = cadenceChanged ? now : existing!.SetUtc;
            var state = existing?.State ?? new DutyRunState(cadence.NextAfter(now, now, _zone));

            if (cadenceChanged)
            {
                state = state with { NextDueUtc = cadence.NextAfter(anchor, now, _zone) };
            }

            var duty = new Duty(existing?.Id ?? NewId(), card, name.Trim(), instruction.Trim(), cadence, end, existing is null || setBy is null ? setBy : existing.SetBy, anchor, state)
            {
                DailyRunCap = dailyRunCap,
                ThroughQuietHours = throughQuietHours,
                PausedBy = existing?.PausedBy,
                PauseReason = existing?.PauseReason,
            };

            Replace(existing, duty);

            return (duty, existing is not null, null);
        }
    }

    /// <summary>Pauses a duty, by the owner, Theseus or a card.</summary>
    /// <remarks>A pause by the owner or by Theseus is kept: a session's pause on top of it would let the session lift it.</remarks>
    public Duty? Pause(string id, string by, string? reason) =>
        Change(id, duty => duty.IsEnded || duty.PausedBy is Duty.Owner or Duty.Theseus
            ? duty
            : duty with { PausedBy = by, PauseReason = Clean(reason) });

    /// <summary>Clears a duty's pause and its count of failures, its next run counting on from now.</summary>
    public Duty? Resume(string id) => Change(id, duty => duty.IsEnded ? duty : duty with
    {
        PausedBy = null,
        PauseReason = null,
        State = duty.State with
        {
            FailuresInRow = 0,
            NextDueUtc = duty.State.NextDueUtc > _clock.UtcNow ? duty.State.NextDueUtc : duty.Cadence.NextAfter(duty.SetUtc, _clock.UtcNow, _zone),
        },
    });

    /// <summary>Ends a duty, with why. An ended duty never runs again.</summary>
    public Duty? End(string id, string reason) =>
        Change(id, duty => duty.IsEnded ? duty : duty with { EndedUtc = _clock.UtcNow, EndReason = Clean(reason) });

    /// <summary>Removes a duty altogether.</summary>
    public bool Delete(string id)
    {
        lock (_gate)
        {
            var removed = _duties.RemoveAll(duty => string.Equals(duty.Id, id, StringComparison.OrdinalIgnoreCase)) > 0;

            if (removed)
            {
                Save();
            }

            return removed;
        }
    }

    /// <summary>Asks for a run now, which the next look gives whatever the cadence.</summary>
    public Duty? RunNow(string id) =>
        Change(id, duty => duty.IsEnded ? duty : duty with { State = duty.State with { RunNowRequested = true } });

    /// <summary>Records a run given to the card. The next due time moves on from now unless this was a Run now.</summary>
    public Duty? Ran(string id) => Change(id, duty =>
    {
        var now = _clock.UtcNow;
        var state = duty.State;
        var today = Today(now);
        var next = state.RunNowRequested && state.NextDueUtc > now ? state.NextDueUtc : duty.Cadence.NextAfter(duty.SetUtc, now, _zone);

        return duty with
        {
            State = state with
            {
                NextDueUtc = next,
                LastRunUtc = now,
                RunCount = state.RunCount + 1,
                CountedDay = today,
                RunsThatDay = state.RunsOn(today) + 1,
                DeliveredUtc = now,
                SeenWorking = false,
                Waiting = null,
                RunNowRequested = false,
            },
        };
    });

    /// <summary>Records a skip, with why; the next due time moves on from now.</summary>
    public Duty? Skipped(string id, string reason) => Change(id, duty => duty with
    {
        State = duty.State with
        {
            NextDueUtc = duty.Cadence.NextAfter(duty.SetUtc, _clock.UtcNow, _zone),
            LastSkipUtc = _clock.UtcNow,
            LastSkipReason = reason,
            Waiting = null,
            RunNowRequested = false,
        },
    });

    /// <summary>Records why a due run waits, or that it no longer does. Saved only when the reason changes.</summary>
    public Duty? Waiting(string id, string? reason)
    {
        var duty = Get(id);

        return duty is null || string.Equals(duty.State.Waiting, reason, StringComparison.Ordinal)
            ? duty
            : Change(id, each => each with { State = each.State with { Waiting = reason } });
    }

    /// <summary>Records that the card was seen working on the run in flight.</summary>
    public Duty? SeenWorking(string id)
    {
        var duty = Get(id);

        return duty is null || duty.State.SeenWorking
            ? duty
            : Change(id, each => each with { State = each.State with { SeenWorking = true } });
    }

    /// <summary>
    /// Records how the run in flight went. After <paramref name="failureLimit"/> failures in a row the duty pauses itself,
    /// saying why; a finished run clears the count.
    /// </summary>
    /// <returns>The duty, and whether this outcome paused it.</returns>
    public (Duty? Duty, bool Paused) Outcome(string id, DutyOutcome outcome, string? detail, int failureLimit)
    {
        var paused = false;
        var duty = Change(id, each =>
        {
            var failures = outcome == DutyOutcome.Finished ? 0 : each.State.FailuresInRow + 1;
            var changed = each with
            {
                State = each.State with
                {
                    LastOutcome = outcome,
                    LastDetail = Clean(detail),
                    FailuresInRow = failures,
                    DeliveredUtc = null,
                    SeenWorking = false,
                },
            };

            if (outcome != DutyOutcome.Finished && failures >= failureLimit && !each.IsPaused)
            {
                paused = true;

                return changed with
                {
                    PausedBy = Duty.Theseus,
                    PauseReason = $"paused by Theseus after {failures} failed runs: {detail ?? outcome.ToString()}",
                };
            }

            return changed;
        });

        return (duty, paused);
    }

    /// <summary>The queue's record of a card, or null when it has never resumed it.</summary>
    public QueueRecord? QueueRecordFor(string card)
    {
        lock (_gate)
        {
            return _queue.FirstOrDefault(each => string.Equals(each.Card, card, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>Records that the queue resumed a card at this status.</summary>
    public QueueRecord QueueResumed(string card, string status)
    {
        var now = _clock.UtcNow;
        var today = Today(now);

        lock (_gate)
        {
            var existing = _queue.FirstOrDefault(each => string.Equals(each.Card, card, StringComparison.OrdinalIgnoreCase));
            var record = new QueueRecord(card, now, status, today, (existing?.ResumesOn(today) ?? 0) + 1);

            if (existing is not null)
            {
                _queue.Remove(existing);
            }

            _queue.Add(record);
            Save();

            return record;
        }
    }

    /// <summary>The local day of a moment, in the book's time zone.</summary>
    public DateOnly Today(DateTimeOffset utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utc, _zone).DateTime);

    private Duty? Change(string id, Func<Duty, Duty> change)
    {
        lock (_gate)
        {
            var existing = _duties.FirstOrDefault(duty => string.Equals(duty.Id, id, StringComparison.OrdinalIgnoreCase));

            if (existing is null)
            {
                return null;
            }

            var changed = change(existing);

            if (changed != existing)
            {
                Replace(existing, changed);
            }

            return changed;
        }
    }

    private void Replace(Duty? existing, Duty duty)
    {
        var index = existing is null ? -1 : _duties.IndexOf(existing);

        if (index < 0)
        {
            _duties.Add(duty);
        }
        else
        {
            _duties[index] = duty;
        }

        Save();
    }

    private void Save()
    {
        _store.Save(new DutyFile([.. _duties], [.. _queue]));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static string NewId() => $"d-{Guid.NewGuid():N}"[..10];
}
