# close-step: evidence, part 1, attempt 2

Pipeline: `feature` (method kit v0, P1 to P18), stage **verify, part 1, attempt 2**. Branch `nigel/close-step`, repo
Theseus, the feature worktree, HEAD `279692b`, compared with `origin/main` (`1c5d59e`,
`git diff origin/main...HEAD`). The fix pass under test is `bb9b753` (tests first) and `8cfc051`.

- **Verifier:** an independent sub-agent session that did not write the change. **Partial independence:** it was
  briefed by the implementing workstream's session and read `implementation.md` (including "Pass 2") and attempt 1's
  evidence.
- **Holdout `stop-with-handoff`: NOT run.** It waits for the verifier card (objective 1.0). Under P18 verify stays
  open regardless of this verdict; this covers part 1 only.
- **Times (read from the clock, UTC; the machine prints its zone as `GMTST`, equal to UTC):** started 2026-11-19
  14:41:42; finished 15:06:42 (the last clock reading before this file was completed).

## Results

| Check | Result |
|---|---|
| `dotnet test Theseus.slnx --nologo` in the worktree (14:42:05 to 14:46:36) | exit 0. **1614 passed, 0 failed, 0 skipped**, total 1614, 4 m 12 s |
| `dotnet build Theseus.slnx --no-incremental --nologo` in the worktree (14:47:11 to 14:47:26) | **0 warnings, 0 errors** |
| Worktree after both | `git status --short` empty (only `bin` / `obj` output written) |
| Stopping tests alone (in the copy, unmutated baseline) | 230 passed (attempt 1: 203; pass 2 adds 27) |

A clean build in the copy first failed with NU1900 (the package-vulnerability audit could not reach the internal
feed from a fresh restore); this is the environment, not the change. The worktree build above, which restores from
its existing assets, is the result. The copy was then restored with `-p:NuGetAudit=false` for the mutation runs only.

## Tests first (P9) and the changed tests (P12)

- `bb9b753` changes test files only (`git diff --name-status 5e4fe15 bb9b753`): adds `CloseNowTests.cs`,
  `ExitResultsMappingTests.cs`, `SecondPressNoticeTests.cs`, `StopViewTests.cs`; modifies `StopStageTextTests.cs`
  (one line).
- Extracted with `git archive bb9b753` into `copy2\bb9b753` and built `tests/Theseus.Tests`: **exit 1, 21 errors**,
  all CS0103 / CS1061 for `StopView`, `SecondPressNotice`, `CloseNowText` and `ExitsTyped.TryCloseNow`, which did not
  exist yet. **Pass.**
- **`StopStageTextTests`, "found" to "confirmed": a correction, not a weakening.** The test was written on this branch
  (`924e0e4`; it does not exist on `main`) and asserted the implementer's own word. Spec AC 9 names the outcome "Tab
  not confirmed" and `NudgeOutcome.TabNotConfirmed` means `FocusSession` did not confirm the tab, not that it was not
  found. The assertion is just as strict; only the expected words changed, to the spec's. It is recorded in
  `implementation.md` (Pass 2, F6) as P12 requires.
- **`StopViewTests` changed after its tests-first commit** (`8cfc051` splits
  `AHeldBackStop_NeverOffersCloseNow_AndSaysItWaitsForCloseThem` into two). The dropped expectation was that a
  held-back stop **with a failed `/exit`** shows the held-back text. Checked by reading that this state cannot occur:
  `_unconfirmed` is filled only in the `ShellViewModel` constructor (`ShellViewModel.cs:220-223`) while `_closed` is
  new and empty; `CloseOne` returns Skip for any `_unconfirmed` session before typing (`:1384-1389`); `ConfirmStops`
  and `AbandonStops` clear the whole set (`:1617`, `:1641`) and nothing adds to it again. The CanCloseNow half kept its
  assertion; the text half now uses a state that can occur. Judged a correction, and it is recorded in
  `implementation.md` ("Differences in pass 2"). No test that exists on `main` was changed.

## Attempt 1's findings, re-checked

