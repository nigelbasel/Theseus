# parked-cores: review

Pipeline: `feature` (method kit v0), stage **review**, autonomy L0. Repo Theseus, branch `main`.
Reviewer: a fresh sub-agent started by the implementer (partial independence: it did not write or verify the change,
but was briefed by the implementer).
Change reviewed: `git diff 60e4ab8 f21d57c -- src tests`, read from the code. Contracts: `work/parked-cores/spec.md`
(AC 1 to 9), `method/rubrics/reviewer.md` (R1 to R19), `AGENTS.md` conventions, and the repo being public.
Line numbers are in the files as they stand at `f21d57c`.

Limits the owner has already accepted (`implementation.md`, pass 2 and pass 3: N4, N5, N6, layout and "logged once"
untested, a running core with no busy value counted as 0%, an error stopping the reader until restart, a read after
`Dispose` reopening the query) are not re-reported, except where the record understates what the user sees (finding 4).

## Rubric

| Item | Verdict | Notes |
|---|---|---|
| R1 Board changes pass the person | not touched | No MCP tool or pre-approved list changed |
| R2 A report is a claim | not touched | |
| R3 Nothing starts a session on its own | not touched | |
| R4 Trackers read only; no durations from sessions | not touched | |
| R5 Only the owner acts on another's lock | not touched | |
| R6 Lock book survives restart, refuses unknown locks | not touched | |
| R7 Clean up only what is proved | not touched | |
| R8 Plain HTTP; token only to its requester | not touched | |
| R9 Nothing destructive at startup | holds | `App.xaml.cs:163-166` only passes the figures; the reader opens lazily on the first timer tick, off the UI thread, and does nothing destructive |
| R10 Not silent about visible state | not touched | |
| R11 Do not start a session with nothing to read | not touched | |
| R12 Sessions learn Theseus's state from a file | holds | `App.xaml.cs` is touched only at the `ResourcesViewModel` construction; `TheseusStatusNotice` and the status-file path are unchanged |
| R13 Paused/blocked are marks; starting a marked card asks | not touched | |
| R14 Briefs and reports placed relatively | holds | `TheseusSettings.cs` gains `CoreClamp` (three numbers); no path is stored |
| R15 Nothing secret to run or update | holds | PDH is part of Windows; no token, package or download is added; the update path is untouched |
| R16 Conventional Windows behaviour | holds | The warning is a reading in the status bar (`MainWindow.xaml:1893-1901`) with a tooltip; no command is added outside the menu bar, no word button on the toolbar, selection untouched. The amber `#D68910` matches every other amber on the board |
| R17 Live state applies; setup has its own dialog | not touched | The figures are file-only by spec (AC 5). The board re-reads the file before its own writes (`BoardViewModel.cs:457`), so a hand edit is not overwritten; it takes effect only on the next start (see finding 7) |
| R18 A card does not grow without bound | not touched | |
| R19 Lock colours matched exactly | not touched | |

No rubric item breaks.

## Against the spec

- **AC 1** holds: `_cores.Read()` runs inside the existing `Task.Run` of the 5-second sample
  (`ResourcesViewModel.cs:111-119`); PDH needs no admin. One qualification is in finding 5 (shutdown).
- **AC 2** holds for unreadable counters: every failure path in `PdhCoreCounters.ReadLocked` goes through `Fail`
  (`PdhCoreCounters.cs:137-148`), which logs only while `_broken` is false, and the outer `catch` (`:91-96`) makes
  sure no exception reaches the view model. With a null reading the watch never stands, so nothing is shown.
- **AC 3 and 4** hold: `CoreClampWatch.ShowsClamp` (`CoreClampWatch.cs:20-28`) and `Observe` (`:31-39`) are as
  specified, with `Parked < Total` and `Math.Max(1, HeldSamples)` as guards. A NaN in the settings or the busy figure
  compares false, so it cannot raise a false warning. The status text is built only from the sample that just
  passed the rule (`ResourcesViewModel.cs:161-162`), so **the warning cannot show falsely** from the rule's side.
- **AC 5** holds: defaults 0.5, 90, 3, in the settings file, absent from the settings window; an older file without
  `coreClamp` reads the defaults (tested); a `"coreClamp": null` falls back to the defaults through the `??` at
  `ResourcesViewModel.cs:52`.
