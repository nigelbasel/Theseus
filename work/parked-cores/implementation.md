# parked-cores: implementation

Pipeline: `feature` (method kit v0), stage **implement**, autonomy L0. Card git-22, branch `main`, repo Theseus.
Follows `plan.md` (approved by Nigel, 2026-11-19).

## What was done

| File | Change |
|---|---|
| `src/Theseus.Core/Resources/CoreSample.cs` | New record: total, parked, running cores' average busy, clock |
| `src/Theseus.Core/Resources/CoreClampState.cs` | New record: standing, started, stopped |
| `src/Theseus.Core/Resources/CoreClampWatch.cs` | New: `ShowsClamp` (AC 3) and `Observe` (AC 4) |
| `src/Theseus.Core/Resources/CoreClampText.cs` | New: status text, tooltip, start and stop log lines (AC 6 to 8) |
| `src/Theseus.Core/Resources/ICoreCounters.cs`, `PdhCoreCounters.cs` | New: the PDH reading, one query kept open; a failure logged once (AC 1, 2) |
| `src/Theseus.Core/Settings/CoreClampSettings.cs`, `TheseusSettings.cs` | New settings record with the three provisional defaults; `TheseusSettings.CoreClamp` (AC 5) |
| `src/Theseus.App/ViewModels/ResourcesViewModel.cs` | Reads the counters in the existing off-thread sample; `ClampText`, `ClampTooltip`; logs start (Warning) and stop (Information) |
| `src/Theseus.App/App.xaml.cs` | Passes the settings' figures to the resources view model |
| `src/Theseus.App/MainWindow.xaml` | The amber status-bar text, shown only while the warning stands |
| `tests/Theseus.Tests/Resources/CoreClampWatchTests.cs`, `CoreClampTextTests.cs`, `PdhCoreCountersTests.cs`, `tests/Theseus.Tests/Settings/CoreClampSettingsTests.cs` | New, 20 tests, failing to build before the code. The real read on this machine ran (not ignored) |

Full suite **1542 of 1542**, 0 warnings. No existing test file changed.

## Every difference from the plan

- **`CoreClampState`** is a type the plan did not list: `Observe` returns it (standing, started, stopped), which the
  plan described without naming.
- **`ICoreCounters` is `IDisposable`**, so the view model can close the PDH query when the board closes.
- **A sample with every processor parked is no clamp** (there are no running processors to be saturated). The plan did
  not say.
- **The start is logged as a Warning and the stop as Information**; the spec said only "one line each".
- **The resources view model takes the figures as an optional constructor argument** (defaults if absent), read once at
  start: a changed settings file takes effect on the board's next start. The plan did not say when they are read.
- **The plan's risk check "compare with Get-Counter at the live check"** is moved into verification, not a test.

## For the verifier and reviewer

- The PDH structure layout is read by hand (`PdhCoreCounters.Values`): the name pointer, then a 4-byte status padded to
  8, then the 8-byte value. Research's probe got the format wrong once; the real-read test checks the total against the
  processor count but cannot check the parked count against anything else.
- The firing path is covered only by the recorded-figures tests: a clamp cannot be produced on demand (plan, P13).

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| implement | 2026-11-19 09:27:44 (logged) | this commit's time | 1542/1542 |

## Pass 2 (after verify part 1 failed, `evidence-part1-attempt1.md`; option A, Nigel)

| Finding | Done |
|---|---|
| F1 (defect) | The tooltip named the power plan's "minimum processor state", a clock-speed setting; the spec says the minimum **cores** setting, which governs parking. Now "the power plan's minimum cores setting (core parking)" |
| F2 | `Read` catches any exception: logged once, then the reader stops, and the board carries on without the warning |
| F3 | A failed array read is a failure, logged once, not a silent null on every sample |
| F4 | Tests added for a hold of zero or less (acts as one), a parked share above one (never warns), and every processor parked (no clamp) |
| Shutdown race | `Read` and `Dispose` take the same lock |

Tests: new file `tests/Theseus.Tests/Resources/ParkedCoresFixTests.cs` (5). The wording test failed before the fix; the
four rule tests cover behaviour that was already right and had no test. **One existing test changed** (rule P12):
`CoreClampTextTests.TheTooltip_SaysWhatItMeansWhatToCheckAndTheFigures` asserted the wrong setting's name, which was the
F1 defect; it now asserts the minimum cores setting. Full suite **1547 of 1547**, 0 warnings.

Recorded, not fixed: the layout and "logged once" behaviour have no unit test (the live Get-Counter comparison checks the
layout); a running core with no valid busy value counts as 0%; and **a failed read stops the reader until the board
restarts**, so a transient PDH error would leave the warning off: the safe direction (a missed warning, never a false
one).
