# parked-cores: evidence

Pipeline: `feature` (method kit v0), stage **verify, part 1 of 2, attempt 3** (re-verify of pass 3). Part 2 is a live
check on a running board by Nigel. Autonomy L0. Card git-22, branch `main`, repo Theseus. Change under test: commit
`f21d57c` ("parked-cores: a temporary PDH status skips one sample instead of stopping the reader"), against N1 and F4 in
`evidence-part1-attempt2.md`. Verifier: a fresh sub-agent started by the implementer (partial independence: same model,
briefed by the implementer, did not write the code). Contract: `work/parked-cores/spec.md` AC 1 to 9. The "Pass 3"
section of `implementation.md` was read only after checks 1 to 3 were done.

## Checks

| # | Check | Result | Evidence |
|---|---|---|---|
| 1 | Full suite `dotnet test Theseus.slnx --nologo --logger "console;verbosity=normal"` | **PASS** | Banner 2026-11-19 10:49:52.917, end line 10:52:10.588. "Test Run Successful. Total tests: 1555, Passed: 1555", exit code 0. No warning or error lines in the build output. No CS0009 or file-in-use error, so no rerun. Every line was appended, timestamped, to `C:\Users\nigel\.claude\logs\git-22.log` under `<parked-cores-verify-3>`. All 8 `ParkedCoresPass3Tests` cases ran and passed. `PdhCoreCountersTests.TwoReads_GiveEveryLogicalProcessor_WithTheParkedOnesAmongThem` ran and passed [1 s] |
| 2a | N1: is the severity test right for PDH's status codes? | **PASS** | `PdhStatus.IsTemporary` is `(status & 0xC0000000) == 0x80000000`. Checked against the SDK header `C:\Program Files (x86)\Windows Kits\10\Include\10.0.26100.0\um\PdhMsg.h`: it defines 2 success codes (0x00000000 `PDH_CSTATUS_VALID_DATA`, 0x00000001 `PDH_CSTATUS_NEW_DATA`), 15 warning codes (0x800007D0 to 0x800007DC, 0x80000BD5, 0x80000BF3) and 69 error codes (0xC...). There are **no informational (0x4xxxxxxx) codes**. So the mask picks exactly the warning codes. Success (0) never reaches the test, because both call sites check `!= 0` first. A Win32 code (top bits 00, non-zero) is not temporary and stops the reader, which is the safe direction. The test cases' names are right: 0x800007D6 is `PDH_CALC_NEGATIVE_DENOMINATOR`, 0x800007D5 is `PDH_NO_DATA`, 0xC0000BC6 is `PDH_INVALID_DATA`, 0xC0000BB8 is `PDH_CSTATUS_NO_OBJECT` |
| 2b | N1: the experiment repeated through `PdhCoreCounters.Read()` | **PASS** | A throwaway console (`...\scratchpad\verify-parked-cores-3\probe`) references the `Theseus.Core.dll` built by check 1, logs to its own folder, and reads `_broken` by reflection after each read. Results are in the table below. In 1,600 back-to-back reads the reader **never stopped**, and no Warning line was logged (the log has only the three "started" lines). The skips are scattered single reads or short runs of 2 to 3, and readings resume immediately after each |
| 2c | N1: does a status that should stop the reader now get skipped forever? | **PASS, with note N4** | Of the 15 warning codes, the ones these calls can return are mainly the calculation ones (`PDH_CALC_NEGATIVE_*`), `PDH_MORE_DATA` (the size call, which the data call overwrites) and `PDH_NO_DATA` / `PDH_CSTATUS_NO_INSTANCE` / `PDH_CSTATUS_ITEM_NOT_VALIDATED`. The real failure modes (invalid handle, no object, no counter, access, invalid data) are all 0xC and still stop the reader. Skipping the warning codes is correct for the calculation statuses. A warning status that **recurs on every read** (for example `PDH_NO_DATA` from the collect, which PDH documents for a query whose counters return nothing) would now skip forever without the one log line. The end state, no warning, is the same as stopping, so this is a note (N4). Not observed. In the raw tally, the only non-zero status in 1,000 collects was 0x800007D6 |
| 3a | F4: do the new guard tests fail when a guard is removed? | **PASS** | Mutation harness in `...\scratchpad\verify-parked-cores-3\mut-original`, `mut-nomax` and `mut-nototal`, each with copies of `CoreClampWatch.cs`, `CoreSample.cs`, `CoreClampState.cs` and `CoreClampSettings.cs`. The three new guard assertions were transcribed into a console. **Original:** 3 of 3 pass. **`Math.Max(1, _settings.HeldSamples)` replaced by `_settings.HeldSamples`:** `AHoldOfZeroOrLess_StillNeedsAClamp_IdleNeverWarns(0)` and `(-2)` **fail** (an idle sample stands the warning). **`&& reading.Parked < reading.Total` replaced by `&& true`:** `EveryProcessorParked_IsNoClamp_EvenIfItsBusyFigureIsHigh` **fails**. Each mutation is caught by the test meant for it |
| 3b | Anything else changed | **PASS** | `git show f21d57c --stat`: `PdhCoreCounters.cs`, new `PdhStatus.cs`, new `ParkedCoresPass3Tests.cs`, `evidence-part1-attempt2.md` (the attempt 2 evidence, added; it was not tracked before) and `implementation.md`. **No existing test file changed or removed.** In `PdhCoreCounters.cs` the changes are limited to the status plumbing (`Values` gains `out uint status`, the per-item local is renamed `itemStatus`, the collect and read failures go through `IsTemporary`, and the Fail reasons now carry the status in hex). No new warnings (check 1). The verify left the tracked files untouched: after writing this file, `git status --short` shows only `?? work/parked-cores/evidence.md` |
| 4 | "Pass 3" section of `implementation.md` | **Matches** | Below |

