# parked-cores: plan

Pipeline: `feature` (method kit v0), stage **plan**, autonomy L0. Branch `main`, repo Theseus.
Implements `spec.md` (approved by Nigel, 2026-11-19, with its three provisional defaults). One repo.

## Design

The rule and its wording live in `Theseus.Core`, tested; reading the counters is a thin Windows class; the App binds.
One type per file.

| Type (new or changed) | File | What it does |
|---|---|---|
| `CoreSample` (new record) | `Core/Resources/CoreSample.cs` | `Total`, `Parked`, `RunningBusyPercent` (average `% Processor Time` of the processors not parked), `ClockPercent` (null if unread) |
| `ICoreCounters` (new interface) | `Core/Resources/ICoreCounters.cs` | `CoreSample? Read()`: null when nothing could be read |
| `PdhCoreCounters` (new, Windows) | `Core/Resources/PdhCoreCounters.cs` | One PDH query kept open: `\Processor Information(*)\Parking Status`, `\Processor Information(*)\% Processor Time`, `\Processor Information(_Total)\% Processor Performance`, joined by instance name, `_Total` rows left out. The first read after opening has no rates and returns null. Any failure is logged once and gives null from then on (AC 1, 2) |
| `CoreClampSettings` (new record) | `Core/Settings/CoreClampSettings.cs` | `ParkedShare` = 0.5, `RunningBusyPercent` = 90, `HeldSamples` = 3 (AC 5) |
| `TheseusSettings` (changed) | `Core/Settings/TheseusSettings.cs` | Gains `CoreClamp { get; init; } = new()`, read from and written to the settings file like its other settings; not in the settings window |
| `CoreClampWatch` (new) | `Core/Resources/CoreClampWatch.cs` | `Observe(CoreSample?)`: whether this sample shows a clamp (AC 3), the run of clamp samples, whether the warning stands (AC 4), and whether it just started or stopped |
| `CoreClampText` (new static) | `Core/Resources/CoreClampText.cs` | The status text and tooltip (AC 6, 7) and the log wording (AC 8) |
| `ResourcesViewModel` (changed) | `App/ViewModels/ResourcesViewModel.cs` | Reads `ICoreCounters` in the same `Task.Run` as the process table; feeds the watch; exposes `ClampText`, `ClampTooltip`; logs start and stop (AC 8) |
| `MainWindow.xaml` (changed) | | A second status-bar text after the machine line, amber `#D68910`, shown only when `ClampText` is not empty |

## Steps, tests first, each in a new file

1. `tests/Theseus.Tests/Resources/CoreClampWatchTests.cs`, with the investigation's recorded figures: a clamp (16 of
   20 parked, running cores saturated) held three samples starts the warning; two do not; idle (17 of 20 parked, running
   cores idle) never does; healthy heavy use (7 of 20 parked, busy) never does; one non-clamp sample stops it; a null
   sample (unreadable) is no clamp and stops it; the same shares on 8 and 64 processors. Fails before the types exist.
2. `tests/Theseus.Tests/Resources/CoreClampTextTests.cs`: the status text, the tooltip, and the log lines.
3. `tests/Theseus.Tests/Settings/CoreClampSettingsTests.cs`: the defaults; a settings file without the section reads
   the defaults; one with it reads its figures.
4. `tests/Theseus.Tests/Resources/PdhCoreCountersTests.cs`: on this machine, two reads give a sample with a total equal
   to the logical processor count, parked within it; ignored where the counters are not available.
5. Implement Core, then the App and XAML. Full suite, 0 warnings.
6. `implementation.md` lists every difference from this plan.

## Verification, and what it cannot reach (method rule P13)

- The live check can show: the board reads the counters on this machine; **no warning at idle**; **no warning under a
  heavy, healthy load** (all processors busy for a minute, from a throwaway PowerShell load like the investigation's).
- It **cannot** show the warning firing: a clamp came from heat and cannot be produced on demand on a cool machine.
  Changing the power plan's minimum-cores setting could imitate one, but that changes system settings and is not done.
  The firing path rests on step 1's tests with the recorded figures; review should look there.

## Risks

- **PDH structure layout read by hand** (the probe in research got the format wrong once). Step 4 compares the parked
  count with what the counters say through PowerShell's `Get-Counter` at the time of the live check.
- **Processor groups** (more than 64 logical processors): instance names are "group,index"; joining by name handles it.
- **A machine that never parks** reports 0 parked: no warning, which is right.
- **The App binding and the amber text have no unit tests;** the live check shows the silent case only.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| plan | 2026-11-19 09:18:33 (logged) | this commit's time | Gate: Nigel approves this plan |
