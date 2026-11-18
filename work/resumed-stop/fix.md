# resumed-stop: fix

Pipeline: `bug` (method kit v0), autonomy L0. Card git-22, branch `main`, repo Theseus.

## Spec (stage **rule**, for Nigel's approval)

What the code protects against: a second `/exit`, or a second stop request, landing in a session that is already
finishing or has moved on; and a stop that is never finished. When unsure, it does not type into a session.

1. **One stop is one request**, from the press until `/exit` is typed or the request is removed. A stop is known
   by its session and the time it was asked.
2. **`/exit` is typed at most once per stop, and a later stop can be finished.** A stop asked after the last `/exit`
   (a resumed session under the same id) is a new stop, and is closed in the usual way. (L3)
3. **Pressing Stop on a session whose stop has already been delivered does not ask again.** The session has been
   told; telling it twice can interrupt it while it writes the handoff. Instead the press runs the close check at
   once, so a session that has handed over and gone quiet is closed on the press, and otherwise the notice says the
   stop is under way. (F1; and in part L2, a press that seems to do nothing.)
4. **A stop's time is the first press.** Asking again while the request is still waiting keeps its original request
   time, so a stop's figures run from when it was first asked. (F3)
5. **A stop counts as finished once `/exit` has been typed for it.** If its request cannot then be removed, a later
   removal of that request in the same board run is not reported as unfinished. After a restart, the leftover
   request is handled as today (held back and asked about). (F4)
6. **Unchanged:** the routes for a request still waiting (interrupt a working session on a second press, nudge an
   idle one), the close conditions and quiet period, expiry, and what is logged.

Out of scope: L1 (closes that wait for an unrelated refresh) and the rest of L2 (showing on the card that a stop is
under way). Rule 3 helps both on a second press only.

## The fix (rule approved by Nigel, 2026-11-18)

| Rule | Change |
|---|---|
| 1, 2 | `ExitsTyped` (Core) keys a stop by its session **and** the moment it was asked, not by session alone. The reproduction test passes |
| 3 | `StopRouting.For` takes `alreadyDelivered`; a live session already handed a stop by this process gets the new route `AlreadyDelivered` and is not asked again. `BeginStop` logs "Did not ask {Workstream} again", and the notice says the stop is under way. Its existing `Refresh()` runs the close step, so a handed-over, quiet session closes on that press |
| 4 | `StopRequestStore.Write` keeps the waiting request's `RequestedUtc` when a stop is asked for again before delivery |
| 5 | The start-again and newer-session retires, and `Sweep` (through a `finished` predicate), do not report a stop as unfinished if `/exit` was typed for it in this board run |
| 6 | Nothing else changed: the existing routes, close conditions, quiet period, expiry and log lines |

New tests, written first and failing to build before the code: routing for a delivered stop (4 cases), a stop of
another session at the same moment, and the first request time kept / a new stop after delivery. Full suite
**1481 of 1481**, 0 warnings.

Cause addressed, not the symptom: the record was keyed on the wrong thing. The other changes come from the same
missing rule: nothing said what a second press on a delivered stop should do.

Known limits:

- Rule 5 holds within one board run. After a restart, a leftover request is held back and asked about, as before.
- The "reproduction test unchanged" check is weaker than it should be: a new test was added to the same file, so the
  file's diff since ed0bd0e is not empty, though it is additions only (11 lines, 0 removed) and the reproduction test
  is untouched. New tests belong in a separate file.
- The App changes (the route's handling, the notice, the call-site guards) have no unit tests; the live check covers
  them.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| rule | 2026-11-18 16:48:58 (logged) | 16:49:25 (commit 6063b09) | Approved by Nigel |
| fix | 16:50:08 (logged) | this commit's time | Full suite 1481/1481 |
