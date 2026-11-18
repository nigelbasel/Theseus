# resumed-stop: evidence

Pipeline: `bug` (method kit v0), stage **verify, part 1 of 2, attempt 2** (re-verify of the fix round `d43d6ff`;
attempt 1 is `evidence-part1-attempt1.md`, which passed `a33e527` with gaps G1-G5). Part 2 is a live check on a
running board by Nigel. Autonomy L0. Card git-22, branch `main`, repo Theseus, at `d43d6ff` (working tree clean).

Verifier: a fresh sub-agent started by the implementer. **Partial independence**: it did not write the code, but it
was briefed by the implementer, on the same model. It read the Spec and attempt 1's evidence, then the code; the
"Pass 2" section of `fix.md` was read only after checks 1-3.

Contract: rules 1-6 of the "Spec" section of `work/resumed-stop/fix.md`.

## Checks

| # | Check | Result | Evidence |
|---|---|---|---|
| 1 | Full suite | **PASS** | `dotnet test Theseus.slnx --nologo --logger "console;verbosity=normal"` from the repo root, run once (no CS0009 or file-in-use error, so no rerun). Banner 17:12:16.264; `Test Run Successful.` 17:14:27.269; `Total tests: 1485`, `Passed: 1485`. Every line appended to `C:\Users\nigel\.claude\logs\git-22.log` under `<resumed-stop-verify-2>`. No `warning CS/NU/MSB` lines in the output (0 matches). The four new tests passed: `Sweep_DoesNotReportAStopWhoseExitWasAlreadyTyped`, `Sweep_ReportsAnExpiredStopThatWasNeverClosed`, `Write_KeepsTheFirstTime_WhenTheWaitingOneIsFromThisRunOfTheSession`, `Write_StartsAFreshStop_WhenTheWaitingOneIsFromAnEarlierRunOfTheSession`. |
| 2 | G1, G3, G5 fixed? | **G3 fixed (one residual case), G1 fixed, G5 fixed** | See the G1-G5 table. |
| 3a | Existing tests changed or removed | **None** | `git show d43d6ff --stat`: the only test file is the new `StopRequestStoreLoggingTests.cs` (+100). Other files: `ShellViewModel.cs`, `IStopRequestStore.cs`, `StopRequestStore.cs`, `evidence-part1-attempt1.md`, `fix.md`. |
| 3b | Stopping behaviour beyond G1/G3 | **None** | `Write` differs only when `sessionStartedUtc` is given *and* the waiting request was asked before it; otherwise it is the `a33e527` code (keep the first time). `Ask` is the only caller of `Write` (`ShellViewModel.cs:1099`), and no other `IStopRequestStore` implementation exists in `src` or `tests`. `Describe`'s new `heldBack` argument defaults to 0; the routes, close step, `Sweep`, expiry and existing log lines are untouched. Added log output: the "did not finish (a newer session is running ...)" line from `Write` in the G3 case, which is an existing `StopTiming.ReportUnfinished` message. |
| 3c | New warnings | **None** | Check 1. |
| 4 | `fix.md` Pass 2 against findings | **Matches, one omission** | See the last section. |

## G1-G5

Line numbers are at `d43d6ff`.