| Finding | Status | Evidence |
|---|---|---|
| **F1** (must fix) card loses its stop after a sent `/exit`; "Typed /exit" never shown | **Fixed** | `ExitsTyped.Attempted` records a Sent attempt per session in `_closing` (`ExitsTyped.cs:56-60`); `Closing(sessionId, startedUtc)` returns it only when `attempt.AtUtc >= sessionStartedUtc` (`:68-69`). `StopView.Of` uses it when there is neither a waiting nor a delivered request (`StopView.cs:33-38`), giving `ExitTyped` at the attempt time, `CanCloseNow` false. The App passes the live session's `StartedUtc` (`ShellViewModel.cs:1502-1513`), which `SessionRegistryReader` reads from the registry's `startedAt` per process (`SessionRegistryReader.cs:63-72`), so a later run (a new process) has a later start and is not shown closing. After the session exits, `Sessions.Find` is null and the view is null. `SyncStopStates` runs at the end of every close pass (`:1378`), including the pass that typed `/exit`, so the card moves to "Typed /exit at HH:mm. Closing." at once and `ShowStop` (`IsLive && !IsStopping`) stays false. Tests: `StopViewTests.AfterExitIsSent_TheCardStaysClosing_ThoughTheRequestIsGone`, `ASessionResumedAfterItsExit_IsNoLongerClosing`. Mutations G01 and G05 caught. See new finding N1 for the one case it does not end |
| **F2** App guards untested (M19, M21, M22) | **Fixed for the guards that decide what is typed or offered** | `TryCloseNow`, `StopView`, `SecondPressNotice`, `CloseNowText` are in Core and tested; `ExitResults.From` is tested from the test project. M18, M19, M21, M22 now caught (table below). Still untested in the App, as `implementation.md` says: the timer stop (M20), the wiring arguments of `StopViewOf` and `BeginStop` (G12app, G13app, G14app, M22appb survive) |
| **F3** Close now notice gives the wrong cause | **Fixed** | `CloseNowText.NotClosed` gives the retire cause for Retire and makes no promise; for Wait and Skip it says "not idle and quiet yet" and that the button can be pressed again, which holds: `TryCloseNow` re-marks typed but leaves `LastAttempt` unchanged, so `MayCloseNow` stays true (`CloseNowTests.APressWhenTheSessionIsNotReady_LeavesNothingToRetryLater` asserts both). Residual causes of Skip ("session gone" between passes, handoff file removed) are rare and the button is hidden for both by the next pass |
| **F4** second-press notice does not name the session | **Fixed** | `SecondPressNotice.For` prefixes each stuck session's title (`SecondPressNotice.cs:30`). "Nothing to stop." can no longer follow it: `othersDescribed` is `stuck.Count < steps.Count`, and every other route in `BeginStop` increments a counter (`PasteYourself` returns before the notice) |
| **F5** retire log line reworded against AC 12 | **Accepted by the owner** (brief; `implementation.md` Pass 2) | `ShellViewModel.cs:1404-1406` unchanged since attempt 1 |
| **F6** "could not be found" | **Fixed** | `StopStageText.cs:47` "its tab could not be confirmed. Close now tries again." |
| **F7** held-back stop promises a 20 s close | **Fixed, with a new wording the spec did not list** | `StopStageText.For(StopView, ...)` (`StopStageText.cs:28-41`) says "Held back when the board opened. Close them, in the banner, finishes this stop." for a held-back stop not yet at an `/exit` stage; `StopView` never offers Close now for it. Judgement below |
| **F8** stopping icon is the Stop glyph in amber | **Known limit** (unchanged) | For the owner at the live check |
| **F9** drawer shows no stage or Close now | **Known limit** (unchanged) | The drawer's Stop follows `ShowStop`, so it too stays hidden after `/exit` is sent |

### The held-back wording (F7)

AC 6's list has five stages and no held-back one; the held-back flow itself is out of the spec's scope. Before pass 2
the card said "Closes once it has been quiet for 20 s." for a stop that will not close until the owner agrees, which
is false text a user acts on (P17). The new text names the real control: the banner's button is `Content="Close
them"` (`MainWindow.xaml:2005`), and the existing second-press notice already uses the same words ("Close them
finishes it, with every other held-back stop.", `ShellViewModel.cs:1058`). It is accurate and consistent. Because it
adds a user-facing string the approved spec does not contain, **the owner should accept it explicitly**, as with F5
(should-fix S1 below, a sign-off rather than a code change).

