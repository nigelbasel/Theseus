# stop-timing: implementation

Pipeline: `feature` (method kit v0), stage **implement**, autonomy L0. Card git-22, branch `main`, repo Theseus.
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

## For the verifier and reviewer

- The fail-safe in `Claim` (a stamp that cannot be written) is a try/catch that returns the unstamped request. It
  is hard to force in a test; read it.
- The installed hook (`Theseus.Hook.exe`, shipped with the app) only changes when Theseus is updated. Until then,
  finished stops log "delivery time not recorded" (AC 4).
- The App wiring has no unit tests. The live check in `spec.md` covers it.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| implement | 2026-11-18 14:03:32 (logged) | this commit's time | Full suite 1448/1448 |
