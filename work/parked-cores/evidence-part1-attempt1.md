# parked-cores: evidence

Pipeline: `feature` (method kit v0), stage **verify, part 1 of 2** (code and tests; part 2 is a live check on a running
board by Nigel), autonomy L0. Card git-22, branch `main`, repo Theseus. Change under test: commit `ce2eecf`.
Verifier: a fresh sub-agent started by the implementer (partial independence: same model, briefed by the implementer,
did not write the code). Contract: `work/parked-cores/spec.md` AC 1 to 9. The three figures (50%, 90%, 3) are taken as
approved; only whether the code implements them was checked.

## Checks

| # | Check | Result | Evidence |
|---|---|---|---|
| 1 | Full suite `dotnet test Theseus.slnx --nologo --logger "console;verbosity=normal"` | **PASS** | Started 2026-11-19 09:59:10, finished 10:01:25. "Test Run Successful. Total tests: 1542, Passed: 1542", exit code 0. No warning or error lines in the build output; no CS0009 or file-in-use, so no rerun. Every line appended, timestamped, to `C:\Users\nigel\.claude\logs\git-22.log` under `<parked-cores-verify>`. `PdhCoreCountersTests.TwoReads_GiveEveryLogicalProcessor_WithTheParkedOnesAmongThem` **ran and passed [2 s]**, not ignored |
| 2 | AC 1 to 9 against the code | **FAIL on AC 7**, rest met or left to the live check | Table below |
| 3 | Code's reading against `Get-Counter`, same moment, this machine | **PASS** (parked count); busy and clock consistent within the sampling noise | Below |
| 4 | Do the new tests test the spec's points; could they pass with wrong code; existing tests changed? | **PASS with gaps** | No existing test file changed (`git show ce2eecf --stat`: only the four new test files under `tests/`). Gaps under Findings |
| 5 | `implementation.md` and `plan.md` claims, including the list of differences | **PASS with one undisclosed difference** | Below |

### Check 3: the code's figures against Get-Counter

A throwaway console outside the repo (`...\scratchpad\verify-parked-cores\probe`, referencing the built
`Theseus.Core.dll`) called `PdhCoreCounters.Read()` once a second; `Get-Counter` ran at the same time in Windows
PowerShell over `\Processor Information(*)\Parking Status`, `% Processor Time` and `(_Total)\% Processor Performance`,
averaging `% Processor Time` over the instances not parked, the same way the code does. Times are each tool's own clock.

| Time (code) | Code: parked / total, running busy, clock | Time (Get-Counter) | Get-Counter: parked / total, running busy, clock |
|---|---|---|---|
| 10:02:23.289 | null (first read, as designed) | | |
| 10:02:24.327 | 5 / 20, 46.5%, 154.3% | | |
| 10:02:25.331 | 8 / 20, 59.0%, 153.1% | | |
| 10:02:26.333 | **10 / 20**, 49.7%, 156.1% | 10:02:26.812 | **10 / 20**, 40.9%, 165.8% |
| 10:02:27.337 | 12 / 20, 34.5%, 166.8% | | |
| 10:02:28.353 | **0 / 20**, 55.0%, 153.9% | 10:02:28.153 | **0 / 20**, 43.0%, 158.1% |
| | | 10:02:29.198 | 11 / 20, 63.3%, 148.3% |

- The parked count agrees at both near-simultaneous pairs (10 and 10; 0 and 0). Total is 20, equal to the 20 instances
  Get-Counter lists (`0,0` to `0,19`), so both `_Total` rows Get-Counter also lists (`0,_total`, `_total`) were left
  out by the code. Busy and clock differ by a few points, which fits two rate counters taken over different one-second
  windows on a machine whose load was moving (the parked count itself swung 0 to 12 within five seconds).
- This also shows the hand-read `PDH_FMT_COUNTERVALUE_ITEM_W` layout working on this 64-bit process for both formats:
  LARGE (parking 0/1 values) and DOUBLE (busy in 0 to 100, clock about 150).

## Acceptance criteria