## Acceptance criteria

| AC | Result | Evidence |
|---|---|---|
| 1 Closed within 5 s of quiet | **Pass by test (Core) and reading (App); live check pending** | Unchanged since attempt 1: `Decide` Wait with `since + Quiet`, `CloseRecheck` +1 s, one `DispatcherTimer` (`ShellViewModel.cs:1461-1484`) |
| 2 Nothing scheduled when nothing waits; one re-check; no double typing | **Pass by test (Core) and reading (App)** | Unchanged. M20 (timer not stopped) remains App-only and untested; the live check covers the timer |
| 3 Close step only, UI thread | **Pass by reading** | Tick runs `CloseFinishedSessions` only; `DispatcherTimer` |
| 4 Four conditions unchanged | **Pass** | `ShouldClose` unchanged; `SessionClosingTests` pass |
| 5 Stopping icon from the ask until exit or retire; not pressable | **Pass by test (Core) and reading (App); rendering at the live check** | F1 fixed: after a sent `/exit` the view stays `ExitTyped` until the session is gone; `ShowStop` false for the card and the drawer; the amber `TextBlock` is not pressable. Caveat N1 (a sent `/exit` that does not end the session) |
| 6 Tooltip at the latest stage, local times | **Pass by test; one added wording (F7) for the owner to accept** | All five stages reachable now, including "Typed /exit at HH:mm. Closing." (`StopViewTests.AfterExitIsSent_...`). Times via `TimeZoneInfo.Local` in `SyncStopStates` |
| 7 A card with no stop is unchanged | **Pass by reading; rendering not checked (P11, live check step 1)** | With no request and no sent attempt for the running run the view's state is null: `IsStopping` false, tooltip empty, `CanCloseNow` false, so the markup is as in attempt 1 |
| 8 Retire judged by hand-over time | **Pass by test** | Unchanged; M01b and M02 were caught in attempt 1 and the code is unchanged |
| 9 No automatic retry; Close now only after nothing typed; never after Refused or Elevated | **Pass by test** | Now in Core: `TryCloseNow` attempts only when `AllowCloseNow` (G02 caught), re-marks typed on any non-Close verdict (M19 caught), `StopView` offers Close now only for TabNotConfirmed or FocusLost and never held back (M21, G03 caught), unknown outcomes map to Refused (M18 caught) |
| 10 Second-press notice says what the card says | **Pass by test (Core); App selection by reading** | `SecondPressNotice` composes the card's own `StopStageText` (M22, G09 to G11 caught). The App's choice of which sessions are stuck (`ShellViewModel.cs:961-963`) is untested (M22appb). Not reachable live (needs a failed `/exit`) |
| 11 `/exit` never typed twice once sent | **Pass by test** | `MayCloseNow` false after Sent; `AlreadyTyped` after Sent; unchanged |
| 12 Log lines | **Pass** | "Closing ..." unchanged; Wait and Skip log nothing; the retire rewording accepted by the owner (F5) |

## Text check (P17), word for word against the spec

