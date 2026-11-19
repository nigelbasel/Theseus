# parked-cores: evidence

Pipeline: `feature` (method kit v0), stage **verify, part 1 of 2, attempt 2** (re-verify of the fix round). Part 2 is a
live check on a running board by Nigel. Autonomy L0. Branch `main`, repo Theseus. Change under test: commit
`5a3fa7c` ("parked-cores: fix what verify part 1 found"), against the findings in `evidence-part1-attempt1.md`.
Verifier: a fresh sub-agent started by the implementer (partial independence: same model, briefed by the implementer,
did not write the code). Contract: `work/parked-cores/spec.md` AC 1 to 9. The "Pass 2" section of `implementation.md`
was read only after checks 1 to 3 were done.

## Checks

| # | Check | Result | Evidence |
|---|---|---|---|
| 1 | Full suite `dotnet test Theseus.slnx --nologo --logger "console;verbosity=normal"` | **PASS** | Started 2026-11-19 10:34:47.114, ended 10:36:55.423. "Test Run Successful. Total tests: 1547, Passed: 1547", exit code 0. The build output has no warning or error lines. No CS0009 or file-in-use error, so no rerun. Every line was appended, timestamped, to the workstream log under `<parked-cores-verify-2>`. `PdhCoreCountersTests.TwoReads_GiveEveryLogicalProcessor_WithTheParkedOnesAmongThem` **ran and passed [2 s]**; it was not ignored. All five `ParkedCoresFixTests` cases and the changed `CoreClampTextTests` test passed |
| 2a | F1: the tooltip names the setting that governs parking | **PASS** | `CoreClampText.cs:27` now reads "Sometimes the power plan's minimum cores setting (core parking)." That is the setting Windows uses to govern parking: "Processor performance core parking min cores", under Processor power management. It matches the spec's "the power plan's minimum cores setting". The changed line in `CoreClampTextTests.cs:24` **corrects** a wrong expectation; it does not weaken the test. It was an exact-text `Does.Contain` for the wrong setting, and it is still an exact-text `Does.Contain`, now for the right one. The new test also asserts `Does.Not.Contain("processor state")`, which is stricter |
| 2b | F2/F3: every failure is logged once and stops the reader, and nothing escapes to the board | **PASS as written; see N1** | `ReadLocked` now has `catch (Exception)` around everything after the `_broken` check (`PdhCoreCounters.cs:85`), so no exception can leave `Read`. `Values` returns null when the size call gives 0, or when the data call returns anything other than 0 (`:155-167`), and `ReadLocked` turns that into `Fail("could not be read")` (`:67-70`). `Fail` logs only while `_broken` is false, sets it, and closes the query (`:131-142`). The open, collect, array-read and exception paths now all go through `Fail`. The probe ran the real reader: after the failure there was one `[WRN] The processor counters could not be read...` line, and every later read was null. Normal situations: the first read after opening still returns null without going through `Fail` (`:57-62`), and per-item bad statuses are still filtered without failing (`:178`). **But a transient whole-array status does stop the reader for good**: see N1 |
| 2c | F4: do the new tests fail on the wrong code? | **FAIL (gap not closed)** | Mutation harness: copies of `CoreClampWatch.cs`, `CoreSample.cs`, `CoreClampState.cs` and `CoreClampSettings.cs` in the scratch folder, with the four new rule tests' assertions run against each copy. (1) `Math.Max(1, HeldSamples)` replaced by `HeldSamples`: `AHoldOfZeroOrLess_ActsAsOne(0)` and `(-2)` **still pass**, because a clamp sample gives a run of 1 and 1 >= 0. The real damage this mutation does (with a hold of 0, an **idle** sample shows the warning, so it shows permanently) goes untested; the harness's extra probe shows `True`. (2) `&& reading.Parked < reading.Total` removed: `EveryProcessorParked_IsNoClamp...` **still passes**, because its sample has busy 0, below the 90 line. The same sample with busy 100 shows a clamp on the mutant (`True`) and not on the real code. No existing `CoreClampWatchTests` case catches either mutation either: none uses a hold below 1 or a sample with every processor parked. `AParkedShareAboveOne_NeverWarns` is a valid statement but guards no particular line. F4 (a), (c) and (d) of attempt 1 have no new test |
| 2d | Shutdown race: Read and Dispose exclude each other, with no deadlock and no long hold | **PASS, with note N2** | `Read` and `Dispose` both `lock (_gate)` (`:32`, `:96`). Inside the lock the only calls are PDH and `TheseusLog`, which is a Serilog file sink (`TheseusLog.cs:32-38`) with no dispatcher or UI marshalling. Nothing inside takes another lock that the UI thread holds, so there is no deadlock path. The lock is held for a collect plus two formatted reads, under 1 ms by the class's own note. The first read also opens the query, about 0.4 s per the class's note [not recorded by me], so a `Dispose` that comes during the board's first sample waits up to that long. Probe: a thread doing 50 reads while the main thread disposed after 5 ms finished at 10:37:52.435 with no exception |
| 3 | Anything else changed | **PASS** | `git show HEAD --stat`: `CoreClampText.cs` (1 line), `PdhCoreCounters.cs`, `CoreClampTextTests.cs` (the one line in 2a, plus a reason message), new `ParkedCoresFixTests.cs`, `evidence-part1-attempt1.md` (the attempt 1 evidence, added) and `implementation.md`. No test was removed, and no other existing test changed. No new warnings (check 1). Behaviour changes beyond F1 to F3 and the lock: none found. The new inline `//` comments are allowed by `AGENTS.md` ("Inline `//` comments are allowed where the code cannot say why on its own") |
| 4 | "Pass 2" section of `implementation.md` | **Matches, with one overstatement** | Below |

