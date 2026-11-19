# close-step: research

Pipeline: `feature` (method kit v0, P1 to P18), stage **research**, autonomy L0. Project `theseus-bootstrap`, phase 1,
scheduled as its first pull request (Nigel, 2026-11-19). Branch `nigel/close-step`, repo Theseus only.

The findings this covers, from earlier work:

| Id | Finding | From |
|---|---|---|
| L1 | A close waits for an unrelated refresh: 87.3 s, 112.5 s, 71.2 s, and once 11 minutes, against a 20 s quiet period | `work/stop-timing/evidence.md`, `work/resumed-stop/evidence.md` |
| L2 | A card shows no sign that a stop is under way, which invites a second press | `work/stop-timing/evidence.md` |
| V1 / S2 | The close step judges a stop by when it was **asked**; the Stop press judges by when it was **handed over**. A stop handed to a session resumed outside the board's Start is retired, not closed | `work/resumed-stop/review.md` |
| V2 | A second press on a stop whose `/exit` was tried but did not land still says it will close | `work/resumed-stop/review.md` |

## How the close step works today

- `ShellViewModel.CloseFinishedSessions` (`src/Theseus.App/ViewModels/ShellViewModel.cs:1351`) types `/exit` into a
  session that was handed a stop, has written its handoff since, and has been idle for `SessionClosing.Quiet`
  (20 s, `src/Theseus.Core/Stopping/SessionClosing.cs:26`). The decision is `SessionClosing.ShouldClose` (`:35`).
- It runs in exactly two places: at the end of the board's refresh (`ShellViewModel.cs:1307`) and when held-back
  stops are confirmed (`:1509`). **Nothing else calls it, and no timer does.**
- The board refreshes when the session registry changes (`SessionRegistryWatcher`, a `FileSystemWatcher` on the
  registry's `*.json`, wired in `App.xaml.cs:151-155`), and on edits, links, F5 and the Refresh button.

## L1: the cause

A session writes the registry when its status changes, including the moment it goes **idle**. That write triggers a
refresh, which runs the close step. But `statusSinceUtc` is that same moment, so the quiet check (`now - since >=
20 s`) always fails on the refresh the session's own change caused. After that the session is idle and writes
nothing, so **nothing runs the close step again** until something unrelated refreshes the board.

Measured, not assumed (P15), in the live checks already recorded:
- An idle session writes nothing to the registry: no refresh, and no log line, from 15:23:18 to 15:34:20 on
  2026-11-18 (`work/stop-timing/evidence.md`, "Stays idle, 2nd").
- Every close that happened came at an unrelated refresh: a banner press, Focus plus F5, another session's change.

So the 20 s quiet period is not slow; it is never re-checked. The delay is however long the board happens to stay
untouched.

### Options for L1

| Option | What it does | Trade-off |
|---|---|---|
| **A. A one-shot re-check when quiet is the only thing missing** (recommended) | When the close step finds a session that meets every condition except quiet, it schedules itself once for the moment quiet will be reached (plus a small margin), on the UI thread. Runs only the close step, not a full refresh | Exact, and costs nothing when no stop is pending. One timer, replaced when a nearer time is found |
| B. A periodic tick while any stop is pending | Every few seconds while a request is waiting or delivered | Simpler to reason about; a close lands up to one tick late |
| C. Drop or shorten the quiet period | | Rejected: quiet is what stops a session that paused to think from being closed (`SessionClosing.cs:22-25`) |

A full refresh is not the thing to schedule: refreshes have taken seconds on this machine (`ShellViewModel.cs:641`).
The close step needs only what it already reads: the session snapshot (unchanged while the session is idle, since
any change refreshes anyway) and the handoff file's time (read fresh each time).

## L2: what the card could show

Today the card's Stop icon (`MainWindow.xaml:639-644`) stays as it is from the press until the session exits. All the
facts needed to show progress already exist: the request on disk (`StopRequestStore`: waiting or delivered, with
`RequestedUtc` and `DeliveredUtc`), the handoff's written time (`StopTiming.HandoffWrittenUtc`), the session's status
and `StatusSinceUtc`, and whether `/exit` was typed (`ExitsTyped`). `StopProgress` already names the stages for the
log (`src/Theseus.Core/Stopping/StopProgress.cs`).

A shape to decide at spec: while a stop is under way, the Stop icon is replaced by a "stopping" state (the board's
amber, `#D68910`, as for a paused card), disabled, with a tooltip that says where it has got to, in the order a stop
happens: *asked, waiting for its turn to end* → *handed over* → *handoff written* → *closing once quiet (in N s)* →
*could not type /exit: <reason>*. A second press then has nothing to press, which removes the invitation L2
describes; the banner notice for "already has its stop" stays for Stop selected.

## V1 / S2: one rule for "which run was this stop for"

`BeginStop` decides by `SessionClosing.HandedBefore` (delivery time, falling back to request time) since
resumed-stop's review; the close step still uses `SessionClosing.StartedSince` (request time) at
`ShellViewModel.cs:1364`. So the same stop can be "this run's" to the press and "an earlier run's" to the close
step, which retires it. Using `HandedBefore` in the close step too gives one rule. `StartedSince` then has one
remaining caller (`StopRequestStore.Write`, `StopRequestStore.cs:45`, where request time is right: a waiting request
has not been handed to anyone).

## V2: an /exit that did not land

When `TabNudge.Nudge` returns anything but `Sent` (`TabNotConfirmed`, `FocusLost`, `Refused`, `BlockedByElevation`,
`src/Theseus.App/Services/NudgeOutcome.cs`), nothing was typed, but `_closed.Typed` was already recorded
(`ShellViewModel.cs:1385`), so the close step never tries again in this board run, and the notice still says the
session will close. The request is left on disk for `Sweep` to expire.

Options for spec: retry the outcomes where nothing was typed, on the same one-shot re-check as L1 and a limited
number of times; or never retry and show "could not type /exit: <reason>; close it yourself" on the card. Which
outcomes are safe to retry depends on what `TabNudge` does to the window when it tries (bringing a terminal to
the front repeatedly would be worse than not closing). **Not yet checked: read `TabNudge` before the spec**
(P4, P15). `BlockedByElevation` cannot succeed on a retry and should only ever be shown.

## Out of scope

- The parked-cores blink: separate small change, after this one (Nigel, 2026-11-19).
- Theseus's own Stop route choices (`StopRouting`), the hook, and how a session is asked to stop: unchanged.
- The four close conditions themselves (asked, idle, handoff since the request, quiet): unchanged.

## Holdout scenarios that apply (by name only)

`stop-with-handoff`. Under P18, verify cannot pass until a verifier has run it, which needs objective 1.0's verifier
card. **So close-step's verify will stay open until the verifier card exists**, unless Nigel decides otherwise at
spec.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| research | logged at the stage start in `C:\Users\nigel\.claude\logs\git-22.log` | this commit's time | Code read in the worktree at 1c5d59e |