| Text | Source | Result |
|---|---|---|
| "Asked at HH:mm. Waiting for its turn to end." | AC 6 | Matches |
| "Handed over at HH:mm. Writing its handoff." | AC 6 | Matches |
| "Handoff written at HH:mm. Closes once it has been quiet for 20 s." | AC 6 | Matches (`SessionClosing.Quiet` 20 s) |
| "Typed /exit at HH:mm. Closing." | AC 6 | Matches, and now reachable |
| "Could not type /exit: its tab could not be confirmed. Close now tries again." | AC 6, AC 9 "Tab not confirmed" | Matches (F6 fixed) |
| "Could not type /exit: the terminal was not in front. Close now tries again." | AC 9 "terminal not in front" | Matches |
| "Could not type /exit: Windows refused the keystrokes. Close it yourself." | AC 9 "Refused", *Close it yourself* | Matches |
| "Could not type /exit: the terminal runs as administrator and Theseus does not. Close it yourself." | AC 9 "blocked by elevation", *Close it yourself* | Matches |
| "Held back when the board opened. Close them, in the banner, finishes this stop." | Not in the spec; the banner button "Close them" (`MainWindow.xaml:2005`) | Accurate against the button; **owner to accept** (S1) |
| "{Title} was not closed: it is not idle and quiet yet. Press Close now again once it is." | AC 9 (Close now tries once, only when pressed, under the four conditions) | Accurate for Wait and Skip; the press can be repeated (`MayCloseNow` stays true) |
| "{Title} was not closed: its stop was handed to an earlier run of the session, so it has been retired." | AC 8 | Accurate: `CloseOne` retires the request on Retire (`ShellViewModel.cs:1396-1408`) |
| Second-press notice "{Title}: Could not type /exit: ..." then, if others, the usual description | AC 10 "says what the card says" | Matches: the card's own text, prefixed by the title |
| Close now button "Close now", tooltip "Type /exit into this session once more. Brings its terminal to the front." | AC 9; plan risks | Matches |
| Stopping icon `AutomationProperties.Name="Stopping"` | AC 5 "stopping icon" | Matches |
| `docs/cards.md` Stop and Close now rows | AC 1, 5, 6, 9 | Match. "cannot be pressed again" is now true until the session exits (F1 fixed). The rows do not mention the held-back wording; not required |
| Log "Retired the stop request for {Workstream}: it was handed to an earlier run of the session, not the one running now" | AC 12 | Reworded; accepted by the owner (F5) |

## Mutation (P16)

Copy: `<scratch>\close-step-verify\copy2\tree`
(robocopy of the worktree without `bin`, `obj`, `.git`; `diff -rq` of `src` and `tests` against the worktree empty
before the runs and after them). One mutation at a time, each file copied back from the worktree after its run.
Script `copy2\_verify\mutate.sh`, log `copy2\_verify\mutations.log`, one `.diff` and one `.txt` per mutation beside
it. Core mutations ran `Theseus.Tests.Stopping` (230); App mutations ran the whole test project (1614).

| # | Mutation (new place) | Result |
|---|---|---|
| M18 | `ExitResults.From`: unknown outcome mapped to FocusLost | **Caught**: `AnOutcomeNotYetKnown_IsNeverOneCloseNowRetries` (+1) |
| M19 | `ExitsTyped.TryCloseNow`: no re-mark after a non-Close verdict | **Caught**: `APressWhenTheSessionIsNotReady_LeavesNothingToRetryLater` (Skip, Wait, Retire) |
| M21 | `StopView`: Close now offered for any stop (`CanCloseNow = state is not null`) | **Caught**: 4 tests |
| M22 | `SecondPressNotice.For`: always the usual description (AC 10 branch removed) | **Caught**: 2 tests |
| M22appb | App `BeginStop`: the stuck check made always false (no session ever treated as stuck) | Survived (App wiring, untested; full suite 1614 pass) |
| M22app | App `BeginStop`: stuck check inverted to `state is null` | Did not build (CS8620, nullability); replaced by M22appb |
| G01 | `ExitsTyped.Closing`: run check removed | **Caught**: `ASessionResumedAfterItsExit_IsNoLongerClosing` |
| G01b | `Closing`: `>=` to `>` | Survived. Boundary only: an attempt at exactly the run's start millisecond cannot happen (the attempt is made to a session already running). Equivalent in practice |
| G02 | `TryCloseNow`: the not-offered return removed (allowance ignored) | **Caught**: `APressWhenNotOffered_AttemptsNothing` |
| G03 | `StopView`: `!heldBack` removed from Close now | **Caught**: `AHeldBackStop_NeverOffersCloseNow` |
| G04 | `StopView`: stage check (`Stage == ExitFailed`) removed from Close now | Survived. **Equivalent mutant:** `MayCloseNow(delivered)` is true only when `delivered` has a TabNotConfirmed or FocusLost attempt, and `StopState.Of` then always gives `ExitFailed` (a delivered request with a non-Sent last attempt takes that branch first, `StopState.cs:30-35`). The check is redundant |
| G05 | `StopView`: the sent-`/exit` (`Closing`) branch disabled | **Caught**: `AfterExitIsSent_TheCardStaysClosing_ThoughTheRequestIsGone` |
| G06 | `StopStageText.For(view)`: `HeldBack &&` removed | **Caught**: `AStopThatIsNotHeldBack_IsWordedByItsStage` |
| G07 | `StopStageText.For(view)`: the "not ExitTyped or ExitFailed" exclusion removed | Survived. Unreachable state: a held-back view is never at either stage (no `/exit` is tried while held back, and the `Closing` branch always passes `HeldBack` false). Equivalent in practice |
| G08 | `CloseNowText`: Retire branch never taken | **Caught**: `Retired_SaysTheStopWasAnEarlierRunsAndPromisesNothing` |
| G09 | `SecondPressNotice.IsStuck`: any state counts as stuck | **Caught**: `OnlyAFailedExit_CountsAsStuck` |
| G10 | `SecondPressNotice.For`: `othersDescribed` ignored, always appends | **Caught**: `AFailedExit_NamesTheSessionAndSaysWhatItsCardSays` |
| G11 | `SecondPressNotice.For`: `othersDescribed` ignored, never appends | **Caught**: `AFailedExitAmongOthers_IsFollowedByTheUsualDescription` |
| G12app | App `BeginStop`: `othersDescribed: true` always | Survived (App wiring, untested) |
| G13app | App `StopViewOf`: `heldBack` passed as false | Survived (App wiring, untested) |
| G14app | App `StopViewOf`: session start passed as `DateTimeOffset.MinValue` (every later run would show closing) | Survived (App wiring, untested) |

