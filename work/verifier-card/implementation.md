# verifier-card: implementation

Pipeline: `feature` (method kit v0), stage **implement**, autonomy L0. Branch `nigel/verifier-card`.

## What was done

| Plan step | Done |
|---|---|
| 0 Measure AC 0 | **Passed.** Two headless `claude -p` sessions, each with a PreToolUse hook passed only through its own `--settings`, and the calling session's `CLAUDE*` variables removed. The hook recorded `THESEUS_ROLE=probe` when the variable was set on the claude process, and `<unset>` when it was not. Nothing in the owner's configuration changed |
| 1 Tests first | Six new files: `WorkstreamRolePersistenceTests`, `LaunchEnvironmentTests`, `HoldoutGuardTests`, `VerifierCardTests`, `VerifierEndingTests`, `VerifyMenuTests`. They failed to build before step 2 (d1951b0) |
| 2 Role | `WorkstreamRole`; `Role` on `WorkstreamSidecar` and `Workstream`; carried by `WorkstreamAssembler` and by both branches of `BoardViewModel.Persist` |
| 3 Environment | `LaunchSpec.Environment`, filled by `WorkstreamLaunchPlanner` through `RoleEnvironment.For`; `ProcessStartRequest.Environment`, set by `ProcessLauncher`; `ClaudeProcessRunner` passes the spec's through |
| 4 Guard | `tools/holdout/holdout-guard.js`, 23 tests |
| 5 Settings | `HoldoutFolder`, `HoldoutRemote` on `TheseusSettings`; two fields and a hint in the Settings window |
| 6 Card | `VerifierCard.For`, `VerifierBrief.For` |
| 7 Action | `VerifyWithHoldoutsCommand` on the card and its context menu; `VerifySelectedWithHoldoutsCommand` in the Workstream menu |
| 8 Ending | `VerifierEnding.StopsOnConfirm`; `ChainsViewModel.ReportConfirmed`, raised after a successful confirm; `ShellViewModel.ReportConfirmed` stops a live verifier as `StopCardCommand` does |
| 9 Docs | `docs/holdouts.md`, linked from the index |
| 10 | Full suite 1589 of 1589, no warnings |

## Every difference from the plan (P3)

1. **The verifier card takes the checked card's colour**, so the pair reads as one on the board. The plan did not say;
   a new colour from the pool would have been the composer's default.
2. **The verifier card is placed in the checked card's column and row**, as a follow-on is.
3. **The guard reads an override path from `THESEUS_SETTINGS`** (used by its tests). Unset in normal use.
4. **A verifier's shell commands naming the holdout are refused even inside `results`** (for example `mkdir`); the
   Write tool creates the folder itself. Reading tools are Read, Grep, Glob and LS.
5. **The guard refuses a verifier's Write, Edit, MultiEdit and NotebookEdit outside `results`**; MultiEdit was added
   beyond the plan's list.
6. **A test run was refused by the lock guard** because the test filter named a class ending in `PersistenceTests`,
   which matches the container-suite pattern. Theseus's suites are exempt by the owner's rule; the filter was renamed
   by subject (`WorkstreamRole`) and the owner told. No test was renamed.
7. **The live check depends on close-step's Stop and close**, which is on pull request #1 and not on this branch.
   Ending a verifier still uses the close step as it is on `main` until both are merged.

## Not done, as planned (P13)

- The Settings fields, the action and the confirm hook are App code with no tests; the live check covers them.
- Installing the guard and setting the holdout folder are Nigel's steps.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| implement | logged at the stage start in the workstream log | this stage's commit | Step 0 at 17:29 |