- **AC 6 and 7** hold: text and placement after the machine line match the spec's example; the tooltip has meaning,
  causes (heat; power plan's minimum cores), and figures.
- **AC 8** holds as written (one line at start, one at stop, with parked of total, running busy, clock), but see
  finding 4 on what those lines will record in practice.
- **AC 9** holds: the machine line and card readings are computed as before; the new work is appended to the same
  worker lambda and after `MachineText` is set.

## Findings

No must-fix findings.

| # | Severity | Where | Finding | Rests on |
|---|---|---|---|---|
| 1 | should fix | `tests/Theseus.Tests/Resources/ParkedCoresFixTests.cs:1-46`, `ParkedCoresPass3Tests.cs:1-38` | Two test classes are named after implementation passes, not after what they test. A future reader looking for the rule's tests finds them split across `CoreClampWatchTests` and two history-named files; `PdhStatus` has its tests in a file that does not carry its name. Move them into `CoreClampWatchTests`, `CoreClampTextTests` and a `PdhStatusTests`, and drop the class comments that recount "verify part 1" and "re-verify" | AGENTS.md (tests named as sentences, one behaviour each; a file named after its type); the rubric's own note that work history belongs in `work/`, not the code |
| 2 | should fix | `ParkedCoresFixTests.cs:24-31`, `:34-39`, `:42-45`, `:16-21` | Four tests cannot fail for the reason they give. `AHoldOfZeroOrLess_ActsAsOne` passes with the `Math.Max` guard removed (a run of 1 is ≥ 0); `EveryProcessorParked_IsNoClamp_ThereIsNothingRunningToBeSaturated` passes with `Parked < Total` removed (busy 0 is below 90 anyway); `AParkedShareAboveOne_NeverWarns` exercises no code that handles a share above one (a share is never above 1, guard or not); `TheTooltip_NamesThePowerPlansMinimumCoresSetting...` repeats the assertion now in `CoreClampTextTests.cs:26-27`. `implementation.md` already admits the first two; the pass 3 tests replace them. Delete these four, or keep only what adds a behaviour | AGENTS.md ("A test proves one behaviour"); the brief's "any test that cannot fail" |
| 3 | should fix | `src/Theseus.App/ViewModels/ResourcesViewModel.cs:34` with `src/Theseus.Core/Resources/ICoreCounters.cs` | `ICoreCounters` exists, but the view model news up `PdhCoreCounters` itself, so the interface has one implementation and no substitute anywhere; and the view model's own logic (`ShowClamp`, `:148-163`: log once on start and stop, clear the text when not standing) has no test. Either take the reader through the constructor (with the settings, replacing the optional argument; see finding 6) and test `ShowClamp` with a fake, or drop the interface and use the class. As it stands it is speculative | Simplicity (brief: "anything speculative"); AGENTS.md ("Logic goes here [Core], so it can be tested without a window") |
| 4 | consider | `ResourcesViewModel.cs:156-159`, `CoreClampText.cs:40-42` | A single skipped sample (a warning-level PDH status, accepted as N5, about 4% at back-to-back reads) during a standing clamp turns the amber text off for about 15 s and writes "Cores no longer held back: the counters could not be read", then a fresh "Cores held back" line. The record describes N5 as delaying a start; it does not say a standing warning blinks off, nor that one real event is logged as several. AC 8's log lines exist "so the defaults can be checked against real events", so split events weaken that purpose, and "could not be read" is wrong for a skip. Smallest change: let a null sample neither extend nor end a standing warning (or at least word the stop line as "a sample was skipped") | Spec AC 8 (purpose of the lines); a reader of the log would misread it |
| 5 | consider | `PdhCoreCounters.cs:100-106` with `:30-36`, `ResourcesViewModel.cs:86-91` | `Dispose` runs on the UI thread and waits on the same lock as `Read`. The first read holds it through `Open` (about 0.4 s, by research), so closing the board during the first sample, or during a slow `PdhCollectQueryData`, blocks the UI thread for that long. Small and only at shutdown, but it is the one place the change can hold the UI thread. Closing with `Monitor.TryEnter` and leaving the handle to process exit would remove it | Brief ("nothing can block ... the board's UI thread"); spec AC 1 |
| 6 | consider | `ResourcesViewModel.cs:47-52` | The optional `clamp = null` argument has exactly one caller (`App.xaml.cs:163-166`), which always passes it; the default exists only so the old signature still compiles. Make it required, and replace the two fully qualified `Theseus.Core.Settings.CoreClampSettings` with a `using` | Simplicity ("no flexibility that was not requested") |
| 7 | consider | `src/Theseus.Core/Settings/CoreClampSettings.cs:3-7` | The doc comment says the figures live in the settings file so they can be revisited, but not that a change takes effect only on the board's next start (`implementation.md` records this). Someone tuning them by hand will expect them to apply as other file edits are picked up. One clause would prevent it | AGENTS.md (doc comments say why); R17's spirit (no edit lost or ignored without a word) |
| 8 | consider | `PdhCoreCounters.cs:177-191` | The hand-read layout uses `IntPtr.Size + 16` as if it worked on any pointer size, but it is right only for 64-bit: on 32-bit, `PDH_FMT_COUNTERVALUE` aligns to 8, so the status sits at offset 8 and the value at 16, not 4 and 12. The board ships `win-x64` only (`scripts/Build-Installer.ps1:66`), so nothing is wrong today; the comment should say the layout is 64-bit, or the item should be a `Sequential` struct read with `Marshal.SizeOf`/`PtrToStructure` so the runtime does the arithmetic | Correctness; a future reader would misread the pointer-size term as portable |
| 9 | consider | `PdhCoreCounters.cs:70`, `:94`, `:151-152`; `ParkedCoresPass3Tests.cs:6-10` | Comments cite "pass 2, F2", "pass 2, F3" and "pass 3", review-finding numbers that mean nothing outside `work/parked-cores/`. The reasons themselves are mostly stated, so the citations can become the dated decision ("parked-cores, 2026-11-19") as elsewhere in the change. `PdhStatus.cs:3-8` is the model to follow: the why, with what was seen and when | AGENTS.md (doc comments cite the decision, dated) |
| 10 | consider | `tests/Theseus.Tests/Resources/CoreClampTextTests.cs:36-43` | `TheLog_RecordsTheFiguresWhenItStartsAndStops` proves three behaviours (start line, stop line, stop with no reading) in one test, so a failure does not say which | AGENTS.md ("A test proves one behaviour") |
| 11 | consider | `tests/Theseus.Tests/Resources/PdhCoreCountersTests.cs:27` | `Total == Environment.ProcessorCount` can differ from the counter's instance count when the process's affinity, a job object or `DOTNET_PROCESSOR_COUNT` limits the count .NET reports, so this test can fail on a machine where the reader is right. Comparing with the counter's own `_Total`-excluded instance count is not possible from outside; acceptable, but worth a message on the assert saying what a mismatch means | Tests should fail only on a real defect |

### Checked and found sound

- **The hand-written PDH interop** is justified: no package is needed (`pdh.dll` ships with Windows, research line 28),
  and `Theseus.Core` already carries native calls (`SystemProcessTable`, `ProcessSnapshot`, `WindowsProcessTree`).
  The 64-bit layout matches `PDH_FMT_COUNTERVALUE_ITEM_W`, `CounterValue` (offsets 0 and 8) is right on both sizes,
  the two-call size probe handles `PDH_MORE_DATA`, the buffer is freed in `finally`, and `_Total` rows (including
  `n,_Total` on multi-group machines) are left out.
- **`CoreClampState`** earns its place: it makes `Observe` testable for start and stop separately, and the view model
  would otherwise have to keep its own previous-standing flag.
- **Public repo:** no internal names, secrets, machine models or machine-specific paths in the source, tests or
  comments. "A 20-processor laptop" and "Nigel" follow the repo's existing practice.
- **One type per file** holds; `CounterValue` is a private nested struct. Doc comments are present on every public
  member. No `#region`, braces everywhere.
- **Status text and tooltip:** "Cores held back: 16 of 20 parked under load" and the tooltip read correctly; the
  rounding ("98% busy") is shown only while the rule holds, so it cannot show a figure that contradicts the rule.

## Summary

The change meets AC 1 to 9 and breaks no rubric item. Nothing on the UI thread can throw or block during normal
running; a counter failure is logged once and leaves the board as it was; and the warning cannot show unless the rule
held on the sample whose figures it shows. **It can be accepted as is**: there is no must-fix finding. The three
should-fix findings are about the tests and one speculative interface, not behaviour: fold the pass-named test files
into files named after their subjects (1), remove the four tests that cannot fail for their stated reason (2), and
either use `ICoreCounters` to test `ShowClamp` or drop it (3). Of the considers, finding 4 is the one with user-visible
effect: a single skipped sample makes a standing warning blink off and splits one clamp into several log events,
which the accepted limit N5 does not describe.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| review | 2026-11-19 11:33:41 +0000 (clock) | 2026-11-19 11:36:34 +0000 (clock, before writing this file) | Read only; no build or test run |

## What was done with each finding (Nigel chose option A, 2026-11-19)

| Finding | Disposition |
|---|---|
| Should-fix 1 | **Done.** The tests from the two pass-named files moved to their subjects: the guard tests into `CoreClampWatchTests`, the status tests into a new `PdhStatusTests`. Both pass-named files are removed |
| Should-fix 2 | **Done.** The four tests that could not fail for the reason they gave are dropped: the two weaker guard tests (replaced by the pass-3 ones, which fail when a guard is removed), the share-above-one test, and the tooltip test that repeated `CoreClampTextTests` |
| Should-fix 3 | **Accepted as a known limit:** `ICoreCounters` has no substitute and `ShowClamp` no test. Injecting the reader belongs with finding 4's fix |
| Consider 4 (the blink) | **Accepted as a known limit, and proposed as follow-up.** One skipped sample during a standing clamp hides the amber text for about 15 s and logs a stop and a fresh start. Fixing it means a skipped sample is "no reading" (neither clamp nor clear), which changes AC 4: a spec change, not a review fix. Added to the follow-up list with close-step |
| Considers 5 to 11 | Accepted as they are |

No behaviour changed. Full suite 1550 of 1550 (1555 less the five test cases dropped). No remaining test asserts
anything it did not before.
