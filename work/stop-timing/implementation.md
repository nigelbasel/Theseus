# stop-timing: implementation

Pipeline: `feature` (method kit v0), stage **implement**, autonomy L0. Branch `main`, repo Theseus.
Follows `plan.md` (approved by Nigel, 2026-11-18).

## What was done

| File | Change |
|---|---|
| `src/Theseus.Core/Stopping/StopProgress.cs` | New enum: never delivered, delivered with no handoff, handed over but not closed |
| `src/Theseus.Core/Stopping/StopRemoval.cs` | New enum: started again, newer session, declined, expired |
| `src/Theseus.Core/Stopping/StopDurations.cs` | New record of a finished stop's durations; null where a time is missing |
| `src/Theseus.Core/Stopping/StopTiming.cs` | New: `Finished`, `Progress`, `Describe`, and `ReportFinished` / `ReportUnfinished`, which read the handoff's write time and write the log lines |
| `src/Theseus.Core/Stopping/PendingStopRequest.cs` | `DeliveredUtc`, optional |
| `src/Theseus.Core/Stopping/StopRequestStore.cs` | `Claim` stamps `DeliveredUtc` after the move, in its own try/catch that returns the unstamped request on failure; `Sweep` reports each expired request; the class comment no longer claims the delivery time was on disk (a move keeps the file's original time) |
| `src/Theseus.Core/Abstractions/IStopRequestStore.cs` | `Claim(sessionId, deliveredUtc = null)` |
| `src/Theseus.Hook/Program.cs` | Passes `DateTimeOffset.UtcNow` to `Claim` |
| `src/Theseus.App/ViewModels/ShellViewModel.cs` | Reports at the five places a request ends: finished close (`ReportFinished`, only when `/exit` was `Sent`), newer session, start-again withdraw, start-again retire, declined at startup |
| `tests/Theseus.Tests/Stopping/StopTimingTests.cs` | New: 10 tests |
| `tests/Theseus.Tests/Stopping/StopRequestStoreTests.cs` | 2 tests added (claim records the delivery time; an older claim has none); existing tests untouched |

## Order, as the plan asked

1. The new tests were written first and failed to build, because nothing they used existed yet (logged under
   `<stop-timing>` at "implement step 1-2").
2. Core types, `Claim`, `Sweep`, then the Hook and App wiring.
3. Full suite: **1448 of 1448 pass**, 0 warnings (the build treats warnings as errors). The first run failed to
   compile on a local name clash in `ShellViewModel` (`waiting`); renamed to `undelivered`, then green.

## Where it differs from the plan, and why

- **`StopDurations` has five fields, not four, and `HandoffToExit` is nullable.** The handoff's write time can fail
  to read (the file moved or locked) at the moment of closing. Rather than guess, the total is still given and the
  handoff figures are missing. `RequestToHandoff` was added for the line that has no delivery time (AC 4), which
  needs it.
- **`Claim`'s new parameter has a default** instead of being required. Every existing caller and test stays
  unchanged, and a caller that passes nothing behaves as before.
- **No new test that `Sweep` logs one line per request.** Log output is not captured in the test suite. The
  existing `Sweep` tests still prove it removes and counts both kinds of expired request; the per-request line is
  for the live verify to read.
- **The old Debug line "Discarded N stop request(s)" is gone,** replaced by one Information line per request
  (AC 5). Nothing read that line.

## Pass 2 (after verify part 1, `evidence-part1-attempt1.md`)

Part 1 passed, with two gaps and five notes. Nigel chose to fix F1-F3 now and the notes where trivial.

| Finding | What was wrong | Done |
|---|---|---|
| F1 (gap) | When the handoff's time could not be read, the finished line said "delivery time not recorded" even when it was | `FinishedText` names each missing time on its own; tested for all four combinations |
| F2 (note) | The plan said the stamp would be written with `AtomicFile`; it was written with `File.WriteAllText`, and **this implementation note did not say so**. A reader could see half a file; a just-removed request could be written back | `AtomicFile`, and only while the delivered file still exists. A window of microseconds between that check and the write remains |
| F3 (gap) | No test of the stamp failing, of the wording and choice of line, or of a real older request file (the "older hook" test wrote the current shape) | Tests added for each: a read-only request still delivers without a stamp; every wording; a hand-written JSON file in the old shape |
| F4 (note) | Two `Retire` results ignored | The newer-session report now happens only if the retire succeeded. The finished report still does not depend on it (a known limit, below) |
| F5 (note) | The stamp's catch was narrower than "nothing escapes" | It now catches everything; the comment says why |
| F6 (note) | Negative figures shown without a flag | The line ends "(a negative figure means the clock or a file time moved)" |
| F7 (note) | "60.0m" for 59.98 minutes, "0.0m" for under a second, the reason shown as a code name; the plan promised queryable numbers | Ages truncate: whole seconds under 90 s, whole minutes under 90 m, then hours to one decimal. Reasons in words. The lines are now one sentence in a single `{Outcome}` property, so the wording can be tested; **the plan's "structured properties, so the log can be queried" is withdrawn**, and figures are read from the text |

Full suite after pass 2: **1465 of 1465**, 0 warnings. The new tests failed to build before the fix (logged at
"implement pass 2").

## For the verifier and reviewer

- The fail-safe in `Claim` is now tested (a read-only request file). A failed atomic write can leave a `.tmp`
  file beside the delivered request; it is not read as a request.
- If the `Retire` after a finished close fails, that request stays on disk, and a later start of the card reports
  it as unfinished as well. The store already logs the failed retire as a warning.
- The installed hook (`Theseus.Hook.exe`, shipped with the app) only changes when Theseus is updated. Until then,
  finished stops log "delivery time not recorded" (AC 4).
- The App wiring has no unit tests. The live check in `spec.md` covers it.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| implement | 2026-11-18 14:03:32 (logged) | 14:08:32 (commit a565dd7) | Full suite 1448/1448 |
| verify part 1, attempt 1 | 14:14:34 | 14:19:18 | Sub-agent: 156,570 tokens, 24 tool calls. Passed with gaps F1-F3 |
| implement, pass 2 | 14:22:18 (logged) | this commit's time | Full suite 1465/1465 |
