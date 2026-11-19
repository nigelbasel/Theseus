# close-step: review

Pipeline: `feature` (method kit v0), stage **review**, autonomy L0. Branch `nigel/close-step`, pull request #1.
Reviewer: a fresh sub-agent started by the implementer (partial independence: it shares the implementer's model and
was briefed by it, but did not write or verify the change). Read only; no build or test was run.

Scope: `git diff origin/main...HEAD -- src tests docs` at `fb538f5`, against `spec.md` AC 1 to 12, `.agents/reviewer-rubric.md`
and `AGENTS.md`. Limits already accepted and recorded (F5 log wording, F7 held-back wording, F8/F9 icon glyph and
drawer) are not re-reported.

| Started | Finished |
|---|---|
| 2026-11-19 16:31:44 (clock) | 2026-11-19 16:37:11 (clock) |

## Rubric

| Item | Verdict | Where |
|---|---|---|
| R1 Board changes pass the person | Not touched | |
| R2 A report is a claim | Not touched | |
| R3 Nothing starts a session on its own | Holds | The timer only runs the close step (`ShellViewModel.cs:1473-1477`); nothing in the change launches |
| R4 No tracker writes, no session durations | Not touched | |
| R5 Only the owner acts on others' locks | Not touched | |
| R6 Lock book survives restart | Not touched | |
| R7 Clean up only what is proved | Not touched | |
| R8 Token only to its requester | Not touched | |
| R9 Nothing destructive at startup | Holds | Held-back sessions are skipped by `CloseOne` (`ShellViewModel.cs:1384-1386`), so neither a refresh nor the timer closes them; `StopView.Of` never offers Close now for them (`StopView.cs:41-42`) |
| R10 Not silent about visible state | Holds | The change adds visibility: amber icon and tooltip for every stage |
| R11 Do not start a session with nothing to read | Not touched | |
| R12 Sessions learn state from a file | Not touched | |
| R13 Paused/blocked are marks | Not touched | Pause unchanged |
| R14 Briefs and reports relative | Not touched | |
| R15 Nothing secret; updates offered | Not touched | |
| R16 Conventional Windows behaviour | **Breaks (doubt)** | Close now exists only on the card (`MainWindow.xaml:653-656`): not in the menu bar's Workstream menu (`MainWindow.xaml:1208-1229`) nor the card's context menu (`MainWindow.xaml:371-398`). See S2 |
| R17 Live state applies as it changes | Holds | No dialog or Save added; the tooltip is live state |
| R18 A card does not grow without bound | Holds | Close now and the stopping icon sit in the icon row; one fixed-height button, at most one per card |
| R19 Lock colours exact | Not touched | |

## Findings

### Must fix

None.

### Should fix

- **S1. The timer decides on a session snapshot up to ~21 s old.** `ShellViewModel.cs:1473-1477` calls
  `CloseFinishedSessions` directly, and `CloseOne` reads `Sessions.Find(...)` (`ShellViewModel.cs:1384`, `1393`), which is
  the snapshot from the last full refresh. Before this change the close step ran inside `Refresh` right after
  `Sessions.Refresh`, so "idle" was read fresh. Now a session that leaves idle in the last ~0.5 s before the tick (the
  registry watcher debounces 400 ms, `SessionRegistryWatcher.cs:25`, then posts a refresh) is still idle in the
  snapshot, and `/exit` is typed into a session that has begun a turn. The pid guard in `TabNudge` protects against the
  wrong *run*, but not against a stale *status*. AC 4 ("the four close conditions are unchanged") is weakened in its
  idle condition; the comment in `SessionClosing.cs:43-51` says why that condition matters. Smallest fix: re-read the
  sessions (`Timed("sessions", Sessions.Refresh)`, the one step, not the full board refresh AC 3 forbids) at the start of
  the tick.
- **S2. Close now is a command outside the menu bar (R16).** It appears only as a word button on the card
  (`MainWindow.xaml:653-656`). R16 *breaks if* a command appears only outside the menu bar. Focus is a pre-existing
  precedent of the same shape, so the owner may decide this is acceptable for per-card actions; that decision should
  then be recorded, dated, in the change (as the rubric requires) or Close now added to the card's context menu and the
  Workstream menu.
