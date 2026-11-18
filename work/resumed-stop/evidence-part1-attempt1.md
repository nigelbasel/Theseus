# resumed-stop: evidence

Pipeline: `bug` (method kit v0), stage **verify, part 1 of 2** (code and tests; part 2 is a live check on a running
board by Nigel). Autonomy L0. Card git-22, branch `main`, repo Theseus, at `a33e527`.

Verifier: a fresh sub-agent started by the implementer. **Partial independence**: it did not write the code, but
it was briefed by the implementer, on the same model, and read the spec before the code.

Contract: rules 1-6 of the "Spec" section of `work/resumed-stop/fix.md`. The rest of `fix.md` was read only after
checks 1-4.

## Checks

| # | Check | Result | Evidence |
|---|---|---|---|
| 1 | Full suite | **PASS** | `dotnet test Theseus.slnx --nologo --logger "console;verbosity=normal"` from the repo root at `a33e527`, run once (no CS0009 or file-in-use error, so no rerun). Banner 16:58:39.990; `Test Run Successful.` 17:02:04.640; `Total tests: 1481`, `Passed: 1481`; exit code 0 (17:02:04.795). Every output line appended to `C:\Users\nigel\.claude\logs\git-22.log` under `<resumed-stop-verify>`. No build warnings in the output (the six "warning" matches are test names such as `Warning_SaysWhatToDoAboutIt`). |
| 2 | Refactor `f9b82e3` changes no behaviour | **PASS** | Before: `HashSet<string>(OrdinalIgnoreCase)`, used at exactly two places, `_closed.Contains(sessionId)` as `alreadyAsked` and `_closed.Add(sessionId)` after `ShouldClose` (old `ShellViewModel.cs:1336`, `1341`). After: `AlreadyTyped(delivered)` / `Typed(delivered!)` (`f9b82e3` `ShellViewModel.cs:1333`, `1338`), with the same set keyed on `request.SessionId`, same comparer. `delivered` is read from the delivered-folder path for `sessionId` (`StopRequestPath.For` normalises through `Guid.TryParseExact`), and its `SessionId` is the id `Ask` wrote, so it equals `sessionId` ignoring case. **Null request:** old code could pass `alreadyAsked = true` with `delivered == null`, new code passes `false`; but `SessionClosing.ShouldClose` returns `false` whenever `delivered is null` before it looks at `alreadyAsked` (`SessionClosing.cs`, first guard), and `Typed` is only reached after `ShouldClose` returned true, so `delivered!` is never null there. Same outcome in every case. Diff stat: ShellViewModel 4+/7-, new `ExitsTyped.cs`, new test file; nothing else. |
| 3a | Reproduction test unchanged since `ed0bd0e` | **PASS** | `git diff ed0bd0e HEAD -- tests/Theseus.Tests/Stopping/ExitsTypedTests.cs` is one hunk of 11 added lines (a new test `AStopOfAnotherSessionAtTheSameMoment_IsAStopOfItsOwn`), 0 removed; `ALaterStopOfTheSameResumedSession_CanStillBeClosed` and the `Request` helper are untouched. Only commit touching the file since: `a33e527`. |
| 3b | Fails on `f9b82e3`'s code | **PASS (by reasoning; not run)** | At `f9b82e3`, `Typed` adds `request.SessionId` and `AlreadyTyped` checks only `SessionId`. The test types for `Request(FirstStop)` and asks about `Request(FirstStop.AddMinutes(4))`, same `SessionId`, so `AlreadyTyped` returns `true` against an expected `False`. Matches the recorded failure in `reproduction.md` ("Expected: False But was: True"). No worktree was created. |
| 3c | Passes now | **PASS** | `Passed ALaterStopOfTheSameResumedSession_CanStillBeClosed [4 ms]` at 17:01:49.559 in the check 1 run. |
| 3d | Existing tests changed or removed | **None** | `git diff ed0bd0e a33e527 --numstat -- tests`: `11 0 ExitsTypedTests.cs`, `25 0 StopRequestStoreTests.cs`, `20 0 StopRoutingTests.cs`. Additions only; no `-` lines in the tests diff. |
| 4 | Rules 1-6 | **Met, with gaps** | See the next table and Findings. |
| 5 | `fix.md` claims and known limits | **Match, with omissions** | See "fix.md against what was found". |

## Rules 1-6

Line numbers are at `a33e527`.

