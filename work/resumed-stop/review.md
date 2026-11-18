# resumed-stop: review

Pipeline: `bug` (method kit v0), with a review stage added by Nigel's decision. Stage **review**, autonomy L0.
Repo Theseus, branch `main`, change `842ec72..d43d6ff` (source and tests only).

Reviewer: a fresh sub-agent started by the implementer. **Partial independence**: it did not write or verify the
code, but it was briefed by the implementer, on the same model. Contracts: the "Spec" section of `fix.md` (rules
1-6), `method/rubrics/reviewer.md`, and `AGENTS.md`'s conventions. Nothing was built or run.

Line numbers are at `d43d6ff`. `SVM` is `src/Theseus.App/ViewModels/ShellViewModel.cs`.

## Rubric

| Item | Result | Notes |
|---|---|---|
| R1 Board changes pass the person | not touched | No MCP or tool list change. |
| R2 A report is a claim | not touched | |
| R3 Nothing starts a session on its own | holds | The change only adds a route that does less (`SVM:948-963`); no launch path added. |
| R4 Trackers read-only; no durations from sessions | not touched | |
| R5 Only the owner acts on another's lock | not touched | |
| R6 Lock book survives restart | not touched | |
| R7 Clean up only what is provably the dead run's | not touched | |
| R8 Plain HTTP; token only to its requester | not touched | |
| R9 Nothing destructive at startup without asking | holds | A held-back stop is counted `heldBack` and nothing is written or typed (`SVM:954-957`); the close step still skips `_unconfirmed` (`SVM:1352`); `Sweep`'s new `finished` predicate can only suppress a report, never close (`StopRequestStore.cs:244-248`), and `_closed` is empty for anything held back at startup. |
| R10 Not silent about state the user can see | holds, with a doubt | A second press now says the stop is under way (`SVM:1050-1057`) instead of nothing. Doubt: for a stop whose `/exit` was attempted but not sent, the same notice promises a close that will not come in this run (finding S1). Pre-existing: with a paste queue the notice is lost (`SVM:1016-1021`, N1 of attempt 1). |
| R11 No start with nothing to read | not touched | |
| R12 Sessions learn state from a file | not touched | |
| R13 Paused/blocked are marks | not touched | |
| R14 Briefs/reports relative | not touched | |
| R15 No secrets; updates offered | not touched | |
| R16 Conventional Windows behaviour | not touched | The notice points to the strip's **Close them** button (`MainWindow.xaml:1968`), which exists only in the strip, not the menu bar; that is pre-existing and the diff adds no command. |
| R17 Live state applies as it changes | not touched | |
| R18 A card does not grow without bound | not touched | |
| R19 Lock colours exact | not touched | |

AGENTS.md: one type per file holds (`ExitsTyped` with a private nested comparer; new test files one fixture each).
Tests are named as sentences. Doc comments cite the rule and date. Public repo: no secrets, internal names or machine
paths; the tests use a real-looking session GUID and a card slug as fixtures, which is harmless but not a placeholder
(C5).

Refactor `f9b82e3`: **changes nothing**. `Contains(sessionId)` became `AlreadyTyped(delivered)`, which differs only
when `delivered` is null, and `ShouldClose` returns false for a null `delivered` before reading `alreadyAsked`;
`Typed(delivered!)` is reached only after `ShouldClose` returned true. Same set, same comparer.

## Findings

