# stop-timing: evidence

Pipeline: `feature` (method kit v0), stage **verify, part 1 of 2**, **attempt 2** (a re-verify of the fix round).
Part 2 is a live check on a running board, by Nigel. Autonomy L0. Card git-22, branch `main`, repo Theseus, commit
`ee9a529` (the fix round), compared with `a565dd7`. The first attempt is in `evidence-part1-attempt1.md`.
Verifier: a fresh sub-agent started by the implementer. Independence is partial: same model, briefed by the
implementer, but it did not write the code. It read `implementation.md` only after checks 1 to 3.

## Checks

| # | Check | Result | Evidence |
|---|---|---|---|
| 1 | Full suite `dotnet test Theseus.slnx --nologo --logger "console;verbosity=normal"` | PASS | Total 1465, passed 1465, failed 0, total time 1.5121 min, exit 0. No compiler warning in the output: the only lines containing "warning" are the names of six tests. `Theseus.Core` and `Theseus.Tests` were rebuilt in this run. No CS0009 or file-in-use error, so no rerun. Every line is in `C:\Users\nigel\.claude\logs\git-22.log` under `<stop-timing-verify-2>`, from 14:26:47.513 (banner) to 14:29:12.994 (exit line). The 17 new test cases all ran and passed. |
| 2 | F1 to F7 | PASS | Table below. F1, F3, F5, F6 and F7 are fixed. F2 is mostly fixed and F4 is partly fixed. What is left is stated in `implementation.md` and is reasonable. |
| 3 | Nothing else changed | PASS | `git show ee9a529 --stat` touches only `ShellViewModel.cs`, `StopRequestStore.cs`, `StopTiming.cs`, the two test files and two `work/` notes. `git diff a565dd7 ee9a529 --numstat -- tests/` gives 39 and 67 lines added, 0 removed, so no existing test was changed. Stopping behaviour: `SessionClosing` is not in the diff. The close conditions, the 20 s quiet period, the expiry and `Sweep` are unchanged. In `ShellViewModel.cs:1322` `Retire` is still called under the same condition, and only the report is now guarded by its result. In `Claim`, the move and the parse are unchanged (`StopRequestStore.cs:86-101`). The new `File.Exists` early return (`:111-114`) returns the already-parsed request, so the delivery is unchanged. |
| 4 | `implementation.md` "Pass 2" against what was found | Matches | See the section at the end. |

## F1 to F7

