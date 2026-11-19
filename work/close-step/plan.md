# close-step: plan

Pipeline: `feature` (method kit v0, P1 to P18), stage **plan**, autonomy L0. Branch `nigel/close-step`, repo Theseus.
From the approved `spec.md`. **Awaiting Nigel's approval.**

## Shape

The close step's decision moves into Core as one pure function, so L1's timing and V1's rule can be tested without a
window (the App call sites were untested: resumed-stop V3). The App keeps only the acting: one timer, the typing,
and the card's properties.

## Steps

| # | Step | Files (one type per file) | Verified by |
|---|---|---|---|
| 1 | **Tests first** (new files, P9), failing to build: the decision, the re-check time, the card's stage text, the retry rule | `tests/Theseus.Tests/Stopping/CloseDecisionTests.cs`, `CloseRecheckTests.cs`, `StopStageTextTests.cs`, `ExitResultTests.cs` | They fail before step 2 |
| 2 | **The decision in Core.** `CloseVerdict` (Skip, Retire, Wait, Close) and `CloseDecision` (verdict, and the moment quiet ends for Wait). `SessionClosing.Decide(...)` returns Retire when the stop was handed to an earlier run (`HandedBefore`, V1, AC 8), Wait with a due time when every condition holds except quiet (AC 1), Close when `ShouldClose` holds, otherwise Skip. `ShouldClose` and the four conditions are unchanged (AC 4) | `src/Theseus.Core/Stopping/CloseVerdict.cs`, `CloseDecision.cs`, `SessionClosing.cs` | Step 1's decision tests; existing `SessionClosingTests` unchanged and passing |
| 3 | **The re-check time.** `CloseRecheck.Next(dues)` gives the earliest due time plus a one-second margin, or none when nothing is waiting (AC 2) | `src/Theseus.Core/Stopping/CloseRecheck.cs` | Step 1 |
| 4 | **What an /exit attempt did.** `ExitResult` (Sent, NothingTyped, MaybePartial, Elevated), mapped from `NudgeOutcome` in the App. `ExitsTyped` records the result of the last attempt per stop; a stop may be tried again only through Close now and only after NothingTyped (AC 9); never after Sent (AC 11) | `src/Theseus.Core/Stopping/ExitResult.cs`, `ExitsTyped.cs` | Step 1; existing `ExitsTypedTests` unchanged and passing |
| 5 | **The card's stage.** `StopStage` (Asked, HandedOver, HandoffWritten, ExitTyped, ExitFailed) and `StopStageText.For(...)`, which gives the tooltip at the latest stage, with local times, exactly as AC 6 words it, and the reason for a failed /exit (AC 9) | `src/Theseus.Core/Stopping/StopStage.cs`, `StopStageText.cs` | Step 1. The words are checked against the spec, not against the code (P17) |
| 6 | **The App.** `CloseFinishedSessions` acts on `Decide`: Retire and Close as today, and the earliest Wait arms one `DispatcherTimer` (`_closeRecheck`), replaced each pass and stopped when nothing waits; its tick runs the close step only (AC 1 to 3). Each pass sets each card's stop state. `NudgeOutcome` maps to `ExitResult` | `src/Theseus.App/ViewModels/ShellViewModel.cs`, `src/Theseus.App/Services/ExitResults.cs` | The live check |
| 7 | **The card.** `IsStopping`, `StopTooltip`, `CanCloseNow` and `CloseNowCommand` on the card. In `MainWindow.xaml` the Stop icon is hidden while stopping and an amber (`#D68910`) stopping icon shows in its place, not pressable, with the tooltip; Close now shows only when offered (AC 5, 6, 9). A card with no stop under way renders as today (AC 7) | `src/Theseus.App/ViewModels/WorkstreamCardViewModel.cs`, `src/Theseus.App/MainWindow.xaml` | The live check, against what is rendered (P11) |
| 8 | **The second-press notice** says what the card says when the last attempt failed (AC 10) | `ShellViewModel.cs` (`Describe`) | A test of `Describe`'s wording if it can be reached without the window; otherwise the live check |
| 9 | Build and the full suite green; `docs/` updated where it describes stopping | `docs/` page on stopping | `dotnet test Theseus.slnx` |

## What the live check covers, and what it cannot (P13)

On a released build of the branch, with a scratch card:
- **Covered:** stop a session and touch nothing; it closes within 25 s of going idle (AC 1). The card shows the
  stopping icon and its tooltip moves through asked, handed over, handoff written, typed /exit (AC 5, 6). Other
  cards are unchanged (AC 7). A second Stop selected gives the new notice (AC 10).
- **Not reachable live, resting on tests:** V1 (needs a session resumed by hand outside the board's Start: tried if
  it can be set up without changing anything else, otherwise tests only); every failed-/exit path (AC 9), since
  `FocusLost` needs focus to move inside a 200 ms window and `Refused` and elevation cannot be produced on demand.
- **Holdout:** `stop-with-handoff`, run by the verifier card when it exists. Verify stays open until then.

## Risks

- **A timer and a refresh both running the close step.** Both run on the UI thread, and `ExitsTyped` still blocks a
  second typing, so the worst case is one extra pass that finds nothing.
- **The tooltip can lag** between passes (for example, a handoff written while the session is still busy shows once
  the next pass runs). Accepted: the pass that matters, the close, is the one the timer brings.
- **Close now takes focus,** on purpose and only when pressed. Its tooltip says it will bring the terminal forward.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| plan | logged at the stage start in `C:\Users\nigel\.claude\logs\git-22.log` | this commit's time | |