| Rule | Met? | Evidence |
|---|---|---|
| 1. One stop is one request, known by session and time asked | **Met** | `ExitsTyped.cs`: set of `(SessionId, RequestedUtc)`, compared by `OrdinalIgnoreCase` and `UtcTicks`, so an offset change of the same instant is the same stop. `RequestedUtc` survives `Claim`'s stamp (`StopRequestStore.cs:114`, `with { DeliveredUtc = when }`), and a `System.Text.Json` round trip of `DateTimeOffset` keeps full tick precision, so the delivered file re-read on each pass gives the same key. |
| 2. `/exit` at most once per stop; a later stop can be finished | **Met** | `ShellViewModel.cs:1355-1361`: `AlreadyTyped(delivered)` feeds `ShouldClose`, and `Typed(delivered!)` is recorded *before* `TabNudge.Nudge`. Two passes before the request is retired: if `Retire` at `:1375` fails, or the nudge was not `Sent`, the file stays and the next pass re-reads the same `(session, RequestedUtc)`, so `AlreadyTyped` is true and nothing is typed. Two cards sharing one session id: the second sees the first's record. A later stop has a new `RequestedUtc` (`Write` keeps an old one only while a request is *waiting*, and `Claim` moves it out of waiting), so it is a new key and is closed: the reproduction test. Narrow exception: Finding G4. |
| 3. A press on a delivered stop does not ask again; runs the close check | **Met for the case in the report; gaps G1, G2, N1** | `ShellViewModel.cs:942-944`: delivered exists, `Sessions.Find` returns the session, and `!StartedSince(handed, running.StartedUtc)`. `StopRouting.cs:41-44` returns `AlreadyDelivered` for any live lifecycle, ahead of the hook-installed and lifecycle checks; `Ended` still gives `None` (tested). The branch writes nothing (`:948-952`), so no new request and no claim overwriting the delivered one (F1). The press reaches the close step through `Refresh()` (`:1014`) → `SyncLifecycles` → `CloseFinishedSessions` (`:1283`), *unless* another selected session went to `PasteYourself`, in which case `AdvanceStop()` returns first (`:1006-1011`) (N1). **"Delivered by this process" is not what is tested:** the test is "delivered to the session running now", which also matches a request left by an earlier Theseus run (G1). **`Sessions.Find` null:** `alreadyDelivered` is false and the press falls through to the old routing, so if `step.Lifecycle` still says live it asks again as before the fix (G2). |
| 4. A stop's time is the first press | **Met as written; gap G3** | `StopRequestStore.cs:39-44`: when a request is waiting, the new one keeps its `RequestedUtc`; `ExpiresUtc`, `Text` and `HandoffPath` come from the new press. Expiry: unchanged from before the fix (a re-ask already set expiry to its own press + 4h; it still does), and `Sweep` reads only `ExpiresUtc`. A different handoff path: the new path is kept and judged against the first press, so a handoff written to it between the presses counts; consistent with "the stop runs from the first press". Not distinguished: a waiting request that belongs to an *earlier* session run (G3). Tested: `Write_KeepsTheFirstRequestTime_...`, `Write_StartsANewStop_WhenNoneIsWaiting`. |
| 5. Finished once `/exit` typed; not later reported unfinished in the same run | **Met** | Every place a delivered request is removed: (a) after a sent `/exit`, `:1375` (reports *finished*, `Retire` result still ignored; the suppressions below cover a failed retire); (b) newer session, `:1342` (`&& !_closed.AlreadyTyped(delivered)`); (c) started again, `:1711-1719` (`&& !_closed.AlreadyTyped(handed)`); (d) expiry, `StopRequestStore.cs:231-243` via `finished`, passed `_closed.AlreadyTyped` at `ShellViewModel.cs:638` (the only call site); (e) `AbandonStops`, `:1495-1502`, not guarded, and needs none: it acts only on `_unconfirmed`, filled once at construction (`:218-221`) when `_closed` is empty, and the close step skips `_unconfirmed` (`:1333`), so a held-back stop cannot have been typed; (f) `Claim`'s `File.Move(..., overwrite: true)` (`StopRequestStore.cs:96`) replaces a delivered request silently; pre-existing, now not reached from the board's own press on a stop delivered to the running session (rule 3). After a restart `_closed` is empty, so a leftover is held back and asked about as before. The predicate path in `Sweep` has no test (G5). |
| 6. Unchanged: waiting routes, close conditions, quiet period, expiry, logging | **Met, one addition** | `StopRouting.For` with `alreadyDelivered = false` is the old function (the new check is the only inserted branch); `Interrupt`/`Nudge` recomputes `cutIn` without `alreadyDelivered`, reachable only when it is false. `SessionClosing` untouched. Expiry: see rule 4. Logging: every existing line unchanged; one new Information line, "Did not ask {Workstream} again: it has already been handed its stop" (`:949-950`), required by rule 3 (N3). Also `Describe` gains an optional `underway` parameter; the old "Nothing to stop." condition now also needs `underway == 0`. |