**Every guard pass 2 added in Core is caught, except two equivalent or unreachable ones (G04, G07) and a boundary
(G01b).** M18, M19, M21 and M22 from attempt 1 are now caught in their new places. What survives is App wiring: the
arguments `StopViewOf` and `BeginStop` pass into the tested Core (G12app, G13app, G14app, M22appb) and, from attempt 1,
the timer stop (M20, not re-run: its code is unchanged). Of these, G14app matters most, since a wrong start time would
undo F1's "a later run is not shown closing"; it is a one-argument call (`session.StartedUtc`), checked by reading.

## New findings

### Must fix

None.

### Should fix

- **S1. The owner should accept the held-back wording** (`src/Theseus.Core/Stopping/StopStageText.cs:41`), a
  user-facing string the approved spec does not list (AC 6). It is accurate (see above); this is a sign-off, not a code
  change.

### Consider

- **N1. A sent `/exit` that does not end the session leaves the card "Closing" for the rest of that run.**
  `ExitsTyped.Closing` has no expiry (`ExitsTyped.cs:69-70`): if the keystrokes landed but the session did not exit
  (for example, it took them as input while not at its prompt), the card shows "Typed /exit at HH:mm. Closing."
  and no Stop icon on the card or in the drawer until the process ends or the board restarts (`_closing` is in memory).
  This matches AC 5 ("until the session exits") and AC 11 forbids a second `/exit`, so it is not a defect against the
  spec, but the card then offers nothing. Stop selected still works for it (the request was retired, so `BeginStop`
  asks again; this route is unchanged from `main`). Worth a line in `docs/cards.md` or a known limit.
- **N2. Stop selected during the closing window asks the session again.** After a sent `/exit` the request is retired,
  so `BeginStop` (`ShellViewModel.cs:944-948`) sees neither a waiting nor a delivered request and routes a new ask
  (for an idle session, a nudge typed into a terminal that is exiting). The card hides Stop during this window, but the
  toolbar and menu do not consult `IsStopping`. Unchanged from `main` and outside AC 10's wording; noted because F1
  now makes "closing" a visible state that the multi-select path ignores.
- **N3.** `StopView`'s stage check in `CanCloseNow` (`StopView.cs:41-42`) is redundant (G04) and `StopStageText`'s stage
  exclusion (`StopStageText.cs:40`) is unreachable (G07). Harmless; both read as intent.

## Live-check steps for the owner (not run; each checked against the code)