### Probe of the real reader (this machine, 20 logical processors)

These are throwaway consoles in the scratch folder, `...\scratchpad\verify-parked-cores-2\probe` and `probe2`. They
reference the `Theseus.Core.dll` built by check 1. Times are from each probe's own clock.

| Time | Run | Result |
|---|---|---|
| 10:37:45.316 to 10:37:45.801 | `PdhCoreCounters.Read()` back to back, 200 times | 33 readings, then the reader logged "could not be read" once at 10:37:45.800 and returned null from then on (167 nulls, then 100 nulls at 10 ms apart and 5 nulls at 1 s apart) |
| 10:38:12 | Raw PDH calls with the reader's own sequence, 300 collects back to back | The parking array always returned 0. The busy array's data call returned **0x800007D6 (PDH_CALC_NEGATIVE_DENOMINATOR) 4 times in 300**, and 0 otherwise. The size call always returned PDH_MORE_DATA with a non-zero size |
| 10:38:26 to 10:39:57.932 | `PdhCoreCounters.Read()` 90 times, 1 s apart | 1 null (the first, by design), no failure logged |

The board reads every 5 s (`ResourcesViewModel.Interval`). The failure was not seen at 1 s spacing in 90 reads. A 5 s
spacing was not run.

## Findings of attempt 1

| # | Status | Evidence |
|---|---|---|
| F1 (defect) | **Fixed** | Check 2a |
| F2 (gap) | **Fixed** | Check 2b: no exception can leave `Read`; logged once |
| F3 (gap) | **Fixed, overcorrected** | Check 2b: an array failure is now logged once and stops the reader. That includes a transient status (N1) |
| F4 (gap) | **Not fixed** | Check 2c: the new rule tests pass on both mutations they were meant to guard against. (a) layout, (c) logged-once and start/stop logging, and (d) the fragile regex have no new test; (a) and (c) are recorded in `implementation.md` |
| F5 to F7 (notes) | Unchanged | Not in scope of this round |
| F8 (note) | **Fixed** | Check 2d |

## New findings

| # | Severity | Finding |
|---|---|---|
| N1 | **gap (moderate; low likelihood at the board's pace)** | `PdhGetFormattedCounterArray` can return a **transient** calculation status for a rate counter: PDH_CALC_NEGATIVE_DENOMINATOR was observed 4 times in 300 back-to-back collects on this machine. `Values` treats any non-zero return as a failure, so one such status now turns the warning off for the life of the board, with one Warning line. Before this round the same status gave null for one sample. The probe reproduced this end to end through `PdhCoreCounters.Read()`. It was not seen at 1 s spacing in 90 reads, and 5 s spacing was not run. Other causes of a negative denominator in real use (sleep and resume, clock changes) were not tested. The direction is safe (a missed warning, never a false one), and `implementation.md` records it as accepted. The evidence here shows it is a real PDH behaviour, not a hypothetical one. A narrower fix would keep PDH_CALC_NEGATIVE_* and PDH_INVALID_DATA as "no reading this sample" and treat only the other codes as broken. That is for the owner to weigh |
| N2 | note | After `Dispose`, a `Read` that was already queued on the pool thread takes the lock, finds `_query` zero and `_broken` false, and **opens the query again** (`:47`). The lock prevents use-after-close but not reopen-after-dispose. At shutdown this leaks one PDH query until the process exits. It is harmless, and it was already true before this round |
| N3 | note | `implementation.md`'s F4 row ("Tests added for a hold of zero or less...") reads as closing F4, but per check 2c the added tests would not fail if `Math.Max` or `Parked < Total` were removed. Its own line, "the four rule tests cover behaviour that was already right", is accurate |

### Check 4: the "Pass 2" section of implementation.md

- The F1, F2, F3 and shutdown-race rows match the code (checks 2a, 2b and 2d).
- "new file ... (5)": correct (4 methods, one with 2 cases). "One existing test changed": correct, and it is a
  correction (check 2a). "1547 of 1547, 0 warnings": matches check 1. "The wording test failed before the fix": [not
  recorded], not checkable from the commit. It would fail against the old text by inspection.
- The F4 row overstates (N3): the tests exist, but they do not guard the lines F4(b) named.
- "Recorded, not fixed": the layout and logged-once behaviour have no unit test (true); a running core with no valid busy
  value counts as 0% (true, `:77`); a failed read stops the reader until restart, so a transient PDH error would leave
  the warning off (true, and now **observed**: N1). The list does not mention F4(d) or N2.

## Verdict, part 1 (attempt 2)

**PASS, with findings.** The one defect from attempt 1, F1, is fixed correctly. The changed test corrects a wrong
expectation and does not weaken it. F2, F3 and the shutdown race (F8) are fixed. The suite passes 1547 of 1547 with the
real counter read running, and no AC is unmet. Two things for Nigel to weigh before or during part 2:

- **N1:** a transient PDH status, observed on this machine, now switches the warning off until the board restarts.
- **F4 is not closed:** the new tests would still pass with `Math.Max` or `Parked < Total` removed.

Neither causes a false warning.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| verify part 1, attempt 2 | 2026-11-19 10:34:24 | 2026-11-19 10:40:20 (last clock read before writing this file) | Suite 10:34:47.114 to 10:36:55.423. Mutation harness 10:37:21 to 10:37:33+. Reader probes 10:37:45 to 10:39:58 |
