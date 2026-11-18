# stop-timing: review

Pipeline: `feature` (method kit v0), stage **review**, autonomy L0.
Reviewer: a fresh sub-agent started by the implementer (partial independence: it did not write or verify the
change, but was briefed by the implementer).
Change reviewed: `git diff 0ab35d4 ee9a529 -- src tests`. Contracts: `work/stop-timing/spec.md` (AC 1-9) and
`method/rubrics/reviewer.md` (R1-R19), plus `AGENTS.md`.
Line numbers are at `ee9a529`.

| | Time (UTC, from the clock) |
|---|---|
| Started | 2026-11-18 15:39:45 |
| Finished | 2026-11-18 15:42:41 |

## Rubric

| Item | Verdict | Note |
|---|---|---|
| R1 board changes pass the person | not touched | No MCP tool or pre-approved list changed |
| R2 a report is a claim | not touched | |
| R3 nothing starts a session on its own | not touched | |
| R4 trackers read-only; sessions never report durations | holds | Every figure comes from Theseus's own times: `RequestedUtc`, the hook's clock at `Claim` (`Theseus.Hook/Program.cs:57`), the handoff file's write time, and Theseus's clock at removal. Nothing is taken from a session |
| R5 only the owner acts on another's lock | not touched | |
| R6 lock book survives restart | not touched | |
| R7 clean up only what is proved | not touched | |
| R8 plain HTTP, token to its requester | not touched | |
| R9 nothing destructive at startup without asking | holds | `AbandonStops` (`ShellViewModel.cs:1496-1501`) only adds a read and a log line; it still runs only on Nigel's answer. The startup hold-back (`ShellViewModel.cs:221`) is unchanged |
| R10 ask rather than duplicate a tab | holds | `StartMany` changes add reads and log lines only (`ShellViewModel.cs:1691-1719`) |
| R11 do not start a session with nothing to read | not touched | |
| R12 sessions learn Theseus's state from a file | holds | The hook's stamp is a file write in the request folder; it needs nothing from Theseus and cannot fail the delivery (`StopRequestStore.cs:98-123`) |
| R13 paused/blocked are marks | not touched | |
| R14 brief/report paths relative | not touched | |
| R15 nothing secret; updates offered | not touched | |
| R16-R19 the Desk | not touched | Nothing in the UI (AC 9) |

No rubric item breaks.

## Spec

| AC | Verdict |
|---|---|
| 1 delivery time recorded at hand-over | holds (`StopRequestStore.cs:98-118`, `Program.cs:57`) |
| 2 a failed stamp never changes delivery | holds: the move and parse are unchanged; the stamp is in its own catch-all and returns the unstamped request (`StopRequestStore.cs:120-123`). See F6 for a leftover file |
| 3 finished line with four figures | holds (`StopTiming.cs:64-85, 111-117`, `ShellViewModel.cs:1354`) |
| 4 no delivery time: total and time to handoff, says so | holds (`StopTiming.cs:68-76`) |
| 5 one line per removal without /exit | holds for the four removals the spec lists; **one more removal exists and is not reported** (F1) |
| 6 not reported again after /exit | holds on the normal path; see F4 for the failed-retire edge |
| 7 stopping unchanged | holds: every new call is a read or a log line, `Retire`/`Cancel` are called under the same conditions, and nothing reads the delivered file's write time that the stamp now changes (checked: no other `GetLastWriteTime` on the request folders) |
| 8 nothing estimated; missing said missing | holds for the figures; the negative-figure note asserts a cause it cannot know (F5) |
| 9 log only | holds |

## Findings