| # | Severity | Where | Finding |
|---|---|---|---|
| S1 | should fix | `SVM:1380`, `SVM:638`, `StopRequestStore.cs:244-248`, `SVM:1361`, `SVM:1735` | **"Typed" is recorded before typing, whatever the outcome, and rule 5 now treats it as finished.** `_closed.Typed(delivered!)` runs before `TabNudge.Nudge` (`:1382`); when the outcome is not `Sent` (`TabNotConfirmed`, `FocusLost`, `Refused`, elevation: "nothing was sent", `NudgeOutcome.cs`), the request is deliberately left so that "Sweep expires it if that never happens" (`SVM:1390-1391`). Before this change that expiry was reported as unfinished; now the `finished` predicate (`_closed.AlreadyTyped`) makes `Sweep`, and the newer-session and started-again retires, remove it **silently**. Rule 5 says finished "once `/exit` has been typed for it", and rule 6 says what is logged is unchanged: a stop that was never told to exit is now never recorded as unfinished. The same stop, pressed again, gets the `AlreadyDelivered` notice "it closes once it has handed over and gone quiet" (`SVM:1052`), which it will not in this run. Keeping the once-only guard is right (rule 2); the fix is to remember "sent" separately from "attempted" (or record `Typed` only on `Sent` and keep a separate attempted set for the guard), and to pass the sent set to `Sweep` and the two retires. `ExitsTyped.Typed`'s doc ("Records that /exit has been typed", `ExitsTyped.cs:19`) then becomes true. |
| S2 | should fix | `SVM:941-945` (and the close step it defers to, `SVM:1359`) | **"Delivered to the session running now" is decided by when the stop was asked, not when it was delivered.** `StartedSince(handed, running.StartedUtc)` compares `RequestedUtc`. The hook delivers a waiting request to whatever process carries the id, with no start check (`StopHookDecision.cs`), and stamps `DeliveredUtc` (`Theseus.Hook/Program.cs:57`). So a stop asked of an earlier run, left waiting, and claimed on the opening prompt of a session resumed outside the board's Start (the case the comment at `SVM:1704-1707` describes) was delivered to the session running now, yet `alreadyDelivered` is false and a second press asks again: rule 3 ("does not ask again ... telling it twice can interrupt it while it writes the handoff") is broken for that case, and the hook's `File.Move(..., overwrite: true)` replaces the delivered file. Comparing `DeliveredUtc ?? RequestedUtc` with the start would decide it as `fix.md` Pass 2 says it is decided. The close step's retire of the same request at `:1359` (it is then never closed) is pre-existing; whether to change it is the owner's call under rule 6. Distinct from G3 (time kept while waiting) and F1 (routing of a still-waiting stale request). |
| C1 | consider | `SVM:1033-1034` | **`Describe`'s new parameters default to 0, but its one caller (`SVM:1026`) passes all seven.** The defaults are speculative and let a future caller drop a count silently. Simplicity (AGENTS.md / method). |
| C2 | consider | `IStopRequestStore.cs:13`, `:44`; `StopRequestStore.cs:28`, `:224` | **Optional parameters added to an interface with one implementation and one caller each** (`SVM:1108`, `SVM:638`). Interface defaults bind to the static type, and the null default of `sessionStartedUtc` is exactly the silent fallback to pre-G3 behaviour (F2). Required parameters would make each call site state its choice; the callers already pass a value. |
| C3 | consider | `ExitsTyped.cs:13`, `:27-34` | **The custom comparer exists only for case-insensitive ids.** `DateTimeOffset` equality and hash already use the UTC instant, so `UtcTicks` adds nothing. Normalising the id once (it is a GUID; `StopRequestPath.For` already parses it) and using the tuple's default equality would drop the nested type. Keeping `OrdinalIgnoreCase` preserves the old set's behaviour, so this is taste, not a defect. |
| C4 | consider | `SVM:1043-1057` | **Notice wording.** "That session's stop was held back ... press Close them to finish it": **Close them** finishes every held-back stop, not only this one, and "That session" is ambiguous when several sessions were selected and only one is counted; the neighbouring lines say "One session ...". "One session's stop was held back when the board opened; Close them finishes it (and any others held back)" would not be misread. |
| C5 | consider | `ExitsTypedTests.cs:12`, `StopRequestStoreLoggingTests.cs:13`, fixtures' `"stop-check-stays-idle"` | Real-looking session id and card slug from the live check used as fixtures. Not a secret or internal name, so not a breach of "The repo is public"; a placeholder id would match the "examples use placeholders" convention. |
| C6 | consider | `StopRoutingTests.cs`, `For_NeverAsksASessionThatHasAlreadyBeenHandedItsStop` | Cases cover `Running`, `Idle`, `NeedsAttention` with `hookInstalled: true` only. `Launching` is also a live lifecycle, and nothing pins that `AlreadyDelivered` wins over `PasteYourself` when the hook is not installed (the order at `StopRouting.cs:41-49`). One `TestCase` each would make the ordering a tested decision. |

Not re-reported: G1-G5, F1-F3, N1-N3 (attempt 1), L1, L2. The comment at `SVM:941` still says "by this very process"
(recorded in `evidence.md`); with S2 it is now also inaccurate as to which time is compared.

Correctness questions asked in the brief:

- **Can `/exit` be typed twice for one stop?** No, within one board run, apart from the recorded G4 race: the guard is
  keyed by `(session, RequestedUtc)` and set before the keystroke (`SVM:1380`). Across restarts a leftover is held
  back (R9).
- **Can a stop never be closed?** Yes, in two pre-existing ways the change does not fix and partly hides: an
  attempted-but-unsent `/exit` (S1, now unreported), and a stale stop delivered to a resumed run (S2). Neither is the
  reported defect, which is fixed (`ALaterStopOfTheSameResumedSession_CanStillBeClosed`).

## Summary

The change fixes the reported defect correctly and simply: the record of typed exits is keyed by the stop, not the
session, and the refactor `f9b82e3` changes no behaviour. No rubric item breaks; R9 holds. It **can be accepted
with two should-fix findings** rather than as is: S1 (rule 5 counts an `/exit` that was never sent as finished, so
such a stop now disappears from the log and its notice promises a close that will not come) and S2 (rule 3's
"delivered to the session running now" compares the asked time, so a stop delivered to a resumed run is asked
again). Neither types `/exit` twice. The rest is simplification and wording (C1-C6).

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| review | 2026-11-18 20:21:25 +0000 (clock) | 2026-11-18 20:24:00 +0000 (clock, before writing this file) | Read only; no build, test or commit. |

## What was done with each finding (Nigel chose option A, 2026-11-18)

| Finding | Disposition |
|---|---|
| S1 (should fix) | **Fixed.** `ExitsTyped` keeps "tried" (the never-twice guard, unchanged) apart from "finished" (`Finished` / `IsFinished`, recorded only when the keystrokes were `Sent`). Sweep, the newer-session retire and the start-again retire now skip the unfinished report only for a stop that finished |
| S2 (should fix) | **Fixed for rule 3.** `SessionClosing.HandedBefore` decides by `DeliveredUtc ?? RequestedUtc`, and `BeginStop` uses it, so a stop the hook handed to the session running now is not asked again. **Left, recorded:** the close step still uses `StartedSince` (request time), so a stop asked of an earlier run but handed to a session resumed outside the board's Start is retired, not closed. Changing that goes beyond rule 6; Nigel's call |
| C4 | **Fixed:** the held-back notice says Close them finishes every held-back stop |
| C1, C2, C3, C5, C6 | Accepted as they are |

Tests: new file `tests/Theseus.Tests/Stopping/StopFinishedTests.cs` (6 tests), failing to build before the change. No
existing test file changed. Full suite 1491 of 1491, 0 warnings.