On a released build of `nigel/close-step`, with a scratch card whose session is idle and has nothing else to do. The
Theseus log is `%LOCALAPPDATA%\Theseus\logs\theseus-<yyyyMMdd>.log` (`TheseusLog.cs:35`, `theseus-.log` rolled daily,
under the App's data directory `logs`). Record the screen for steps 2 to 4: the closing window is seconds long.

1. **A card with no stop (AC 7).** Compare the scratch card and one other live card with the same cards on a
   main-branch board (screenshots side by side). Expected: identical: Stop icon present, no amber icon, no Close now
   button, same spacing.
2. **Stop and touch nothing (AC 1, 5, 6).** Press the card's Stop, then do not touch the board, F5 or any other card.
   Expected: the Stop icon is replaced at once by an amber icon that does nothing when clicked, and the drawer shows no
   Stop. Hovering shows "Asked at HH:mm. Waiting for its turn to end." while the request waits, then "Handed over at
   HH:mm. Writing its handoff.", then "Handoff written at HH:mm. Closes once it has been quiet for 20 s." (this may lag
   until the next pass, as the plan's risks say).
3. **The close time (AC 1, 12).** In the log, find "Closing <workstream>, which was asked to stop and has handed over:
   Sent". Expected: no more than 25 s after the session went idle (its status-since time in its registry file under
   `%USERPROFILE%\.claude\sessions\`), with no unrelated refresh in between, followed by "Stop of <workstream>
   delivered after ...". Nothing is logged for the waiting pass.
4. **Between `/exit` and exit (AC 5, 6; F1).** From the moment the terminal is raised until the tab closes. Expected:
   the card keeps the amber icon, its tooltip reads "Typed /exit at HH:mm. Closing." (HH:mm the local time of the
   "Closing ..." log line), no Stop icon on the card or in the drawer, no Close now. Once the session has exited and
   the board has refreshed, the card is not live and shows neither icon (`ShowStop` needs `IsLive`).
5. **The next run is not shown closing (F1).** Press Play on the same card to start a new run. Expected: an ordinary
   live card with the Stop icon, no amber icon. (The new process has its own registry `startedAt`, later than the
   attempt.)
6. **Second press (AC 10, only in part).** During step 2 (before the `/exit`), select the card and use Stop selected.
   Expected: "That session already has its stop and is finishing it; it closes once it has handed over and gone
   quiet." Do **not** use Stop selected during step 4's window: it asks the session again (N2). The AC 10 notice for a
   failed `/exit` cannot be produced on demand and rests on `SecondPressNoticeTests`.
7. **A held-back stop (F7), if the owner wants to see it.** Stop a second scratch card and, once its tooltip says
   "Handed over" or "Handoff written" and before it closes, quit Theseus (its `OnExit`, `App.xaml.cs:319-351`, disposes
   its services and has no step that ends sessions; whether the launcher's dispose touches running sessions was not
   traced, so check the session's tab is still open). Reopen Theseus within the request's 4-hour expiry. Expected: the
   banner with **Close them** and **Leave them running**; the card amber, tooltip "Held back when the board opened.
   Close them, in the banner, finishes this stop.", and no Close now. Press **Close them**. Expected: "Closing the
   session that was asked to stop." and, once it is quiet, the "Closing ...: Sent" log line, then step 4's states.
8. **Not reachable live:** V1 / AC 8 (needs a session resumed outside the board's Start), every failed-`/exit` path and
   Close now (AC 9), and the AC 10 failure notice. These rest on the Core tests, now including the Close now sequence
   and the card's offer (`CloseNowTests`, `StopViewTests`).

## Verdict, part 1, attempt 2

**PASS** for part 1. Build (0 warnings), the full suite (1614 of 1614), tests-first for pass 2, and the AC table pass;
F1 is fixed and tested, and every Core guard pass 2 adds is caught by a test except two equivalent mutants and one
boundary. One should-fix is a sign-off: the owner accepts the held-back wording (S1). Surviving App-wiring mutations
(G12app, G13app, G14app, M22appb, and M20 from attempt 1) are the known untested App layer. **The verify stage stays
open** until the holdout `stop-with-handoff` has been run by the verifier card (P18), and the live check is still to
be done.

## Notes on this verification

- Files written: this file, and inside `...\scratchpad\close-step-verify\copy2\` only (`tree\`, `bb9b753\`,
  `_verify\`). Nothing was deleted. The background-task output files the tool harness keeps under
  `...\233623bc-2f72-4934-ba2b-c4a52669d270\tasks\` are written by the harness for every background command, not by
  this verification; they are named here so the owner is not surprised by them.
- `dotnet test` and `dotnet build` in the worktree wrote their usual `bin` / `obj` output there; `git status` stayed
  clean.
