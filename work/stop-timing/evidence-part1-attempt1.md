# stop-timing: evidence

Pipeline: `feature` (method kit v0), stage **verify, part 1 of 2** (code and tests; part 2 is a live check on a
running board by Nigel), autonomy L0. Card git-22, branch `main`, repo Theseus, commit `a565dd7`.
Verifier: a fresh sub-agent started by the implementer. Independence is partial: same model, briefed by the
implementer, but it did not write the code and read `implementation.md` and `plan.md` only after checks 1 to 4.

## Checks

| # | Check | Result | Evidence |
|---|---|---|---|
| 1 | Full suite `dotnet test Theseus.slnx --nologo --logger "console;verbosity=normal"` | PASS | Total 1448, passed 1448, failed 0, total time 1.6376 min, exit 0. No compiler warnings in the output (`TreatWarningsAsErrors` is on, `Directory.Build.props:22`). The build was incremental, so a project that was not recompiled would not re-emit warnings. No CS0009 or file-in-use error, no rerun. Every line is in `C:\Users\nigel\.claude\logs\git-22.log` under `<stop-timing-verify>`, from 14:14:59.292 to 14:17:05.924. All 12 new tests ran and passed. |
| 2 | AC 1 to 9 against the code | PASS with gaps | Table below. No AC is unmet on the normal path. Gaps are in failure or race paths only. |
| 3 | Tests cover the spec | PARTIAL | The durations and progress logic is tested. Not tested: the two log lines, the stamp-failure path, an old-shape JSON file, and all App call sites (AC 5 exactly once, AC 6). No pre-existing test was changed: `git show a565dd7 --numstat` gives 20 added and 0 removed in `StopRequestStoreTests.cs`, and the diff has no removed lines under `tests/`. |
| 4 | Edge cases (throwaway probe in the scratchpad, referencing the built `Theseus.Core.dll`, culture set to de-DE) | PASS with notes | Output quoted under Findings. |
| 5 | `implementation.md` and `plan.md` against what was found | Mostly match | Two mismatches: F3 and F7. |

## Acceptance criteria

| AC | Result | Evidence |
|---|---|---|
| 1. The delivered request records `DeliveredUtc`, read from the clock at hand-over | Met | `Hook/Program.cs:57` passes `DateTimeOffset.UtcNow` to `Claim`. `StopRequestStore.cs:96-111` stamps the moved file. Test `Claim_RecordsWhenTheRequestWasDelivered` checks the returned request and the copy on disk. |
| 2. A failed stamp never stops, delays or changes a stop | Met (read and probed) | The stamp runs after the move, and its exceptions are caught (`StopRequestStore.cs:107-116`: `IOException`, `UnauthorizedAccessException`, `NotSupportedException`), returning the unstamped request. Probe: with a read-only request file the write failed, and the claim still returned the request (`thrown=none claimed=True deliveredUtc=null`). The hook's output is unchanged: `request.Text` survives the `with` copy, and `Program.cs:71-76` is untouched. The only exceptions that could escape the filter are `ArgumentException` (impossible here, because the path was just used for the move) and `JsonException` from `Serialize` (implausible for this flat record). The hook has no AOT or trimming (`Theseus.Hook.csproj`), and `Parse` already relies on reflection-based JSON. If something did escape, `Program.cs:78-81` would return 0 after the move, which would lose the stop. See F5. The stamp adds one small file write before the output: a delay of milliseconds, not nothing. |
| 3. `/exit` typed: one Information line with workstream, request to delivery, delivery to handoff, handoff to `/exit`, and total | Met | `ShellViewModel.cs:1348-1351` calls `ReportFinished` only when the outcome is `Sent`. The template is at `StopTiming.cs:69-72`. Probe line: `Stop of probe-ws finished in 55.0s: delivered after 35.0s, handoff -5.0s later, /exit 25.0s after that`. |
| 4. No delivery time: the line still has the total and the time to the handoff, and says the delivery time was not recorded | Met | `StopTiming.cs:77-79`. Probe: `finished in 55.0s: delivery time not recorded, handoff 30.0s after the request, /exit 25.0s after that`. This line is also used, wrongly, when the delivery time *is* recorded but the handoff time cannot be read. See F1. |
| 5. Every removal without `/exit` gives one line: workstream, age, reason, progress | Met on the normal path | Removal sites in `src/` (grep of `Retire`, `Cancel` and `Sweep` on the store; the only store users are `ShellViewModel` and the hook): (a) `ShellViewModel.cs:1322` newer session, reports at 1323. The return of `Retire` is ignored, so a failed delete is reported anyway and again on every refresh (F4). (b) `:1350` after `/exit` `Sent`, reports finished, not unfinished. (c) `:1495` declined, reports only if `Retire` returned true and the request parsed. (d) `:1690` start again, withdraw, reports only if `Cancel` returned true and the request parsed. (e) `:1707` start again, retire, same guard. (f) `StopRequestStore.cs:207` expiry, waiting request, reports only after `Cancel` returned true. (g) `:219` expiry, delivered request, same guard. The Debug count was removed (old `:204-206`). Probe: an expired request gave exactly one line, and a second sweep removed 0. A request whose JSON cannot be parsed is removed with no line, because it cannot name a workstream. |
| 6. A stop removed after `/exit` is not reported as unfinished | Met on the normal path | Site (b) retires and reports finished once. `_closed` stops a second attempt in the same process, and `Sessions.Find` skips the session once it has gone. The exception is a failed `Retire` at `:1350` (return ignored): the file stays, and a later expiry or decline then reports it as unfinished. See F4. |
| 7. Stopping is unchanged | Met, with one race (F2) | Against `a565dd7~1`: `SessionClosing` is unchanged (not in the diff). Quiet stays 20 s (`SessionClosing.cs:26`). Expiry is unchanged (`StopRequestLife`, and the `Sweep` filters `ExpiresUtc <= now`). The conditions in `CloseFinishedSessions` are unchanged. `Retire` and `Cancel` are still called in the same order and under the same conditions, and every report comes after the removal. The new reads (`Waiting` and `Delivered` before `Cancel` and `Retire`) only read. The `Sweep` return value is unchanged. The one difference is that the hook now writes the delivered file a second time, non-atomically, after the move (F2). |
| 8. Durations come only from recorded times, and a missing time is said to be missing | Met, with one wording gap (F1) | `StopTiming.Finished` (`:17-29`) subtracts recorded values only. A missing value stays null, and `Seconds` prints `(not recorded)` (`:107-108`). The handoff time is the file's write time (`:96-98`), and the exit and removal times are `UtcNow` at the call. Nothing is clamped or estimated, so negative figures can appear (F6). |
| 9. Records stay in the Theseus log: no new file, nothing in the UI | Met | Output goes only through `TheseusLog.Information`. No new file or UI binding is in the diff. The existing delivered JSON gains one field. |