- **S3. `ExitsTyped` now holds four overlapping records and an orchestration step.** `_finished`
  (`ExitsTyped.cs:14`) is now exactly "the last attempt was `Sent`" (`ExitsTyped.cs:57-60`), so `IsFinished` can be
  `LastAttempt(request)?.Result == ExitResult.Sent` and `Finished` (`ExitsTyped.cs:38-43`), which production no longer
  calls (implementation.md difference 5), can go. `AllowCloseNow` (`ExitsTyped.cs:113-118`) is public only for
  `TryCloseNow` and a test. `TryCloseNow` taking a `Func<CloseDecision>` (`ExitsTyped.cs:79-97`) makes a record of what
  was typed also the sequencer of the Close now press. Behaviour is right; the type has grown past its doc comment
  ("the stops Theseus has already typed /exit for"). Simplicity (AGENTS.md "Simplicity First"; the brief's question).
- **S4. Machine paths and an internal workstream id are committed to the public repo.** `work/close-step/research.md:104`,
  `plan.md:49`, `implementation.md:68` name the owner's user-profile log path and its workstream slug;
  `evidence-part1-attempt1.md:4`, `:78`, `:202`, `:205` and `evidence-part1-attempt2.md:4`, `:114` name the worktree
  path and a session-scoped scratch path under the user profile. Outside `src tests docs`, but in the pull request.
  AGENTS.md, "The repo is public": no machine paths; use placeholders such as `<root>\<feature>\<repo>`.

### Consider

- **C1. Close now that fails again says nothing in the banner.** `ShellViewModel.cs:1527-1530` sets a notice only for a
  verdict other than `Close`; a `Close` whose nudge is `FocusLost` again changes only the tooltip's time. The owner
  pressed, the terminal flashed, and the card looks the same.
- **C2. `CloseNowText` blames "not idle and quiet" for every `Skip`.** `CloseNowText.cs:11` is also shown when the
  session has gone or has no session id (`CloseOne`, `ShellViewModel.cs:1384-1388`), where "Press Close now again"
  is false.
- **C3. `StopState`, `StopView` and `StopStageText` overlap.** The `ExitTyped` state is built in two places
  (`StopView.cs:27-29`, `StopState.cs:75-77`); `StopState.Of` is only used through `StopView.Of` and tests, and could
  fold into it. `SecondPressNotice.IsStuck` (`SecondPressNotice.cs:11`) restates `Stage == ExitFailed`, also checked
  in `StopView.cs:42`. `StopStageText` has two `For` overloads, one of which re-checks `State is null` already checked by
  its caller (`ShellViewModel.cs:1495`). None is wrong; together they are three types and two factories for one card
  state.
- **C4. `CloseRecheck` is a static class for a minimum plus a constant** (`CloseRecheck.cs:14-29`). It is tested and
  harmless; `SessionClosing` already owns `Quiet` and could own the margin and the minimum.
- **C5. `ShouldClose` and `Decide`.** Not duplicated: `Decide` reuses `ShouldClose` and asks it again at
  `since + Quiet` (`SessionClosing.cs:101-110`), which is what proves `Wait` is always in the future and the timer
  cannot spin. That property is worth one sentence in `Decide`'s doc comment, since it is the reason the timer is safe.
- **C6. The timer is created lazily on whichever thread first reaches a `Wait`** (`ShellViewModel.cs:1470-1472`).
  Every caller today is the UI thread, so it holds; constructing it in the constructor, or passing the UI dispatcher,
  would make that independent of who calls `Refresh`.
- **C7. Comments cite undated decisions.** `MainWindow.xaml:647` ("close-step AC 5 and 6"), `CloseNowText.cs:5`
  ("verify F3"), `StopView.cs:6`, `SecondPressNotice.cs:6`, `ExitsTyped.cs:67`, `CloseRecheck.cs:5`, `ExitResult.cs:4`.
  AGENTS.md asks for dated citations; `CloseVerdict.cs:3` and `StopStageText.cs:4` show the form. "on this machine"
  (`ShellViewModel.cs:1458-1459`) means nothing to a reader of the public repo.
- **C8. Test files and messages.** `ExitResultTests` tests `ExitsTyped` (attempts, `MayCloseNow`, `AllowCloseNow`), not
  `ExitResult`; `StopStageTextTests` also carries the stage derivation of `StopState.Of` (`StopStageTextTests.cs:15-56`).
  `Times_AreShownInTheMachinesOwnZone` (`StopStageTextTests.cs:79`) passes an explicit zone, so it proves "the given
  zone", not the machine's. Several tests assert two behaviours (`CloseDecisionTests.cs:35-39`,
  `ExitResultTests.cs:78-82`, `SecondPressNoticeTests.cs:163-169`). Assertion messages cite mutation ids ("verify M19",
  "verify M21") a future reader cannot look up. No test file is named after a fix pass; none found that cannot fail.