| # | Status | Evidence |
|---|---|---|
| F1 | Fixed | `FinishedText` (`StopTiming.cs:64-85`) builds the delivery part and the handoff part separately. `Finished` (`:17-29`) can produce only four shapes, and each is worded correctly: all times present gives "delivered after X, handoff Y later, /exit Z after that". No delivery time gives "delivery time not recorded, handoff Y after the request, /exit Z after that". Delivery present with no handoff gives "delivered after X, handoff time could not be read", so the delivery time is no longer blamed. Neither time present gives "delivery time not recorded, handoff time could not be read". Each shape has an exact-match test (`StopTimingTests` `AFinishedStop...`). A wrong branch choice would now fail at least one test. |
| F2 | Mostly fixed (a known window remains) | `StopRequestStore.cs:111-116` checks `File.Exists`, then calls `AtomicFile.WriteAllText`, which writes `<id>.json.tmp` and then calls `File.Move(..., overwrite: true)` (`AtomicFile.cs:23-25`). A reader can no longer see half a file. A request removed between the check and the move can still be written back, but the window is now the time to write one small temp file, not the whole write. `implementation.md` says so. New note N1: a failed stamp leaves the `.tmp` file behind. |
| F3 | Fixed | (a) `Claim_StillDeliversWhenTheDeliveryTimeCannotBeWritten`: a read-only file still claims, with a null `DeliveredUtc`. If exceptions escaped `Claim`, it would fail. It cannot tell a catch-all from the old narrow filter, because `UnauthorizedAccessException` was caught by both, but the catch-all is plain to see in the code. (b) The wording and branch tests are listed under F1, plus the four removal reasons and six ages. (c) `ARequestFileWrittenBeforeDeliveryTimesExisted_IsStillRead` writes JSON by hand with no `deliveredUtc` property. If the field were required (`JsonRequired`), deserialising would throw `JsonException`, `Parse` would return null (`:242-245`), and `Is.Not.Null` would fail. So the test can fail on wrong code. |
| F4 | Partly fixed, knowingly | The newer-session site (`ShellViewModel.cs:1322-1325`) reports only if `Retire` succeeded. The finished site (`:1353-1354`) still ignores the result. The pre-existing "Retired..." line (`:1327`) is still unconditional. Leaving these is reasonable: only a failed delete triggers it, the store already logs that failure as a warning (`StopRequestStore.cs:199-203`), and the limit is stated in `implementation.md`. |
| F5 | Fixed | `catch (Exception)` at `StopRequestStore.cs:120`. The `with` copy at `:107` is outside the `try`, but copying a non-null record cannot throw. Nothing thrown while stamping can escape `Claim`. |
| F6 | Partly fixed | Finished lines get "(a negative figure means the clock or a file time moved)" when any figure is negative (`StopTiming.cs:79-82`, with a test). Unfinished lines do not: a request time in the future prints, for example, `-7200s after it was asked`, with no flag and in seconds rather than hours (N2). |
| F7 | Fixed | Ages are truncated (`StopTiming.cs:105-108`). Probe results: 59.9 s gives `59s`, 89.99 s gives `89s`, 90 s and 119 s give `1m`, 3599.9 s gives `59m`, 5399.9 s gives `89m`, 5400 s gives `1.5h`, 8280 s gives `2.3h`, 8640 s gives `2.4h`, 9360 s gives `2.6h` and 15120 s gives `4.2h` (no floating-point drop at the decimal), and 100 h gives `100.0h`. These match the tests' expectations, and the boundaries fall where the doc comment says. Reasons are now in words. The plan's promise of queryable values is withdrawn in `implementation.md`: each line is one `{Outcome}` string. Durations still use `InvariantCulture` (`:143-144`). |

## New findings

| # | Severity | Finding |
|---|---|---|
| N1 | note | When the atomic stamp fails at the move (for example, the read-only case), `<id>.json.tmp` stays in `delivered\`. Probe: after a read-only claim, both `...json` and `...json.tmp` were left, and `Handed()` counted 1. So the leftover is not read as a request, as `implementation.md` says. Nothing ever deletes it, though: `Retire` and `Sweep` handle only `<id>.json`. One leftover per failed stamp, overwritten by the next stamp for the same session. This is harmless clutter. |
| N2 | note | `UnfinishedText` has no flag for a negative age, and `Age` shows any negative value in seconds (`-7200s`), because it is below 90. This is rare: it needs a clock change, or a request time in the future. |
| N3 | note | No test covers the exact 90 s or 90 min boundary (the tests use 89 s and 5399 s). The probe shows these boundaries behave as documented. |

## Verdict, part 1 (attempt 2)

**PASS.** The full suite passes, 1465 of 1465, with no compiler warning. The fix round fixes F1, F3, F5 and F7.
It fixes F2 except for a stated microsecond window, and F4 and F6 in part, knowingly. It removes or changes no
existing test, and it does not change stopping behaviour. The new notes (N1 to N3) are minor and need no action
before the part-2 live check.

## Comparison with implementation.md "Pass 2"

It matches. Every row agrees with the code: the F2 window it states, the `.tmp` leftover not being read (confirmed
by the probe, though it never says the file is never cleaned up), the finished-site `Retire` limit, the withdrawn
structured-properties claim, and 1465 of 1465 with 0 warnings. It does not mention N2 (unfinished lines have no
negative flag), because F6 asked only about the finished line's figures.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| verify part 1, attempt 2 | 2026-11-18 14:26:27 (observed, `date`) | 2026-11-18 14:29:51 (observed, `date`, before writing this file) | Suite ran 14:26:47.513 to 14:29:12.994 (workstream log). The probe (a throwaway console app in the scratchpad that references the built `Theseus.Core.dll`) ran right after the suite, and its temp folder under `%TEMP%\stop-probe-*` was then deleted. |
