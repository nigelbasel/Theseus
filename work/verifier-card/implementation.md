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

## Pass 2 (verify part 1 failed; run overnight under Nigel's overnight autonomy brief, 2026-11-19)

| Finding | Disposition |
|---|---|
| M1 (must fix) | **Fixed.** The guard reads the hook's `cwd`: path fields resolve against it, a search with no path acts on it, and **any shell command run with its working directory inside the holdout is refused** for every role but author. So the verifier card now **starts in the checked card's folder, with the holdout as an extra directory** (overnight decision 3; spec AC 10 changed and marked as awaiting Nigel; question 1). The brief adds "Read the scenarios with the file tools; run builds and tests from the checked card's folder, never from inside the holdout." |
| M2 (must fix) | **Fixed.** Verify with holdouts… is unavailable (disabled, both menus) while no holdout folder is set; the folder is read on each shell refresh, which saving Settings triggers. Both tooltips show while disabled and say it needs the folder set in Tools > Options |
| S1 | **Fixed.** `ProcessLauncherEnvironmentTests` runs the real launcher (`cmd /c exit %VAR%`); the guard's no-settings allow and trailing separator now have tests. Mutation 6 (Persist's new-entry branch) is left: that branch is not reached by this flow |
| S2 | **Fixed.** Git Bash `/c/...` paths are read as `C:/...` |
| S3 | **Fixed.** A remote matches with `.git` after it and in SSH form (`host:owner/repo`); a name only counts whole on both sides, so `notexample.com/...` does not match |
| S4 | **Fixed.** A verifier's write target is resolved (`..` removed) and must be strictly inside `results`: `results\..\scenarios`, `results old\` and a file named `results` are refused |
| S5 | **Fixed.** The docs say Tools > Options |
| S6 | **Fixed.** The doc comments are on the right properties |
| Considers | Fixed in passing: a settings file with a byte order mark is read; the brief says "the checked card's repos" as the spec does; the docs no longer promise more than this branch delivers about closing. The rest recorded as they are |

**Existing tests changed (P12):** `VerifierCardTests.ItRunsInTheHoldoutWithTheCheckedCardsRepos` became
`ItRunsInTheCheckedCardsFolder_WithTheHoldoutAndItsOtherReposInReach`, because decision 3 reversed the directories it
asserted. The brief test asserted my own wording, "its repos", where the spec says "the checked card's repos", and now
also asserts the new line.

**Tests first:** the 18 new guard cases and the launcher test were written before the fix: 14 failed on the old
guard, and the 5 that passed cover behaviour that was already right (the launcher, a trailing separator, and three
allowed cases). Full suite 1608 of 1608, no warnings.

## Not done, as planned (P13)

- The Settings fields, the action and the confirm hook are App code with no tests; the live check covers them.
- Installing the guard and setting the holdout folder are Nigel's steps.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| implement | logged at the stage start in the workstream log | this stage's commit | Step 0 at 17:29 |