- **C9. `docs/cards.md:30-31`.** "cannot be pressed again" is true of the card, not of Stop selected (verify N2, not
  recorded as accepted). The Close now row does not say that it types nothing unless the session is idle and quiet at
  that moment, which is what the banner then says.
- **C10. `BeginStop` finds the card by a hand-written key match** (`ShellViewModel.cs:961`) where `Board.Card(key)`
  exists.

## Checked and holding

- **/exit twice, or into the wrong run:** `CloseOne` records `Typed` before the nudge (`ShellViewModel.cs:1415`), so even
  a re-entrant pass during the nudge's sleep sees it as tried. A stop handed to an earlier run is retired by
  hand-over time (`SessionClosing.cs:96-99`, AC 8). `Closing` is scoped to the run now running (`ExitsTyped.cs:69-70`).
- **No unasked retry:** a failed attempt counts as typed (`ExitsTyped.cs:54`); only Close now removes it, only for
  `TabNotConfirmed`/`FocusLost`, and a press that finds the session not ready puts it back (`ExitsTyped.cs:91-94`).
  An unknown nudge outcome maps to `Refused` (`ExitResults.cs:71`).
- **Timer cannot spin or leak:** `Wait` only when `ShouldClose(now)` is false and `ShouldClose(since + Quiet)` true, so
  `QuietEndsUtc > now`; plus a 1 s margin. One timer, stopped on every tick and when nothing waits.
- **UI thread:** the tick runs at Background priority, behind any posted refresh; nothing new blocks beyond the nudge
  that already ran on the UI thread. Extra per-pass work is small file reads (`StopViewOf`, `ShellViewModel.cs:1502-1513`).
- **AC 7:** `ShowStop = IsLive && !IsStopping` (`WorkstreamCardViewModel.cs`), `IsStopping` false whenever no view.
- **AC 12:** "Closing ..." line unchanged; `Wait` and `Skip` log nothing.

## Summary

The change does what the spec asks, and its central safety properties hold: `/exit` is never typed twice, never into a
later run, and never retried without a press. The timer cannot spin. There is no must-fix. It can be accepted once S1 is
fixed or explicitly accepted by the owner: the timer acts on a session status that can be stale by the length of the
registry debounce, where before it was read fresh. S2 needs a dated owner decision or a menu entry. S4 is a
public-repo clean-up of the work artefacts. S3 is simplification and can follow.

## What was done with each finding (Nigel chose option A, 2026-11-19)

| Finding | Disposition |
|---|---|
| S1 | **Fixed.** The timer's tick and a Close now press re-read the sessions (`Sessions.Refresh`, not a full board refresh) before the close step decides, so it never acts on a snapshot up to about 20 s old. App code, untested like the rest of the App wiring; checked by the re-verify |
| S2 | **Decision for Nigel**, asked next: Close now in the menus, or button-only like Focus, recorded dated |
| S3 | **Recorded as a follow-up simplification:** `ExitsTyped` keeps overlapping records (`_finished` can be derived from the last attempt) and holds the Close now orchestration (`TryCloseNow`). Behaviour is right and tested |
| S4 | **Fixed for this pull request:** machine paths and the card id removed from `work/close-step/` (worktree, scratch and log paths replaced by descriptions or `<scratch>`). The same problem in the stage 0 work files already on `main` is a separate decision for Nigel |
| docs wording | **Fixed:** `docs/cards.md` no longer says the icon "cannot be pressed again" (Stop selected still works), and says Close now types only if the session is idle and quiet |
| Considers | Recorded as they are |

Full suite 1614 of 1614.