**Stop-timing logging (StopTiming.ReportFinished / ReportUnfinished).** `ReportFinished` is unchanged and still logs
even if the following `Retire` failed. Three of the five `ReportUnfinished(... delivered: true ...)` sites are now
skipped for a stop already typed (NewerSession, StartedAgain, Expired); the Declined site is unreachable for one.
Because of rule 4, both reports now measure age from the first press (`nowUtc - request.RequestedUtc`), which is F3's
intent; a re-asked stop therefore reports a longer age than before.

## Findings

| # | Severity | Finding |
|---|---|---|
| G1 | gap | **Rule 3's "delivered by this process" is implemented as "delivered to the session running now"** (`ShellViewModel.cs:942-944`; the comment above it and the `fix.md` table both say "by this process"). A delivered request left by an earlier Theseus run, held back in `_unconfirmed`, also satisfies it. Pressing Stop on such a held-back session now writes nothing (which keeps rule 5's "held back and asked about") but the notice says "it closes once it has handed over and gone quiet", while the close step skips `_unconfirmed` sessions until Confirm, so it will not close on its own. Before the fix the press wrote a new request (the F1 overwrite). The notice is wrong for that case; nothing is typed wrongly. For the live check. |
| G2 | gap | **When `Sessions.Find` returns null but the step's lifecycle is live**, `alreadyDelivered` is false and the press asks again exactly as before the fix (F1 not covered for that case). Whether the two sources can disagree in practice is not decided by the code read here. |
| G3 | gap | **Rule 4 keeps the time of any waiting request, including one from an earlier run of the same session id.** If a stop was pressed, the session then ended before its turn did (so the request was never claimed), and the session is resumed *outside* the board's `StartMany` (which cancels a waiting request at `:1692-1703`), a new press keeps the old time. After delivery, `StartedSince(delivered, session.StartedUtc)` is true, so the close step retires it as a newer session (`:1340-1351`) and the session is never closed: the reported symptom, by another route. Before the fix that second press wrote a fresh time and would have closed. Narrow (needs a start not made by the board), but a regression the rule did not foresee: the rule could say "keeps the time of a waiting request asked of the session running now". |
| G4 | gap | **Race at a re-ask.** `Write` reads `Waiting` and then writes; if the hook claims the waiting request between the two, the new pending request carries the delivered stop's `RequestedUtc`. After `/exit` is typed for that stop, a later claim of the orphan would be keyed as the same stop and never closed. Milliseconds wide; noted for completeness, not seen. |
| G5 | gap | **No unit test for `Sweep`'s `finished` predicate** (`StopRequestStore.cs:235-239`), although it is in Core and testable: none of the five `Sweep_*` tests passes it. `fix.md`'s known limits name the untested App changes but not this. The App guards (`:1342`, `:1716`, `:942-952`) are untested, as `fix.md` says. |
| N1 | note | The press reaches the close step only when no selected session was queued for pasting: `AdvanceStop()` returns before `Refresh()` (`:1006-1011`). Only matters for a multi-session press with a session routed to `PasteYourself`. Also, "closed on the press" still needs 20 s of idle (`SessionClosing.Quiet`), as rule 6 requires. |
| N2 | note | F4 is answered by suppressing the later unfinished report, not by acting on `Retire`'s result at `:1375`; a failed retire is still not logged there. Consistent with rule 5 as written. |
| N3 | note | `fix.md` says "nothing else changed: ... log lines"; existing lines are unchanged and one is added (rule 6 row). |

No defect found.

## fix.md against what was found

- Rules 1, 2, 4, 5, 6 rows: match the code. Rule 3 row: matches except "by this process" (G1).
- "New tests ... routing for a delivered stop (4 cases), a stop of another session at the same moment, and the
  first request time kept / a new stop after delivery": matches (`StopRoutingTests` 3 cases + 1, `ExitsTypedTests` 1,
  `StopRequestStoreTests` 2), all passing in check 1. "Written first and failing to build": [not verified].
- "Full suite 1481 of 1481, 0 warnings": matches check 1.
- Known limit 1 (rule 5 per board run): matches. Known limit 2 (test file not empty since `ed0bd0e`, additions
  only, 11 lines): matches check 3a exactly. Known limit 3 (App changes untested): matches; it omits the untested
  `Sweep` predicate in Core (G5).
- Not mentioned: G1, G2, G3, G4.

## Verdict, part 1

**PASS.** The suite passes (1481/1481), the refactor changes no behaviour, the reproduction test is unchanged and
now passes, every rule is met in code (rule 3's notice for held-back stops, G1, is left to the live check), and no
defect was found. Five gaps are recorded for the owner, of which G3 (a stale waiting request's time kept across a
resume made outside the board) is the one most worth a decision, and G1 the one the live check can see.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| verify part 1 | 2026-11-18 16:58:13 (clock) | 2026-11-18 17:02:59 (clock, before writing this file) | Suite 16:58:39.990-17:02:04.795 (logged). No worktree created. |
