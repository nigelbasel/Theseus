# close-step: re-verify of the review fixes

Pipeline: `feature` (method kit v0), stage **re-verify of review fixes**. Branch `nigel/close-step`, commit `e136ac4`
(`git show HEAD` only). Verifier: an independent sub-agent that did not write the fixes. Read against `review.md`
(S1 to S4 and their dispositions) and `spec.md` (AC 1 to 12).

| Started | Finished |
|---|---|
| 2026-11-19 16:42:37 (clock) | 2026-11-19 16:44:55 (clock, before this file was written) |

## 1. S1: sessions re-read before the timed close and the Close now press

**Result: fixed. The stale window the review described is gone.**

- The tick (`src/Theseus.App/ViewModels/ShellViewModel.cs:1473-1481`) now stops the timer, calls `Sessions.Refresh()`,
  then `CloseFinishedSessions()`. `CloseNow` (`ShellViewModel.cs:1531-1532`) calls `Sessions.Refresh()` before
  `TryCloseNow`. `CloseOne` reads `Sessions.Find(...)`, so status and status-since come from the registry as read at
  that moment, not from the last full refresh.
- `SessionsViewModel.Refresh` reads the registry files directly (`_registry.Read()`), so the 400 ms watcher debounce
  the review named no longer stands between a session going busy and the decision. What is left is the time between
  that read and the keystroke (console title reads for each live session, then the nudge's own settling). That is the
  same window the close step had before this feature, when it ran inside the full refresh after `Sessions.Refresh`.
  It cannot be closed by any pre-check and is not a regression.
- Re-entrancy: none added. `TabNudge` blocks with `Thread.Sleep` and pumps no messages, so nothing else runs on the UI
  thread mid-pass. The only subscriber to `Sessions.Refreshed` is the jump list (`App.xaml.cs:250`), which does
  nothing when the list is unchanged (`SessionJumpList.cs:30`). The timer is stopped before the re-read and re-armed
  at the end of `CloseFinishedSessions`, as before.
- Exceptions: no new path. `SessionRegistryReader.Read` retries and catches JSON and IO errors; the same call already
  runs in every full refresh. If something did throw, the timer would stay stopped until the next full refresh
  re-arms it, which fails safe (no `/exit` typed).
- UI-thread cost: one registry read and one title read per live session, only when a re-check is due or Close now is
  pressed. Small and rare.
- Consistency: the re-read updates the sessions list but not the cards' lifecycle (`SyncLifecycles` is not run). See
  C1 below; it does not affect what is typed.
- AC 3 ("the close step only, not a full board refresh"): **holds.** The tick runs one step of the full refresh, the
  sessions re-read, which the close step needs as input. It does not run `Board.Refresh`, `SyncLifecycles`, `Regroup`,
  pull requests, stops, account or `Sweep`. The review recommended exactly this split.
- AC 4 (the four close conditions unchanged): restored to its pre-feature strength for the idle condition.

## 2. S4: no machine paths or card id under `work/close-step/`

**Result: not fully fixed.** No drive-letter path, no user-profile path, no worktree path and no `git-22` remains in
any file, `review.md` included. The full scratch paths were replaced by `<scratch>`. But two path fragments remain
that the replacement missed, and one of them carries the session id:

- `work/close-step/evidence-part1-attempt2.md:232`: `` `...\<session-id>\tasks\` `` with the real session GUID
  of the verifying session in place of `<session-id>` (it is the session-scoped scratch root).
- `work/close-step/evidence-part1-attempt1.md:203` (`...\scratchpad\close-step-verify\build-924e0e4.txt`,
  `...\build-head-clean.txt`) and `work/close-step/evidence-part1-attempt2.md:230`
  (`...\scratchpad\close-step-verify\copy2\`): elided scratch paths, not the `<scratch>` placeholder used elsewhere in
  the same files.

Acceptable as they are: `%USERPROFILE%\.claude\sessions\` and `%LOCALAPPDATA%\Theseus\logs\...` are product locations
written with environment variables, not paths on one machine; the branch name and the public repo's own GitHub URLs in
`evidence.md`.

Side effect of the replacement: `evidence-part1-attempt1.md:204-205` now reads "The workstream log in the workstream
log was not written", a duplicated phrase.

## 3. `docs/cards.md`: Stop and Close now rows

Rows as committed (`docs/cards.md:30-31`):

> □ **Stop** | Asks the session to write its handover notes when its turn ends. When the notes appear and the session
> has been quiet for 20 seconds, Theseus types `/exit` into the tab so it closes. Nothing is ever killed: a killed
> session writes nothing. While the stop is under way the card's icon turns amber and does nothing when clicked; hover
> over it to see how far the stop has got.

> **Close now** | Appears only when Theseus could not type `/exit` and nothing was typed, for example because the
> terminal was not in front. It tries once more, bringing the terminal to the front, and types only if the session is
> idle and quiet at that moment. If Windows refused the keystrokes, or the terminal runs as administrator, the amber
> icon says to close the session yourself instead.

**Result: both rows match the spec and the code.**

- "20 seconds": `SessionClosing.Quiet`, shown as "20 s" in the tooltip (`StopStageText.cs:22`); AC 1, 4.
- "does nothing when clicked; hover over it": the stopping icon is a `TextBlock` with a tooltip, not a button
  (`MainWindow.xaml:649-652`), amber `#D68910`; AC 5, 6. It no longer claims Stop cannot be pressed at all, so the
  menu's Stop selected is not contradicted (review C9).
- Close now "only when ... nothing was typed": offered only for `TabNotConfirmed` and `FocusLost`
  (`ExitsTyped.cs:110`); AC 9.
- "types only if the session is idle and quiet at that moment": `TryCloseNow` runs `CloseOne` under the same
  conditions; asked and handoff written already hold for any stop that reached a failed `/exit`; AC 9.
- "the amber icon says to close the session yourself": `StopStageText.Reason` for `Refused` and `Elevated`
  ("... Close it yourself."); AC 9.

## 4. Build and full suite

`dotnet test Theseus.slnx --nologo` in the feature worktree: build succeeded, **1614 passed, 0 failed, 0 skipped,
1614 total** (1 m 50 s), exit code 0. No warning lines in the output. The build step was incremental, so a warning in
a project it did not recompile would not have been printed; the changed project (`Theseus.App`) was rebuilt. The
three "launch ticket" lines are test output, not failures.

## Findings

### Must fix

None.

### Should fix

- **V1. A session id is still in a committed path.** `work/close-step/evidence-part1-attempt2.md:232` keeps the
  verifying session's GUID in a scratch path fragment. This is the same public-repo problem as S4, which the
  disposition reports as fixed. Replace with `<scratch>\..\tasks\` or a plain description. While there, make the
  elided scratch fragments at `evidence-part1-attempt1.md:203` and `evidence-part1-attempt2.md:230` use `<scratch>`
  like the rest of those files, and repair the duplicated phrase at `evidence-part1-attempt1.md:204-205`.

### Consider

- **C1. Brief Stop icon on a card whose session has just gone.** After the tick's re-read, a session that exited is
  missing from `Sessions`, so `SyncStopStates` clears `IsStopping` while `card.IsLive` still holds the value from the
  last `SyncLifecycles`. `ShowStop = IsLive && !IsStopping` (`WorkstreamCardViewModel.cs:248`) can then show a
  pressable Stop on a dead card until the next full refresh. The registry watcher posts that refresh when the entry
  goes, so the window is short, and Stop on a gone session types nothing.
- **C2. The re-read is untested.** The disposition says so, matching the rest of the App wiring. No test would fail if
  either `Sessions.Refresh()` call were removed.
- **C3. New comments cite an undated decision** ("close-step review S1", `ShellViewModel.cs:1477-1478`, `1531`), the
  same form as review C7.

## Verdict

**PASS**, with one should-fix (V1). S1 is fixed and adds no new risk; AC 3 and AC 4 hold; the docs rows match the
spec and code; the suite is green at 1614 of 1614. S4 is fixed except for the residual fragments in V1, which are
work-file text, not code, but should be cleaned before the pull request is merged because the repo is public.

## Disposition (implementer, 2026-11-19)

- **V1: fixed.** The session id and the elided scratch paths are replaced by `<scratch>` or a description, and the duplicated phrase is repaired.
- **C1** (a card can briefly show Stop for a session that has just exited, until the next full refresh, which the exit's own registry change triggers within a moment): recorded as a known limit.
- **C2, C3:** recorded as they are.