| Gap | Status | Evidence |
|---|---|---|
| G1 (notice wrong for a held-back stop) | **Fixed** | `ShellViewModel.cs:950-961`: on `AlreadyDelivered`, a session in `_unconfirmed` counts as `heldBack`, not `underway`; `Describe` (`:1043-1048`) says "press Close them to finish it". The button exists (`MainWindow.xaml:1968`, `ConfirmStopsCommand`), shown while `HasUnconfirmedStops`, which needs `Sessions.Find` non-null — already true on this branch, since `alreadyDelivered` required it (`:943-945`). `ConfirmStops` clears `_unconfirmed` and runs `CloseFinishedSessions` (`:1497-1505`). Nothing is written or typed on the press, so rule 5's "held back and asked about" still holds. Untested (App), as before; for the live check. The code comment at `:941` still says "by this very process"; `fix.md` Pass 2 corrects the wording, the comment is unchanged. |
| G2 (`Sessions.Find` null, lifecycle live) | **Not fixed; recorded limit** | `:943-945` unchanged. |
| G3 (stale waiting request lends its time) | **Fixed when the session is found; unchanged when it is not** | `StopRequestStore.cs:43-53`: if `sessionStartedUtc` is given and `StartedSince(waiting, started)` (`started > waiting.RequestedUtc`, `SessionClosing.cs:73-78`), the old request is reported (`ReportUnfinished(waiting, delivered: false, NewerSession, now)`) and the new request keeps its own `RequestedUtc`; otherwise it keeps the waiting one's time. **Rule 4 for this run:** a request from this run has `RequestedUtc` ≥ the session's start, so `StartedSince` is false and the first time is kept (test `Write_KeepsTheFirstTime_...`). **Closes once, never stuck:** the replacement's `RequestedUtc` is the press (`DateTimeOffset.UtcNow`), later than the start, so after delivery `CloseFinishedSessions` does not retire it as a newer session (`:1359`) and it is a new `ExitsTyped` key, closed once by the usual route. The stale request was never delivered, so no `/exit` was typed for it and it cannot be closed twice. **Reported once:** after the replacement the waiting file is the fresh request, so a third press keeps its time and reports nothing; the report repeats only if the file write fails (`catch` at `:61-64`), which leaves the stale request in place. **`sessionStartedUtc` null** (`Sessions.Find(...)?.StartedUtc` null at `:1108`): `Write` keeps the old time, exactly the `a33e527` behaviour, so G3 remains in that case if the session is found again before delivery and closing. Narrow (needs both an outside resume and a missed lookup at the press), the same lookup gap as G2; not recorded in Pass 2 (F2 below). |
| G4 (claim race at a re-ask) | **Not fixed; recorded limit** | `Write` still reads `Waiting` then writes. In the G3 branch the window can now also give two unfinished reports for the stale request (Write's `delivered: false`, then the close step's `delivered: true` NewerSession at `:1361-1363`); logging only, same millisecond window. |
| G5 (`Sweep` predicate untested) | **Fixed** | `StopRequestStoreLoggingTests.cs`. The tests can fail on wrong code: ignoring `finished` makes `Sweep_DoesNotReport...` find "did not finish"; not counting a finished stop fails its `gone == 1`; always skipping the report fails `Sweep_Reports...`. The G3 pair: always replacing fails `Write_KeepsTheFirstTime_...`, never replacing or not reporting fails `Write_StartsAFreshStop_...`, and the comparison direction is pinned by the start times either side of the request. Not caught: a predicate given the wrong request (both lambdas ignore their argument); minor. The fixture starts and stops the static `TheseusLog`, the same pattern as `LinkBookLoggingTests`; no assembly-level NUnit parallelism was found, so the tests do not share the logger with a parallel fixture. "The two G3 tests failed to build before the change": [not verified]. |

## Findings

| # | Severity | Finding |
|---|---|---|
| F1 | note (pre-existing) | A stale waiting request from an earlier run makes `alreadyAsked` true (`:939`), so this run's first press is routed as a second press (`Interrupt` for a working session, `Nudge` for an idle one). Unchanged by `d43d6ff` and allowed by rule 6; now that `Write` treats the stale request as a different stop, the routing and the store disagree about whether this is a re-ask. For the owner. |
| F2 | gap (minor) | G3 with `sessionStartedUtc` null still keeps the stale time (see G3 row). Pass 2 lists G2 and G4 as limits but not this. |
| F3 | note | G4's window can produce a second unfinished report for the stale request in the G3 branch (see G4 row). Logging only. |

No defect found.

## fix.md "Pass 2" against what was found

- G3 row: matches the code, including the report text and "one from this run keeps its first time". It does not
  mention the null-start case (F2).
- G1 row: matches. G5 row: matches; the tests use the real log as described.
- "New file ... (4 tests)", "No existing test file changed", "1485 of 1485, 0 warnings": match checks 1 and 3a.
- "The two G3 tests failed to build before the change": [not verified] (they use the new `sessionStartedUtc`
  parameter, so they could not have built against `a33e527`, which is consistent).
- G2, G4 recorded as limits: match. The wording correction for rule 3 matches G1 of attempt 1; the code comment at
  `ShellViewModel.cs:941` still carries the old wording.

## Verdict, part 1 (attempt 2)

**PASS.** Suite 1485/1485 with no warnings; no existing test changed; G3 is fixed for the case it describes (rule 4
still holds for a stop of this run, the replacement closes once and cannot be stuck as a newer session, the report is
made once), G1's notice is corrected, and G5's tests would fail on wrong code. Nothing else in stopping changed. One
minor residual (F2, G3 when the session is not found at the press) and one pre-existing routing note (F1) are left for
the owner; G1's notice remains for the live check.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| verify part 1, attempt 2 | 2026-11-18 17:12:04.022 (clock) | 2026-11-18 17:14:47.183 (clock, before writing this file) | Suite 17:12:16.264-17:14:27.269 (logged). No worktree created. Run output kept at `C:\Users\nigel\AppData\Local\Temp\claude\3c9e613a9d1facbc\233623bc-2f72-4934-ba2b-c4a52669d270\scratchpad\verify-resumed-2\test-run.txt`. |
