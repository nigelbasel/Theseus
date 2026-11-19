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