## Findings

| # | Severity | Finding |
|---|---|---|
| F1 | gap | `ReportFinished` (`StopTiming.cs:66-79`) uses the "delivery time not recorded" line whenever any of the three figures is null. If the delivery *was* stamped but the handoff's write time cannot be read when the line is written, the log says the delivery time was not recorded (a false statement) and leaves out the delivery figure. Probe, with delivery stamped and the handoff gone: `finished in 60.0s: delivery time not recorded, handoff (not recorded)s after the request, /exit (not recorded)s after that`. This can only happen if the handoff disappears between `ShouldClose` (which required it) and the report, across the `TabNudge` call, so it is rare. It conflicts with the wording of AC 8. |
| F2 | note | The stamp is a second, non-atomic `File.WriteAllText` on the delivered file after the move. (1) A Theseus read during that write (`Delivered`, `Handed`) can see a partial file, and `Parse` returns null. The close step simply retries on the next tick, but at startup the request could miss being held back in `_unconfirmed`. A start-again read in that window would retire the request without a line. (2) If Theseus retires or expires the request between the hook's move and its write, the write creates the file again: the request comes back, and its expiry is reported a second time. Both windows are milliseconds wide, and the second needs a stop to be delivered at the moment its card is restarted or expires. The plan said the stamp would go "through `AtomicFile`". The code does not, and `implementation.md` does not mention the change. |
| F3 | gap | Tests: none covers the stamp-failure path (the spec's "How it will be verified" asks for "the hook's claim recording the delivery time without ever failing the delivery"). None covers the text or branch choice of either log line: `ReportFinished` could always take the fallback branch and every test would still pass. None covers an old-shape JSON file (plan step 2). `ARequestDeliveredByAnOlderHook_HasNoDeliveryTime` writes through the current `Write`, which emits `"deliveredUtc":null`, and claims without a time, so it never reads a file that lacks the property. The probe covered both gaps by hand: the old shape parses with a null `DeliveredUtc`, and a read-only file still claims. The App call sites (AC 5 exactly once, AC 6) have no tests and are left to the live check, as `implementation.md` says. |
| F4 | note | At `ShellViewModel.cs:1322` and `:1350` the return of `Retire` is ignored, unlike the other sites. If the delete fails, the newer-session case is reported on every refresh until it succeeds, and the finished case can later be reported as unfinished when it expires or is declined. Only a failed delete triggers either. The pre-existing "Retired..." line at `:1324` already had the same repeat. |
| F5 | note | The exception filter on the stamp is narrower than "never fails the delivery" strictly needs. A catch-all would make AC 2 true by construction. No escaping type is plausible today (see AC 2). |
| F6 | note | Nothing is clamped, so clock changes and ordinary ordering show up as negative figures, with nothing to flag them. Probe: delivery before the request gave `delivered after -60.0s`. A handoff written before the turn ended (the session handed over in the same turn the stop arrived) gave `handoff -5.0s later`. A request time in the future gave `-2.0m after it was asked`. These are true to the recorded times, as AC 8 asks, but a reader may take them for a bug. |
| F7 | note | Wording and format. Ages round at the boundary: 59.98 min prints `60.0m` and 60 min prints `1.0h`, while 0 s and 1 s both print `0.0m` and 100 days prints `2400.0h`. The reason is rendered by Serilog with quotes (`("StartedAgain", 5.0m ...)`), as a PascalCase enum name. The plan calls the values "structured properties, so the log can be queried", but the seconds and the age are pre-formatted strings, not numbers. Culture is safe: under de-DE every value used `.` as the separator (`InvariantCulture`, `StopTiming.cs:107-113`). A null `HandoffPath`, or a handoff deleted before removal, gives `delivered, but no handoff was written` with no exception. |

### Probe output (de-DE culture), from the Theseus log format

```
Stop of probe-ws did not finish ("StartedAgain", 5.0m after it was asked): delivered, but no handoff was written      <- HandoffPath null
Stop of probe-ws did not finish ("NewerSession", 5.0m after it was asked): delivered, but no handoff was written      <- handoff file missing
Stop of probe-ws finished in 60.0s: delivery time not recorded, handoff (not recorded)s after the request, /exit (not recorded)s after that   <- delivery WAS stamped (F1)
Stop of probe-ws finished in 51.3s: delivered after -60.0s, handoff 90.0s later, /exit 21.3s after that              <- delivered before requested
Stop of probe-ws finished in 55.0s: delivered after 35.0s, handoff -5.0s later, /exit 25.0s after that               <- handoff before delivery
Stop of probe-ws finished in 55.0s: delivery time not recorded, handoff 30.0s after the request, /exit 25.0s after that  <- old hook
Stop of probe-ws did not finish ("Declined", -2.0m after it was asked): ...
Stop of probe-ws did not finish ("Expired", 0.0m | 0.0m | 0.5m | 60.0m | 1.0h | 2400.0h after it was asked): ...   <- 0 s, 1 s, 30 s, 59.98 min, 1 h, 100 d
old shape waiting DeliveredUtc null: True ; old hook claim DeliveredUtc null: True
read-only claim: thrown=none claimed=True deliveredUtc=null pendingLeft=0
sweep removed 1, second sweep removed 0   (one "Expired" line, 5.0h)
```

## Comparison with implementation.md and plan.md

- These claims match what was found: the full suite at 1448 of 1448, and 0 warnings; 10 test cases in `StopTimingTests`; 2 tests added to `StopRequestStoreTests`, with the existing tests untouched; the default-parameter deviation; `StopDurations` with five fields; the Debug line removed; the App wiring left to the live check; and the installed hook not stamping until Theseus is updated.
- These claims do not match: the plan's "rewrites it ... through `AtomicFile`" (F2), which is not done and is not listed as a deviation; the plan's step-2 test that "a request written by the older shape still parses", which the added test does not do (F3); and "Values are structured properties, so the log can be queried", which holds only as strings (F7). `implementation.md` says it reports at "the five places", and that is accurate for `ShellViewModel`, with `Sweep`'s two in Core listed separately.

## Verdict, part 1

**PASS.** The suite passes (1448 of 1448). AC 1 to 9 are met on the normal path in code. The App-level properties (AC 5 exactly once at each site, AC 6) are explicitly left to the part-2 live check. No defect was found. Before that check or a later change, consider the gaps: F1 (the wrong "delivery time not recorded" wording when only the handoff time is missing) and F3 (no tests for the stamp-failure path, the log lines or old-shape JSON). F2 (the non-atomic stamp, a departure from the plan) is the one note that touches stopping, and only in a millisecond race.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| verify part 1 | 2026-11-18 14:14:34 (observed, `date`) | 2026-11-18 14:19:18 (observed, `date`, before writing this file) | Suite ran 14:14:59.292 to 14:17:05.924 (workstream log). Probe ran at 14:18:10. |
