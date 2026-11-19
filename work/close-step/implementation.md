# close-step: implementation

Pipeline: `feature` (method kit v0, P1 to P18), stage **implement**, autonomy L0. Branch `nigel/close-step`.

## What was done

| Plan step | Done | Commit |
|---|---|---|
| 1 Tests first | Four new files (P9): `CloseDecisionTests`, `CloseRecheckTests`, `ExitResultTests`, `StopStageTextTests`. They failed to build before step 2 (`CloseDecision`, `ExitResult` and the rest did not exist) | 924e0e4 |
| 2 to 5 Core | `CloseVerdict`, `CloseDecision`, `SessionClosing.Decide`, `CloseRecheck`, `ExitResult`, `ExitAttempt`, `ExitsTyped` (attempts, `MayCloseNow`, `AllowCloseNow`), `StopStage`, `StopState`, `StopStageText`. The stopping tests passed, 203 of 203 | 221b554 |
| 6 to 8 App | `CloseFinishedSessions` acts on `Decide` through `CloseOne`; one `DispatcherTimer` (`ArmCloseRecheck`) for the earliest wait, running the close step only; `SyncStopStates` sets each card's stopping state after every pass; `CloseNow`; `ExitResults.From`; the card's `IsStopping`, `ShowStop`, `StopTooltip`, `CanCloseNow`, `CloseNowCommand`; the amber stopping icon and Close now in `MainWindow.xaml`; the second-press notice | this stage's commit |
| 9 | Full suite 1587 of 1587, no warnings. `docs/cards.md` updated | this stage's commit |

## Every difference from the plan (P3)

1. **The research was partly wrong about L2.** The card already had a status line while a request **waits**
   (`WorkstreamCardViewModel.StopPending`, "stopping — waiting for the turn it was given to end"), set in
   `ShellViewModel`'s lifecycle sync. It disappears once the request is delivered, which is where the "no sign" came
   from. Left as it is; the new icon and tooltip cover every stage.
2. **The drawer's Stop button** (card details) also hides while a stop is under way (`ShowStop`), not only the card's
   icon. The plan named only the card. Same rule, so the two cannot disagree.
3. **Close now re-checks before typing.** If the session is not ready to close at the moment of the press (no longer
   idle, or not quiet), nothing is typed, the stop is marked tried again so nothing retries it later on its own, and
   the banner says why. The plan said "tries once"; without this, the allowance given by the press would have let the
   timer retry later, unasked, which AC 9 forbids.
4. **The retire log line's wording** changed to match the rule ("handed to an earlier run"), since it now decides by
   hand-over time.
5. **`ExitsTyped.Finished` is no longer called directly** by the close step; `Attempted` records it for a sent
   attempt. The method stays, used by its existing tests.
6. **The second-press notice** shows the card's failure text for each such session, then the usual description for
   the rest, or only the failure text when every selected session is in that state.
7. **An outcome the mapping does not know** is treated as `Refused`, so it is never retried.

## Not done, as planned (P13)

- No test reaches the App code: the timer, `CloseOne`, `SyncStopStates`, `CloseNow`, the notice, the XAML. The live
  check covers what it can (plan, "What the live check covers").
- V1 and the failed-`/exit` paths rest on Core tests.

## Known limits

- **The tooltip can lag** between passes, as the plan's risks say: a pass runs on every refresh, on the timer, and on
  Close now.
- **A timer-driven close raises the terminal**, as every close always has; it now happens about 20 s after the session
  goes quiet rather than at an unpredictable later refresh.

## Pass 2 (verify part 1 failed; Nigel chose option A, 2026-11-19)

| Finding | Disposition |
|---|---|
| F1 (must fix) | **Fixed.** `ExitsTyped` remembers the sent `/exit` per session (`Closing`), scoped to the run now running, so the card stays "Typed /exit … Closing." after the request is retired, and Stop does not come back while the session closes. A later run of the same session is not shown as closing |
| F2 | **Fixed, mostly.** The card's stop view (`StopView.Of`), the Close now attempt (`ExitsTyped.TryCloseNow`), its notice (`CloseNowText`), the second-press notice (`SecondPressNotice`) and the `/exit` mapping (`ExitResultsMappingTests`) are now tested. Left in the App, untested: the timer being stopped (M20) and the one-line choice in `BeginStop` to look for a stuck session; the live check covers the timer |
| F3 | **Fixed.** Close now's notice says why it did not close, and promises a second press only where one can help |
| F5 | **Accepted by Nigel:** the retire log line keeps its new wording ("handed to an earlier run") |
| F6 | **Fixed.** "Could not be confirmed", as the spec says. One existing test changed (P12): `StopStageTextTests` asserted my wrong word, "found" |
| F7 | **Fixed.** A stop held back when the board opened says "Held back when the board opened. Close them, in the banner, finishes this stop." and never offers Close now. The spec's AC 6 list did not have this stage; added because the 20 s promise was false for it (P17). **Wording accepted by Nigel, 2026-11-19** (attempt 2's S1) |
| F4 | **Fixed in passing:** the second-press notice names each stuck session |
| F8, F9 | **Known limits:** the stopping icon is the Stop glyph in amber; the details drawer hides Stop while stopping but shows neither the stage nor Close now |

Differences in pass 2: a new test combined "held back" with a failed `/exit`, which cannot occur together (the close
step skips held-back sessions, and the record of `/exit` attempts is empty at each start). It failed, was split into
two tests of states that can occur, and the code was not changed for it. Full suite 1614 of 1614, no warnings.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| implement | logged at the stage start in the workstream log | this stage's commit | |
