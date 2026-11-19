# stop-timing: plan

Pipeline: `feature` (method kit v0), stage **plan**, autonomy L0. Branch `main`, repo Theseus.
Implements `spec.md` (approved by Nigel, 2026-11-18). One repo; no change to how stopping behaves.

## Design

Behaviour lives in `Theseus.Core`, where it can be tested; the App only calls it at the moments a stop ends.

| Type (new or changed) | File | What it does |
|---|---|---|
| `PendingStopRequest` (changed) | `Core/Stopping/PendingStopRequest.cs` | Gains `DateTimeOffset? DeliveredUtc { get; init; }`. Null on requests written before this change, and on any whose stamp could not be written |
| `IStopRequestStore.Claim` / `StopRequestStore.Claim` (changed) | `Core/Abstractions/IStopRequestStore.cs`, `Core/Stopping/StopRequestStore.cs` | `Claim(sessionId, deliveredUtc)`. After moving the file to `delivered\` (unchanged), it rewrites it with `DeliveredUtc` through `AtomicFile`, inside its own try/catch. If the rewrite fails, it returns the request as claimed, without the stamp (AC 1, 2) |
| `Theseus.Hook` (changed) | `Hook/Program.cs` | Passes `DateTimeOffset.UtcNow` to `Claim`. Nothing else in the hook changes |
| `StopProgress` (new enum) | `Core/Stopping/StopProgress.cs` | `NeverDelivered`, `DeliveredNoHandoff`, `HandedOverNotClosed` |
| `StopRemoval` (new enum) | `Core/Stopping/StopRemoval.cs` | Why an unfinished request was removed: `StartedAgain`, `NewerSession`, `Declined`, `Expired` |
| `StopDurations` (new record) | `Core/Stopping/StopDurations.cs` | `RequestToDelivery?`, `DeliveryToHandoff?`, `HandoffToExit`, `Total`, all `TimeSpan`; null where a time is missing |
| `StopTiming` (new static) | `Core/Stopping/StopTiming.cs` | `Finished(request, handoffWrittenUtc, exitUtc)` gives `StopDurations`. `Progress(request, delivered, handoffWrittenUtc)` gives `StopProgress`: not delivered, then `NeverDelivered`; delivered with no handoff written at or after `RequestedUtc`, then `DeliveredNoHandoff`; otherwise `HandedOverNotClosed`. `ReportFinished` and `ReportUnfinished` write the log lines (below), reading the handoff file's write time themselves |
| `StopRequestStore.Sweep` (changed) | `Core/Stopping/StopRequestStore.cs` | Reports each expired request through `StopTiming.ReportUnfinished(..., StopRemoval.Expired)` before removing it, instead of one Debug count. Still returns the count |
| `ShellViewModel` (changed) | `App/ViewModels/ShellViewModel.cs` | At each removal site, reads the request before removing it and reports it: `CloseFinishedSessions` after `/exit` is `Sent`, `ReportFinished`; the same method's newer-session retire, `NewerSession`; start-again `Cancel` and `Retire`, `StartedAgain`; `AbandonStops`, `Declined`. The `Retire` after a `Sent` close reports nothing more (AC 6) |

## The log lines

Information level, in the Theseus log. Seconds to one decimal place. Values are structured properties, so the
log can be queried.

```
Stop of {Workstream} finished in {Total}s: delivered after {ToDelivery}s, handoff {ToHandoff}s later, /exit {ToExit}s after that
Stop of {Workstream} finished in {Total}s: delivery time not recorded, handoff {RequestToHandoff}s after the request, /exit {ToExit}s after that
Stop of {Workstream} did not finish ({Removal}, {Age} after it was asked): {Progress}
```

`{Progress}` is worded for a person: "never delivered: the session did not end a turn", "delivered, but no handoff
was written", "handed over, but /exit was never typed".

## Steps, tests first

1. `StopTimingTests` (new, `tests/Theseus.Tests/Stopping`), failing first:
   - durations of a finished stop with a delivery time; without one (delivery fields null, total still right);
   - each of the three progress answers, including a handoff written before the request counting as no handoff;
   - a handoff exactly at the request time counting as written (the same rule `SessionClosing` uses).
   Verified by: they fail before `StopTiming` exists, and pass after.
2. `StopRequestStoreTests` additions: `Claim` records the delivery time it is given; a request written by the
   older shape (no `DeliveredUtc`) still parses; `Sweep` still removes and counts expired requests in both folders.
   Verified by: the new ones fail first; every existing store test still passes unchanged.
3. Implement the Core types, then `Claim`, then `Sweep`. Verified by: steps 1 and 2 pass.
4. Wire `Theseus.Hook` and the four `ShellViewModel` sites. Verified by: the full suite passes, with 0 warnings.
5. Write `implementation.md`: what was done, what differs from this plan, and why.

## Risks

- **The hook is a separate process, shipped in the installer.** Until the installer updates it, requests are
  claimed without a stamp; AC 4 covers that. A failure while stamping must not fail a delivery; that path is a
  try/catch that is hard to make fail in a test, so the reviewer reads it.
- **Reporting at the `ShellViewModel` sites means reading each request before removing it.** A read that fails
  just skips the report; it never blocks the removal.
- **The handoff's write time is the file system's.** A handoff touched again later (for example an edit after
  the stop) moves it. That only makes a duration longer, never invents one, and is noted in the line's meaning,
  not hidden.
- **No App-level tests exist for these call sites.** They are checked by the live verify on a running board
  (spec, "How it will be verified").

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| plan | 2026-11-18 13:59:53 (logged) | this commit's time | Gate: Nigel approves this plan |