| AC | Result | Evidence |
|---|---|---|
| 1 Read each sample, off the UI thread, per processor parked and busy, no admin | **Met** (no-admin and on-the-board left to the live check) | `ResourcesViewModel.cs:111-119`: `_cores.Read()` runs inside the existing `Task.Run`, so the first-read `PdhOpenQuery` cost is also off the UI thread. Counters `PdhCoreCounters.cs:89-90`: `\Processor Information(*)\Parking Status` and `\Processor Information(*)\% Processor Time` (time-based busy per logical processor, the right counter for "saturated"). Layout `PdhCoreCounters.cs:141-156`: stride `IntPtr.Size + 16` = 24 on 64-bit; name pointer at 0, `CStatus` at 8, 4 bytes padding, value at 16 read as Int64 for LARGE and reinterpreted as double for DOUBLE: correct for x64/ARM64. Join by instance name (`:62`), both sets from the same object so `"0,3"` keys match; group names handled. `_Total` filter `:150` is `Contains("_Total", Ordinal)`, which drops `_Total` and group totals `"0,_Total"`; Check 3 shows 20 of 20 after the filter. The test suite's read ran as an ordinary user |
| 2 Unreadable: no warning, nothing else changes, logged once | **Met for open and collect failures; gap for the others** | `Fail` (`:106-117`) logs only while `_broken` is false, then closes the query and every later `Read` returns null (`:30-33`). First read returns null with no log (`:47-52`); `Observe(null)` gives no warning (`CoreClampWatch.cs:25`). Not thrown for `DllNotFound`, `EntryPointNotFound`, `OutOfMemory` (`:70`). Gaps F2, F3 |
| 3 The rule | **Met** | `CoreClampWatch.cs:25-28`: `Parked/Total >= ParkedShare` and `RunningBusyPercent >= RunningBusyPercent`, both inclusive. Adds `Parked < Total` (disclosed). `ParkedShare` above 1 can never be reached, so the warning never shows; NaN likewise |
| 4 Starts after N in a row, stops at the first non-clamp sample | **Met** | `CoreClampWatch.cs:34-39`: the run resets to 0 on any non-clamp or null sample; standing is `run >= Math.Max(1, HeldSamples)`, so 0 or negative behaves as 1 (shows on the first clamp sample); `Started` and `Stopped` are edges only |
| 5 Three settings with the defaults, in the settings file, not the window | **Met** | `CoreClampSettings.cs:11-17` (0.5, 90, 3). `TheseusSettings.cs:78` `CoreClamp { get; init; } = new()`: a file without the key reads the defaults (positional record with init properties; test `ASettingsFileWritten_BeforeTheWarningExisted_ReadsTheDefaults`). Written back: every settings write in the App is `store.Read() with { ... }` (e.g. `SettingsViewModel.cs:374`, `BoardViewModel.cs:485`), so the section is carried and serialised as `coreClamp`. Not in the settings window (no reference in `SettingsViewModel`). Read once at start (`App.xaml.cs:166`), disclosed. A hand-written `"coreClamp": null` falls back to defaults (`ResourcesViewModel.cs:52`) |
| 6 Amber status text after the machine line | **Met in code; appearance left to the live check** | `MainWindow.xaml:1893-1901`, directly after the `MachineText` block, `#D68910` (the colour the board's other flags use, e.g. `MainWindow.xaml:528`), hidden when `ClampText` is empty. Text `CoreClampText.cs:13`, test exact. Firing path cannot be shown live (spec, P13) |
| 7 Tooltip: meaning, what to check (heat; sometimes the power plan's **minimum cores** setting), the figures | **Not met** | `CoreClampText.cs:27` says "Sometimes the power plan's minimum processor state." That is a different power setting (the frequency floor), not the core parking minimum the spec names. Defect F1. The rest is met: meaning `:25`, heat `:26`, figures `:21-22` |
| 8 One log line each at start and stop, with parked/total, running busy, clock | **Met** | `ResourcesViewModel.cs:150-159`: logged only on the `Started`/`Stopped` edges, so once each per episode. Figures `CoreClampText.cs:44-46`; the clock is left out when unread, and a stop caused by an unreadable sample says so (`:40-41`). Start as Warning, stop as Information (disclosed) |
| 9 Nothing else changes | **Met, with a gap; left to the live check** | Only additions to the status bar, view model and settings. Gap F2: the read shares the machine sample's `Task.Run`, so an exception it does not catch would lose that tick's machine line |

## Findings

| # | Severity | Finding |
|---|---|---|
| F1 | **defect** | AC 7: the tooltip names "the power plan's minimum processor state" (`CoreClampText.cs:27`), but the spec names the minimum **cores** setting (core parking's minimum, "Processor performance core parking min cores"). They are different settings: someone who follows the tooltip would check the wrong one. The plan also names the minimum-cores setting (`plan.md`, Verification). The test `CoreClampTextTests.cs:24` asserts the wrong wording, so it passes |
| F2 | gap | AC 2/9: `Read` catches only `DllNotFoundException`, `EntryPointNotFoundException` and `OutOfMemoryException` (`PdhCoreCounters.cs:70`). Anything else escapes into `SampleAsync`'s catch (`ResourcesViewModel.cs:138-141`): that tick's machine line and card figures are not updated, and the warning "Could not read what the sessions are using" is logged on every sample, not once. No such exception was seen; the risk is low |
| F3 | gap | AC 2: if `PdhGetFormattedCounterArray` fails, or every instance has a bad status, `Values` returns empty and `Read` returns null (`:57-60`, `:136-139`) with nothing logged and no switch to broken, so that failure is logged zero times, not once. No warning shows, so the visible behaviour is right |
| F4 | gap | Tests: (a) `PdhCoreCountersTests` cannot fail on a wrong layout or a wrong parked rule: busy is clamped to 0..100 before the range check, `Parked` in 0..Total is always true, and a wrong status offset would make the read empty and turn the test into Ignore rather than a failure; Check 3 is the only evidence the parked count is right. (b) No test for `HeldSamples` 0 or negative, `ParkedShare` above 1, or all processors parked; removing `Math.Max(1, ...)` would make `HeldSamples = 0` show the warning permanently and no test would fail. (c) No test that the failure is logged once, or that start and stop are each logged once (`ResourcesViewModel.ShowClamp` has no test; disclosed in the plan's risks). (d) `ASettingsFileWritten_BeforeTheWarningExisted_ReadsTheDefaults` does not assert that its regex removed the section; it does today (camelCase `coreClamp`, not the first property, no nested braces), so the test is valid but fragile |
| F5 | note | The busy for a running processor with no valid busy value is taken as 0 (`busy.GetValueOrDefault`, `:62`), which lowers the average and so can only suppress a warning |
| F6 | note | On a 32-bit process the layout would be wrong (stride 20 and status at 4; the real item is 24 with the value union aligned to 8). The projects are AnyCPU without Prefer32Bit, so this runs 64-bit; only matters if the board were ever built x86 |
| F7 | note | Parking Status is an instant value while busy is a rate over the 5-second interval; on this machine the parked count swung 0 to 12 within five one-second reads at moderate load (Check 3). The three-sample hold covers this; worth watching in the live check, not a defect |
| F8 | note | `Dispose` closes the PDH query on the UI thread while a sample may be reading on a pool thread; at worst this logs the open/collect failure once at shutdown |

### Check 5: implementation.md and plan.md

- The file table matches the commit's `--stat`. "20 tests" is right: CoreClampWatchTests 12 cases (8 methods, one with
  4 cases), CoreClampTextTests 4, PdhCoreCountersTests 1, CoreClampSettingsTests 3. "1542 of 1542" and "the real read
  ran (not ignored)" match Check 1. "0 warnings": no warning lines in this run's output. "No existing test file changed"
  matches. "Failing to build before the code": [not recorded], not checkable from the commit.
- The listed differences from the plan (`CoreClampState`, `IDisposable`, all-parked is no clamp, Warning/Information,
  optional constructor argument read once, Get-Counter comparison moved to verification) are each true in the code.
- **Undisclosed difference:** the plan says "Any failure is logged once and gives null from then on"; the
  formatted-array failure path (F3) neither logs nor gives up. The tooltip wording (F1) is a departure from the spec
  that the differences list does not mention either.
- The "for the verifier" notes are accurate: the layout is as described, and the real-read test indeed cannot check the
  parked count (F4a).

## Verdict, part 1

**FAIL.** The suite passes (1542 of 1542) and the reading matches Get-Counter on this machine, but AC 7 is not met:
the tooltip points at the power plan's minimum processor state instead of its minimum cores setting (F1, defect). Every
other AC is met or left to the live check. F2 to F4 are gaps to weigh, not blockers.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| verify part 1 | 2026-11-19 09:57:55 | 2026-11-19 10:03:53 (last clock read before writing this file) | Suite 09:59:10 to 10:01:25; counter comparison 10:02:23 to 10:02:29 |