### Probe of the real reader (this machine, 20 logical processors)

Times are from each probe's own clock.

| Time | Run | Result |
|---|---|---|
| 10:54:37.719 to 10:54:39.629 | `Read()` 300 times back to back | 1 leading null (priming), 288 readings, **11 skipped**, never stopped |
| 10:54:39.774 to 10:54:40.541 | `Read()` 300 times back to back | 1 leading null, 286 readings, **13 skipped**, never stopped |
| 10:54:40.732 to 10:54:42.070 | `Read()` 1,000 times back to back | 2 leading nulls (the first read after priming was itself skipped), 957 readings, **41 skipped**, never stopped |
| 10:54:58.804 to 10:54:59.852 | Raw PDH, the reader's own call sequence, 1,000 collects | Collect: 0 every time. Parking array: 0 every time. Busy array data call: **0x800007D6 10 times**, 0 otherwise |

The skip rate through `Read()` (3.7% to 4.3%) is higher than the raw loop's (1.0%) and attempt 2's (4 of 300). The
reader cannot say why it returned null, so the skips are not attributed code by code; the raw tally shows no status
other than 0x800007D6. A 5 s spacing (the board's pace) was not run.

## N1 and F4

| # | Status | Evidence |
|---|---|---|
| N1 (gap) | **Fixed** | Checks 2a to 2c: warning-level statuses skip one sample; error-level ones still stop the reader, logged once with the status. 1,600 back-to-back reads, 65 skipped, never stopped |
| F4 (gap) | **Closed for (b)** | Check 3a: both guards now have a test that fails without them. F4 (a) layout, (c) logged-once and (d) the regex still have no test; `implementation.md` records (a) and (c) |

## New findings

| # | Severity | Finding |
|---|---|---|
| N4 | note | A warning-level status that recurs on every read would now give null forever with no log line, where pass 2 logged once. The board shows the same thing either way (no warning), so only the trace is lost. Not observed here |
| N5 | note (low) | Now that a skipped sample is the expected path, note how it is used: `ResourcesViewModel` passes the null to `CoreClampWatch.Observe` (`ResourcesViewModel.cs:150`), which resets the run to 0. During a real clamp, one skipped sample hides a standing warning, and it needs `HeldSamples` (3, about 15 s) more samples to show again. This is the spec's rule ("a sample that could not be read ... does not [show a clamp]"), and the direction is safe (a missed warning, never a false one). The skip rate at the board's 5 s pace is [not recorded]; back to back it is about 4% |
| N6 | note | The skip behaviour inside `Read()` has no unit test; only the classifier does. The real-counter test and this probe exercise it, but would not fail if a call site stopped using `IsTemporary` |

### Check 4: the "Pass 3" section of implementation.md

- N1 row: matches the code (checks 2a and 2b). One small imprecision: "only error-level statuses stop the reader". A
  non-zero status with top bits 00 (not a PDH code), or a size call that returns 0 with size 0, also stops it. Both are
  the safe direction.
- F4 row: matches check 3a. Both claims ("fails without `Math.Max(1, ...)`", "fails without `Parked < Total`") hold.
- "new file ... (8)": correct (5 + 2 + 1 cases). "No existing test file changed": correct (check 3b). "1555 of 1555, 0
  warnings": matches check 1.
- The tests-first admission (the `PdhStatus` test never failed before the code) is consistent with the commit, which
  adds the class and its test together. It cannot be checked further from the history. The statement that the guard
  tests pass on the current code and exist to fail when a guard goes is exactly what check 3a shows.
- "Still recorded, not fixed": all four items are accurate. The last is N2 from attempt 2. N4 to N6 are not listed.

## Verdict, part 1 (attempt 3)

**PASS.** N1 is fixed: the severity test matches PDH's status codes, and through `PdhCoreCounters.Read()` 1,600
back-to-back reads skipped 65 samples without the reader ever stopping. F4 (b) is closed: each guard's removal fails
its new test. The suite passes 1555 of 1555 with no warnings and no existing test changed. No AC is unmet. The new
findings N4 to N6 are notes. N5 is worth Nigel watching in part 2: a skipped sample briefly hides a standing warning.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| verify part 1, attempt 3 | 2026-11-19 10:49:36 | 2026-11-19 10:55:21 (last clock read before writing this file) | Suite 10:49:52.917 to 10:52:10.588. Mutation harness finished 10:54:15. Reader probes 10:54:37.719 to 10:54:42.070. Raw tally 10:54:58.804 to 10:54:59.852 |