| # | Severity | Where | Finding | Rests on |
|---|---|---|---|---|
| F1 | should fix | `src/Theseus.Core/Stopping/StopRequestStore.cs:89`, with `src/Theseus.App/ViewModels/ShellViewModel.cs:940` | A delivered request can still be removed with no line. `alreadyAsked` checks only the waiting folder, so a second Stop on a session whose earlier request was delivered but never closed (the stuck case this feature exists to measure, and every later stop under L3) writes a new pending request, and the next `Claim` moves it over the delivered one with `overwrite: true`. The earlier request disappears without `/exit` and without a "did not finish" line. The spec's list of removals omits this path, so the implementation follows the spec; the spec's "whenever" does not hold. Either report the overwritten request (in the hook, or by Theseus before it asks again), or record a decision that a re-ask replaces it silently | AC 5 ("whenever"), AC 8 |
| F2 | should fix | `src/Theseus.Core/Stopping/StopTiming.cs:129-140` and `src/Theseus.App/ViewModels/ShellViewModel.cs:1382-1395` | Two readers of the same handoff write time. `ShouldClose` decides on `ShellViewModel.WrittenUtc`; the finished line re-reads it through `StopTiming.HandoffWrittenUtc`, which catches a different set of exceptions (adds `ArgumentException`, has no blank-path check). The time the close was decided on and the time reported can differ, and the two will drift. One Core helper used by both (or passing the already-read time into `ReportFinished`) removes the duplicate | simplicity; AGENTS.md "logic goes in Core" |
| F3 | consider | `src/Theseus.App/ViewModels/ShellViewModel.cs:1058-1069` (`Ask`), with the `Interrupt`/`Nudge` routes at 945-955 | A re-ask rewrites the waiting request with a new `RequestedUtc`. Every figure in the line is therefore from the latest press, not the first, and the earlier press leaves nothing. That is defensible, but nothing says it, and a reader setting the holdout deadline from these lines (the spec's purpose) will take "total" as from the first Stop. Say so in `StopTiming`'s or `StopDurations.Total`'s doc comment | AC 3, AC 8; "anything a future reader would misread" |
| F4 | consider | `src/Theseus.App/ViewModels/ShellViewModel.cs:1353-1354` | `Retire`'s result is ignored on the finished path, unlike the three new unfinished paths. If the retire fails (it logs a warning and leaves the file), the request later expires or is declined at the next launch and is then reported as unfinished after `/exit` was typed. Rare and partly predates the change, but it is the one way AC 6 can fail | AC 6 |
| F5 | consider | `src/Theseus.Core/Stopping/StopTiming.cs:79-82` | "a negative figure means the clock or a file time moved" asserts a cause. A handoff written after the request but before the turn ended (a session already handing over when asked) gives a negative delivery-to-handoff with no clock movement. Say the figure is negative and leave the cause out | AC 8 (nothing estimated) |
| F6 | consider | `src/Theseus.Core/Stopping/StopRequestStore.cs:116`, via `src/Theseus.Core/Persistence/AtomicFile.cs:24-26` | When the stamp's final move fails (the read-only case the test exercises), `AtomicFile` leaves `<id>.json.tmp` in the delivered folder for good. Harmless to `Handed()` (it lists `*.json`), but litter in a folder whose files are otherwise all meaningful | AC 2 ("exactly as today") |
| F7 | consider | `src/Theseus.Core/Stopping/StopRequestStore.cs:111-116` | Exists-then-write is not atomic: if Theseus retires the request between the check and the move, the stamp writes it back. The resurrected request would then be retired again by `StartedSince` in the cases checked, so the effect is a spurious "did not finish" line at worst. The comment says it prevents this; it narrows it. Word the comment to match | comments must not mislead |
| F8 | consider | `tests/Theseus.Tests/Stopping/StopTimingTests.cs`, `StopRequestStoreTests.cs` | Untested: `ReportFinished`, `ReportUnfinished`, `HandoffWrittenUtc`, and `Sweep` producing one line per expired request (the AC 5 change to expiry). The wiring rests on the live check alone, and that check did not see the unfinished path (evidence part 2). A `Sweep` test that captures the log would cover the one Core path that is cheap to test | spec "How it will be verified"; AGENTS.md test-first |
| F9 | consider | `tests/Theseus.Tests/Stopping/StopRequestStoreTests.cs`, `ARequestDeliveredByAnOlderHook_HasNoDeliveryTime` | The name claims an older hook; the test calls the new `Claim` without a time, which only proves the parameter's default. `ARequestFileWrittenBeforeDeliveryTimesExisted_IsStillRead` already covers the older-file case. Rename it to what it proves, or drop it | AGENTS.md "a test proves one behaviour", named as what it shows |
| F10 | consider | `src/Theseus.Core/Stopping/StopTiming.cs:72-77` | The first arm requires `RequestToDelivery` although `DeliveryToHandoff` being present already implies it, and `RequestToHandoff` is always present when a handoff time is. Two arms on (`DeliveryToHandoff`, `RequestToHandoff`) say the same thing | simplicity |
| F11 | consider | `src/Theseus.Core/Stopping/StopTiming.cs:53, 64, 88, 105` | `Describe`, `FinishedText`, `UnfinishedText` and `Age` are public only so tests can reach them, widening Core's surface. Acceptable if that is the house pattern; otherwise `internal` with the test project as a friend | simplicity |
| F12 | consider | `src/Theseus.Core/Stopping/StopRemoval.cs:12` | "Nigel declined the stops" in a public type's doc. Citations by name are the house style, but this is a description of behaviour, not a citation; "the owner declined" reads correctly for anyone running Theseus | AGENTS.md "the repo is public" (minor) |

Checked and not a finding: one type per file (three new files, one type each); test names follow
`Subject_Behaviour_Qualification`; the stamp's catch-all is deliberate and its comment says why (implementation F5);
no secrets, internal names or machine paths in code, comments or tests (the test JSON uses existing placeholder
values). L1-L3 are not made worse by this change.

## Summary

The change does what the spec asks for the paths the spec lists, and stopping itself is unchanged (AC 7): every
addition is a read, a stamp that cannot fail delivery, or a log line. No rubric item breaks. It can be accepted, with
two should-fix items best settled before the data is relied on: F1, a removal path (a second stop's claim
overwriting an earlier delivered request) that leaves no line and that the spec missed, so the owner should decide
whether to report it or record that it is silent; and F2, the duplicated handoff-time reader, which lets the close
decision and the reported figure disagree. The rest are wording, tests and small simplifications.

## What was done with each finding (Nigel chose option A, 2026-11-18)

| Finding | Disposition |
|---|---|
| F1 (should fix) | **Moved to a new bug change for L3** (sessions resumed under the same id are never closed by a second stop). F1, F3 and F4 are all about what happens when a session that was already asked to stop is asked again; logging that path now would be written around behaviour the L3 fix is about to change |
| F2 (should fix) | **Fixed.** One helper, `StopTiming.HandoffWrittenUtc(path)`, replaces `ShellViewModel.WrittenUtc`, and the close step passes the handoff time it decided on into `ReportFinished`, so the two cannot disagree. Three tests added (no path, no file, a file with a set write time); they failed to build before the change |
| F3, F4 | Moved to the L3 bug change, with F1 |
| F5, F6, F7 | Accepted as known limits; F6 and F7 are already recorded in `implementation.md` |
| F8 | Accepted: the report methods are thin wrappers over tested text functions; the per-request expiry line is checked by reading |
| F9, F10, F11 | Accepted as they are |
| F12 | **Fixed:** "The board's owner declined", not a person's name, in `StopRemoval` |

Full suite after the fixes: 1470 of 1470, 0 warnings; test changes are additions only.
