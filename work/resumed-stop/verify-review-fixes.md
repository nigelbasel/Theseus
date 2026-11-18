# resumed-stop: verify the review fixes

Pipeline: `bug` (method kit v0). Stage: **re-verify of the review fixes** (S1, S2, C4 of `review.md`), autonomy L0.
Repo Theseus, branch `main`, commit `0b3052c`.

Verifier: a fresh sub-agent started by the implementer. **Partial independence**: it did not write the code or the
review, but it was briefed by the implementer, on the same model. Contracts: the "Spec" section of `fix.md` (rules
1-6) and findings S1, S2, C4 of `review.md`. The "What was done" section of `review.md` was read only after checks 1-3.

`SVM` is `src/Theseus.App/ViewModels/ShellViewModel.cs`. Line numbers are at `0b3052c`.

## Checks

| # | Check | Result | Evidence |
|---|---|---|---|
| 1 | Full suite | **PASS** | `dotnet test Theseus.slnx --nologo --logger "console;verbosity=normal"`, 20:43:09 to 20:45:15 (clock). Total 1491, passed 1491, exit 0. All five projects rebuilt (`Theseus.Core.dll` written 20:43:12); no `warning CS` or error line in the output. No CS0009 or file-in-use error; run once. Every line is in `C:\Users\nigel\.claude\logs\git-22.log` under `<resumed-stop-verify-3>`. The six `StopFinishedTests` ran and passed. |
| 2 | S1: tried vs finished | **PASS** | `AlreadyTyped` (tried) is used in one place only, the never-twice guard passed to `ShouldClose` (`SVM:1375`); `Typed` is still recorded before the keystroke (`SVM:1380`), so `/exit` cannot be typed twice for one stop (unchanged). `Finished` is recorded only inside `if (outcome == NudgeOutcome.Sent)` (`SVM:1394`). `IsFinished` is used at every place a delivered stop's unfinished report can be skipped: `Sweep` (`SVM:638`, predicate applied only to handed requests, `StopRequestStore.cs:244-251`), the newer-session retire (`SVM:1361`) and the start-again retire (`SVM:1736`). The other `ReportUnfinished` calls are for waiting requests (never typed) or `AbandonStops` (`SVM:1521`, held-back stops, which the close step skips, so never typed). `ReportFinished` runs only on `Sent`, after `Finished`, so a stop cannot be reported both finished and unfinished. A tried-but-unsent stop is now reported unfinished when removed, as before the original fix. |
| 3a | S2: BeginStop judged by delivery | **PASS** | `SVM:943-945` uses `SessionClosing.HandedBefore`, which compares the start with `DeliveredUtc ?? RequestedUtc` (`SessionClosing.cs:85-90`). The hook stamps `DeliveredUtc` (`StopRequestStore.cs:123`). The comment above now says "to the session running now", which matches. |
| 3b | S2: the part left | **PASS, with a note (V1)** | The close step still uses `StartedSince` (`SVM:1359`), as `review.md` says. Safe: the change can only make `alreadyDelivered` true more often, which routes to `AlreadyDelivered` and types and asks nothing; the `/exit` guard is untouched. No new way to type `/exit` twice. But see V1: the left part limits what the S2 fix achieves. |
| 3c | New tests can fail on wrong code | **PASS** | `AStopWhoseExitWasOnlyAttempted_HasNotFinished` fails if `IsFinished` read the tried set. `AStopAskedOfAnEarlierRunButHandedToThisOne_BelongsToThisOne` fails if `HandedBefore` compared `RequestedUtc` (asked 15:00, started 15:10 would give true). `AStopWithNoDeliveryTime_IsJudgedByWhenItWasAsked` pins the fallback. Before the change the members did not exist, so the file could not build (by inspection; not checked out and run). They test Core only; see V3. |
| 3d | C4 notice text | **PASS, with a note (V4)** | One: "That stop was held back when the board opened. Close them finishes it, with every other held-back stop." Several: "{n} of these stops were held back when the board opened. Close them finishes every held-back stop." Both now say what **Close them** does (`ConfirmStops`, `SVM:1497-1505`, clears every held-back stop). The button is labelled "Close them" (`MainWindow.xaml:1968`). |
| 4 | Anything else changed | **PASS** | `git show 0b3052c --stat`: `SVM.cs`, `ExitsTyped.cs`, `SessionClosing.cs`, new `StopFinishedTests.cs`, `review.md`. No existing test file changed or removed; `fix.md` untouched. The only other behaviour change is the notice wording. Repo convention allows `var` (`AGENTS.md:49`). |
| 5 | "What was done" matches | **PASS, with gaps** | S1, S2 and C4 dispositions match the code, as do "6 tests", "no existing test file changed" and "1491 of 1491, 0 warnings". It does not mention V1 or V2. |

## Findings

| # | Severity | Finding |
|---|---|---|
| V1 | should note | **The S2 fix mostly does not reach its case, because of the part left.** The close step runs on every refresh (`SVM:1302`), and `BeginStop` itself calls `Refresh()` before writing the notice (`SVM:1022`). For the S2 case (asked of an earlier run, handed to the session resumed outside Start), `StartedSince` is true, so the first close-step pass after delivery retires the request and reports it unfinished (`SVM:1359-1368`). So: (a) a press after that pass finds no delivered request and asks again, as before the fix; (b) a press inside the window between delivery and the next pass is not asked again, but its own `Refresh()` then retires the request, and the notice still says "already has its stop and is finishing it; it closes once it has handed over and gone quiet", which will not happen. Neither types `/exit` twice. Rule 3 now holds only in that window. `review.md` records the close step as left for Nigel; it does not say that this leaves the `BeginStop` fix close to having no effect. |
| V2 | should note | **The notice half of S1 is not addressed.** The review said that for a tried-but-unsent stop the `AlreadyDelivered` notice "promises a close that will not come in this run" (S1, R10). It still does: `alreadyDelivered` does not look at `IsFinished` or `AlreadyTyped`, so a second press says it "closes once it has handed over and gone quiet", while `AlreadyTyped` blocks any further `/exit`. The logging half of S1 is fixed. "What was done" does not mention the notice. |
| V3 | consider | **The App call sites have no tests.** Reverting any of `SVM:638`, `:1361`, `:1736` to `AlreadyTyped`, removing `_closed.Finished` at `:1394`, or `BeginStop` back to `StartedSince` would pass the suite. Same known limit as `fix.md`. The existing test name `Sweep_DoesNotReportAStopWhoseExitWasAlreadyTyped` still names "typed", not "finished" (cosmetic). |
| V4 | consider | **C4, one stop:** "That stop" keeps the ambiguity the review named for "That session" when several sessions were selected and only one was held back. "with every other held-back stop" is a little odd when it is the only one. The several-stop text reads correctly. |

## Verdict

**PASS, with notes.** The S1, S2 and C4 fixes are correct as written, are safe (no new way to type `/exit` twice, and a
stop cannot be reported both finished and unfinished), and break nothing: 1491 of 1491, no warnings, no existing test
changed. Two things for Nigel: V1 (because the close step was left alone, the S2 fix works only between delivery and
the next refresh, and in that window the notice promises a close that will not come) and V2 (S1's notice half is
still open).

## Record

| Step | Started | Finished | Notes |
|---|---|---|---|
| re-verify | 2026-11-18 20:42:55 +0000 (clock) | 2026-11-18 20:45:47 +0000 (clock, before writing this file) | Suite 20:43:09 to 20:45:15. Read and run only; no source change, commit or push. Temp files left in the session scratchpad `verify-resumed-3\`. |
