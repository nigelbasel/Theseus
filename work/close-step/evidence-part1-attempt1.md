# close-step: evidence, part 1

Pipeline: `feature` (method kit v0, P1 to P18), stage **verify, part 1**. Branch `nigel/close-step`, repo Theseus,
the feature worktree, HEAD `5e4fe15`, compared with `origin/main` (`git diff origin/main...HEAD`).

- **Verifier:** an independent sub-agent session that did not write the change. **Partial independence:** it was
  briefed by the implementing workstream's session and read the implementer's plan and implementation notes.
- **Holdout `stop-with-handoff`: NOT run.** It waits for the verifier card (objective 1.0). Under P18 verify cannot
  pass until an independent verifier has run it, so this verdict covers part 1 only and the stage stays open.
- **Times (read from the clock, UTC; the machine's zone printed as `GMTST`, equal to UTC):** started 2026-11-19
  13:19:41; findings complete 13:42:39 (the last clock reading before this file was written).

## Build and tests

| Check | Result |
|---|---|
| `dotnet test Theseus.slnx --nologo` in the worktree (13:20:06 to 13:22:50) | exit 0. **1587 passed, 0 failed, 0 skipped**, total 1587, 2 m 12 s |
| Warnings | That run was incremental, so it would print none either way. A `dotnet build Theseus.slnx --no-incremental` of an identical copy of HEAD (copy folder below; `diff -rq` of `src` and `tests` against the worktree empty) gave **0 warnings, 0 errors** |
| Stopping tests alone (in the copy) | 203 tests in `Theseus.Tests.Stopping` |

## Tests first (P9)

- `924e0e4` adds exactly four files, all tests: `tests/Theseus.Tests/Stopping/CloseDecisionTests.cs`,
  `CloseRecheckTests.cs`, `ExitResultTests.cs`, `StopStageTextTests.cs` (`git show --name-status`).
- Extracted with `git archive 924e0e4` into the copy folder and built `tests/Theseus.Tests`: **exit 1, 15 errors**,
  all CS0103 / CS0246 for `ExitResult`, `CloseDecision`, `StopState` (types that did not exist yet).
- The Core commit `221b554` follows it. `git diff 924e0e4 HEAD -- tests` is empty: the tests were not changed after
  they were written. No existing test was changed (P12 not engaged).

**Pass.**

## Acceptance criteria

| AC | Result | Evidence |
|---|---|---|
| 1 Closed within 5 s of quiet, no other activity | **Pass by test (Core) and by reading (App); live check pending** | `Decide` returns Wait with `since + Quiet` (`CloseDecisionTests.ASessionThatOnlyLacksQuiet_WaitsUntilItsQuietPeriodEnds`); `CloseRecheck` adds 1 s (margin test bounds it at 5 s). `ArmCloseRecheck` (`ShellViewModel.cs:1466`) arms one `DispatcherTimer` for it; the tick runs `CloseFinishedSessions`. |
| 2 Nothing scheduled when nothing waits; one re-check; no double typing | **Pass by test (Core) and reading (App)** | `CloseRecheck.Next([])` is null and the timer is stopped (`:1470`). One field `_closeRecheck`, stopped and re-armed each pass. Typing is blocked by `ExitsTyped.AlreadyTyped`. Mutation M20 (timer not stopped) is caught by no test: App code is untested, as the plan says. |
| 3 Re-check runs the close step only, on the UI thread | **Pass by reading** | The tick calls `CloseFinishedSessions` only. The timer is a `DispatcherTimer` created in the first pass, which runs inside `Refresh` / `SyncLifecycles`, already UI-bound. |
| 4 Four conditions unchanged | **Pass** | `ShouldClose` is unchanged in the diff; `Decide` calls it; the existing `SessionClosingTests` pass. |
| 5 Stopping icon from the ask until exit or retire; cannot be pressed | **FAIL** | The icon is a `TextBlock` (not pressable) in `#D68910` (`MainWindow.xaml:649-652`), shown by `IsStopping`. But on a sent `/exit`, `CloseOne` retires the request at once (`ShellViewModel.cs:1436`), so `SyncStopStates` finds neither a waiting nor a delivered request (`:1516-1518`), `IsStopping` goes false and **the pressable Stop icon returns while the session is still running**, until it exits. See F1. |
| 6 Tooltip at the latest stage, local times | **FAIL (one stage unreachable)** | The wording of all five matches the spec (text check below). Asked, Handed over, Handoff written and Could not type are reachable. **"Typed /exit at HH:mm. Closing." can never be shown**: `StopState.Of` needs the delivered request to reach its `lastAttempt` branch, and the App has removed it by then (F1). `StopStageTextTests.ASentExit_IsClosing` passes only because it hands `Of` a delivered request the App never has. |
| 7 A card with no stop looks and behaves as today | **Pass by reading; rendering not checked (P11 needs the live check)** | With `IsStopping` false: `ShowStop == IsLive` (the old binding), the stopping `TextBlock` and Close now are `Collapsed` (`BoolToVisibilityConverter`), so they take no space. `ShowStop` is raised wherever `IsLive` is. The drawer's Stop follows the same rule. |
| 8 Retire judged by hand-over time | **Pass by test** | `Decide` uses `HandedBefore`; `AStopHandedToAnEarlierRun_IsRetired`, `AStopAskedBeforeAResumeButHandedToIt_IsClosedNotRetired`; mutations M01b and M02 caught. |
| 9 No automatic retry; Close now only after nothing typed; never after Refused or Elevated | **Pass by test (Core) and reading (App)** | `Attempted` records Typed for every result; `Decide` gives Skip (not Wait) once `alreadyAsked`, so the timer cannot retry. `AllowCloseNow` clears the typed mark only when `MayCloseNow` (TabNotConfirmed or FocusLost). `CloseNow` re-marks it typed when the press finds the session not ready (`:1537`), and runs synchronously on the UI thread, so the timer cannot slip in between. Unknown outcomes map to Refused. App mutations M18, M19, M21 survive: untested, as planned. |
| 10 Second-press notice says what the card says after a failed attempt | **Pass by reading; reachable by neither tests nor the live check** | `BeginStop` adds the card's failure text for an AlreadyDelivered stop whose last attempt was not Sent (`:961-967`). Mutation M22 (branch removed) survives. The notice does not name the session (F4). |
| 11 `/exit` never typed twice once sent | **Pass by test** | `AlreadyTyped` after Sent; `MayCloseNow` false after Sent; existing `ExitsTypedTests` pass; M05, M06, M11 caught. |
| 12 Log lines | **Pass, with one wording change** | "Closing {Workstream}, which was asked to stop and has handed over: {Outcome}" unchanged; Wait and Skip log nothing. The retire line was reworded (implementation difference 4), while AC 12 says existing lines stay. See F5. |

## Differences from the plan (implementation.md)

| # | Difference | Judgement |
|---|---|---|
| 1 | Existing `StopPending` status line left | Acceptable |
| 2 | Drawer Stop hidden while stopping | Acceptable (same rule). The drawer shows no stopping state or Close now (F9) |
| 3 | Close now re-checks before typing, re-marks typed if not ready | Acceptable, and needed for AC 9. Its notice gives one cause for every non-Close verdict (F3) |
| 4 | Retire log wording changed | Needs the owner's acceptance against AC 12 (F5) |
| 5 | `Finished` called through `Attempted` | Acceptable; M10b caught |
| 6 | Notice composition for several sessions | Acceptable; see F4 |
| 7 | Unknown outcome treated as Refused | Acceptable, the safe side; M18 (unknown mapped to FocusLost) caught by no test |

## Text check (P17), against the spec, word for word

| Text | Source | Result |
|---|---|---|
| "Asked at HH:mm. Waiting for its turn to end." | AC 6 | Matches |
| "Handed over at HH:mm. Writing its handoff." | AC 6 | Matches |
| "Handoff written at HH:mm. Closes once it has been quiet for 20 s." | AC 6 | Matches (20 from `SessionClosing.Quiet`) |
| "Typed /exit at HH:mm. Closing." | AC 6 | Matches, but never shown (F1) |
| "Could not type /exit: <reason>." | AC 6, 9 | Prefix matches. "its tab could not be found. Close now tries again.": the spec and `NudgeOutcome` say the tab could not be **confirmed** (`FocusSession` did not return `TabSelected`), not that it was not found (F6). "the terminal was not in front. Close now tries again." matches "terminal not in front". "Windows refused the keystrokes. Close it yourself." and "the terminal runs as administrator and Theseus does not. Close it yourself." match AC 9's *Close it yourself* and what the outcomes mean in `TabNudge.Send` (access denied means elevation) |
| Close now tooltip "Type /exit into this session once more. Brings its terminal to the front." | Plan, risks: "Its tooltip says it will bring the terminal forward" | Matches |
| Close now not-ready notice "{Title} was not closed: it is no longer idle and quiet. Close now can be pressed again once it is." | Implementation difference 3 | **The cause can be wrong** (F3) |
| Second-press notice | AC 10: "says what the card says" | Same text as the card's failure tooltip; see F4 |
| `docs/cards.md`, Stop row: "quiet for 20 seconds"; "While the stop is under way the icon turns amber and cannot be pressed again; hover over it to see how far the stop has got." | AC 1, 5, 6 | Matches the spec; "cannot be pressed again" is not true after a sent `/exit` (F1) |
| `docs/cards.md`, Close now row | AC 9 | Matches: only when nothing was typed; tries once more and raises the terminal; refused or administrator says close it yourself |

## Mutation (P16)

Done in a copy, `<scratch>\close-step-verify\copy`
(robocopy of the worktree without `bin`, `obj`, `.git`), one mutation at a time, each file restored from the worktree
after its run (`diff -rq` of `src` against the worktree empty afterwards). Script and per-run logs are in `copy\_verify\`.
Core mutations ran the `Theseus.Tests.Stopping` tests (203); App mutations ran the full suite (1587).

| # | Mutation | Result |
|---|---|---|
| M01 / M01b | `Decide`: HandedBefore retire removed (M01, as `if (false)`, did not build: CS0162; M01b non-constant) | Caught: `AStopHandedToAnEarlierRun_IsRetired` |
| M02 | Retire judged by `StartedSince` (asked time) | Caught: `AStopAskedBeforeAResumeButHandedToIt_IsClosedNotRetired` |
| M03 | Wait branch removed | Caught: `ASessionThatOnlyLacksQuiet_WaitsUntilItsQuietPeriodEnds` |
| M04 | Wait whenever `statusSince` is known | Caught: 5 tests |
| M05 | Wait ignores `alreadyAsked` | Caught: `AStopAlreadyTypedFor_IsNeitherClosedAgainNorWaitedFor` |
| M06 | `MayCloseNow` result filter removed | Caught: 4 tests |
| M07 | `MayCloseNow` also offers Refused | Caught: 2 tests |
| M08 | `MayCloseNow` drops FocusLost | Caught: 2 tests |
| M09 | `AllowCloseNow` without its `MayCloseNow` check | Caught: `CloseNow_IsRefusedWhenNotOffered` |
| M10 / M10b | Sent no longer finishes the stop (M10 CS0162; M10b non-constant) | Caught: `ASentAttempt_FinishesTheStop` |
| M11 | `Attempted` no longer records Typed | Caught: 3 tests |
| M12 | `StopState`: handoff "since the request" check removed | Caught: `AHandoffFromBeforeTheRequest_DoesNotCount` |
| M13 | `StopState`: Sent shown as failed | Caught: 5 tests |
| M14 / M14b | `StopState`: last-attempt branch removed | Caught: 5 tests |
| M15 | `CloseRecheck`: margin not added | Caught: 2 tests |
| M16 | `CloseRecheck`: latest instead of earliest | Caught: `SeveralWaiting_OnlyTheEarliestIsScheduled` |
| M17 | Margin 6 s | Caught: `TheMargin_KeepsTheCloseWithinFiveSecondsOfQuiet` |
| M18 | App `ExitResults.From`: default mapped to FocusLost (retryable) | **Survived** (1587 pass) |
| M19 | App `CloseNow`: a not-ready press does not re-mark typed (would let the timer retry unasked, AC 9) | **Survived** |
| M20 | App `ArmCloseRecheck`: timer not stopped when nothing waits | **Survived** |
| M21 | App `SyncStopStates`: Close now offered for any stop | **Survived** |
| M22 | App `BeginStop`: AC 10 branch removed | **Survived** |

Every Core guard is caught. Every App guard survives: no test reaches the App, as `implementation.md` says. M19 and
M21 guard AC 9, the one criterion where a wrong answer types into a terminal unasked; M22 is AC 10, which plan step 8
said would be tested "if it can be reached without the window" (it was not attempted).

## Findings

### Must fix

- **F1. After a sent `/exit` the card loses its stopping state before the session exits, and "Typed /exit at HH:mm.
  Closing." is never shown.** `src/Theseus.App/ViewModels/ShellViewModel.cs:1436` retires the delivered request on
  Sent; `SyncStopStates` / `StopStateOf` (`:1493-1519`) build the state only from the waiting and delivered requests,
  so they give null, `IsStopping` is false, and the pressable Stop icon (and the drawer's Stop) return until the
  session's exit refreshes the board. Breaks AC 5 (until the session exits), AC 6 (fourth stage) and `docs/cards.md`'s
  "cannot be pressed again". The Core test `StopStageTextTests.ASentExit_IsClosing` does not reflect what the App
  passes. A fix needs the sent attempt to be found without the delivered file (for example by keeping, per session,
  the request the attempt was made for until the session is gone), with a test of that in a new file.

### Should fix

- **F2. AC 9's App guards have no test** (M19, M21 survive), nor has AC 10's (M22). The plan accepted the App as
  untested and named it (P13), but AC 9 is the criterion whose failure types into the owner's terminal. Moving
  `CloseNow`'s allow, re-check and re-mark sequence, and the notice composition, into Core would make them testable.
  Owner's call.
- **F3. Close now's not-ready notice can give the wrong cause.** `ShellViewModel.cs:1538` says "it is no longer idle
  and quiet" for every verdict other than Close, but `CloseOne` also returns non-Close when the stop was handed to an
  earlier run (Retire: the request is then retired, so "can be pressed again" is false too), when the session is not
  in the snapshot, or when the stop is held back (`_unconfirmed`). P17: a cause the user acts on.
- **F5. AC 12 says the existing log lines stay; the retire line was reworded** (`ShellViewModel.cs:1409`,
  implementation difference 4). The new words are more accurate. The owner should accept the change explicitly. No
  code or test in the repo matches the old text.

### Consider

- **F4.** The second-press notice (`ShellViewModel.cs:966`, `:1039-1043`) does not name the session, so with several
  selected the owner cannot tell which one could not be closed. If the stuck sessions are not every step and the
  others fall in no counter, `Describe` gives "Nothing to stop." and the notice reads "Could not type /exit: ...
  Nothing to stop." (not confirmed reachable).
- **F6.** "its tab could not be found" (`src/Theseus.Core/Stopping/StopStageText.cs:29`): the outcome is "could not be
  **confirmed**" (spec AC 9, `NudgeOutcome.TabNotConfirmed`).
- **F7.** A held-back stop (`_unconfirmed`, before the owner confirms at startup) shows "Handoff written at HH:mm.
  Closes once it has been quiet for 20 s." although it will not close until confirmed: `StopStateOf` does not consult
  `_unconfirmed`.
- **F8.** The "stopping icon" is the Stop glyph (`&#xE71A;`) in amber; the spec says the Stop icon "is replaced by a
  stopping icon". Whether the same glyph in amber reads as stopping is for the owner at the live check.
- **F9.** The drawer hides Stop while stopping but shows neither the stage nor Close now.

### Timer and double typing, by reading

- **No spin.** `Decide` returns Wait only when `ShouldClose` fails at `now` and holds at `since + Quiet`. The two calls
  differ only in the quiet check, so Wait implies `now < since + Quiet`, and the due time (`since + 21 s`) is in the
  future when it is decided. If the pass itself takes over a second the interval is clamped to zero, fires once, and
  the next pass gives Close or Skip, not Wait. After an attempt `alreadyAsked` is true and both calls fail: Skip.
- **No second `/exit`.** Every attempt is marked typed before `TabNudge` runs (`:1420`) and again by `Attempted`; only
  `AllowCloseNow` clears it, only after a nothing-typed result, and only from the press.

## Live-check steps for the owner (not run; each checked against the code)

On a released build of `nigel/close-step`, with a scratch card whose session is idle and has nothing else to do. The
Theseus log is `%LOCALAPPDATA%\Theseus\logs\theseus-<yyyyMMdd>.log`.

1. **A card with no stop (AC 7).** Before stopping, compare the scratch card and one other live card with the same
   cards on a main-branch board (screenshots side by side). Expected: identical: Stop icon present, no amber icon, no
   Close now button, same spacing.
2. **Stop and touch nothing (AC 1, 5, 6).** Press the card's Stop, then do not touch the board, F5 or any other card.
   Expected: the Stop icon is replaced at once by an amber icon that does nothing when clicked. Hovering shows "Asked
   at HH:mm. Waiting for its turn to end." while the request waits, then "Handed over at HH:mm. Writing its handoff."
   after the turn ends, then "Handoff written at HH:mm. Closes once it has been quiet for 20 s." (this one can lag
   until the next pass, as the plan's risks say).
3. **The close time (AC 1, 12).** In the log, find "Closing <workstream>, which was asked to stop and has handed over:
   Sent". Expected: no more than 25 s after the session went idle (its status-since time in its registry file under
   `%USERPROFILE%\.claude\sessions\`), with no unrelated refresh in between, followed by "Stop of <workstream>
   delivered after ...". Nothing is logged for the waiting pass.
4. **Between /exit and exit (AC 5, 6).** Watch the card from the moment the terminal is raised. **Expected by the
   spec:** "Typed /exit at HH:mm. Closing." and no Stop icon. **Expected by the code (F1):** the ordinary Stop icon is
   back until the session exits, and "Typed /exit" never appears. This step should fail until F1 is fixed. The window
   is short (the session exits within seconds), so a screen recording is the reliable way to see it.
5. **Second press (AC 10, only in part).** During step 2, select the card and use Stop selected (toolbar or menu).
   Expected: "That session already has its stop and is finishing it; it closes once it has handed over and gone
   quiet." The new AC 10 notice needs a failed `/exit`, which cannot be produced on demand (plan, P13), so AC 10 rests
   on reading only (and has no test: M22). The plan's "a second Stop selected gives the new notice" is therefore not
   reachable live.
6. **Not reachable live:** V1 / AC 8 (needs a session resumed outside the board's Start), and every failed-`/exit` path
   and Close now (AC 9). These rest on the Core tests and on reading; their App wiring has no test (F2).

## Verdict, part 1

**FAIL.** Build, the full suite (1587 of 1587, 0 warnings), tests-first and every Core mutation pass, but AC 5 and AC 6
fail by reading (F1): after a sent `/exit` the card drops its stopping state and offers Stop again before the session
exits, and the "Typed /exit" stage can never be shown. Separately, the stage cannot pass until the holdout
`stop-with-handoff` has been run by the verifier card (P18).

## Notes on this verification

- Files written besides this one: build and test logs and the mutation script, all under the scratchpad
  (`<scratch>\fullsuite.txt`,
  `<scratch>\close-step-verify\build-924e0e4.txt`, `<scratch>\close-step-verify\build-head-clean.txt`, and
  `copy\_verify\`). Two of the logs sit beside the copy folder rather than inside it. Nothing was deleted. The
  workstream log was not written, since the brief allowed only this file.
- `dotnet test` in the worktree wrote its usual `bin` / `obj` output there.
