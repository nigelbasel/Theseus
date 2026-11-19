# verifier-card: plan

Pipeline: `feature` (method kit v0), stage **plan**, autonomy L0. Branch `nigel/verifier-card`, repo Theseus. From the
approved `spec.md`. **Approved by Nigel, 2026-11-19, including step 0's headless measurement.**

## Steps

| # | Step | Files (one type per file) | Verified by |
|---|---|---|---|
| 0 | **Measure AC 0 first.** Start one headless `claude -p` session with `THESEUS_ROLE=probe` set on its process and a PreToolUse hook passed only through that session's own `--settings` (nothing in the owner's configuration changes). The hook writes the variable it sees to a file under the scratch folder; the prompt makes one tool call. If the file does not say `probe`, stop and return to Nigel | none committed; the result goes in `implementation.md` | The file's contents |
| 1 | **Tests first** (new files, P9), failing to build or failing: the role kept in the sidecar and carried on the card; a launch's environment from the role (none for no role); the guard by role, tool and path, driven through node like the lock guard's tests; the verifier card's plan, links, report path and brief; the stop on confirm (and none on satisfy by hand, none for other cards); both menu entries | `tests/Theseus.Tests/Domain/WorkstreamRoleTests.cs`, `Launching/LaunchEnvironmentTests.cs`, `Holdout/HoldoutGuardTests.cs`, `Links/VerifierCardTests.cs`, `Links/VerifierEndingTests.cs`, `Repository/VerifyMenuTests.cs` | They fail before the code |
| 2 | **The role.** `WorkstreamRole` (None, Verifier); `Role` on `WorkstreamSidecar` and `Workstream`, carried by `WorkstreamAssembler` (AC 1). Not in `ProposalRequest` or any MCP tool (AC 3) | `src/Theseus.Core/Domain/WorkstreamRole.cs`, `WorkstreamSidecar.cs`, `Workstream.cs`, `WorkstreamAssembler.cs` | Step 1 |
| 3 | **A launch's environment.** `LaunchSpec.Environment` (name to value), filled by `WorkstreamLaunchPlanner` from the role: `THESEUS_ROLE=verifier`, nothing otherwise. The launcher sets those variables, and only those, on the `claude` process it starts (AC 2) | `src/Theseus.Core/Launching/LaunchSpec.cs`, `WorkstreamLaunchPlanner.cs`, `src/Theseus.Core/Abstractions/ProcessStartRequest.cs` and `ProcessLauncher.cs` (or where they live), `src/Theseus.Launcher/ClaudeProcessRunner.cs` | Step 1; and the live check (the verifier's guard role works) |
| 4 | **The guard.** `tools/holdout/holdout-guard.js`: reads `HoldoutFolder` and optional `HoldoutRemote` from Theseus's `settings.json` under the user's local application data; with neither set, allows everything. Roles from `THESEUS_ROLE` and `THESEUS_HOLDOUT_AUTHOR` (AC 4 to 6). No owner path or repo name in it (P19) | `tools/holdout/holdout-guard.js` | Step 1's guard tests |
| 5 | **Settings.** `HoldoutFolder`, `HoldoutRemote` on `TheseusSettings`, editable in the Settings window, written to the same `settings.json` the guard reads (AC 7) | `src/Theseus.Core/Settings/TheseusSettings.cs`, `src/Theseus.App/SettingsWindow.xaml`, its view model | The live check |
| 6 | **The verifier card, in Core.** `VerifierCard.For(checked card, settings, now)` gives the new card's plan (title "Verify: …", cwd the holdout folder, extra directories the checked card's; swapped in fix pass 2: cwd the checked card's folder, the holdout first among the extra directories), its role, its two links and the report path `results/<key>-<yyyyMMdd-HHmm>.md`; `VerifierBrief.For(...)` gives the fixed brief, in the spec's words (AC 10, 11; P17) | `src/Theseus.Core/Links/VerifierCard.cs`, `VerifierBrief.cs` | Step 1 |
| 7 | **The App action.** `VerifyWithHoldoutsCommand` in the card's context menu and the Workstream menu; unavailable, with the reason, when the holdout folder is not set or the card is itself a verifier. It writes the plan, the sidecar's role and the links through the existing stores, and starts nothing (AC 7, 9, 12) | `src/Theseus.App/ViewModels/ShellViewModel.cs`, `MainWindow.xaml` | Step 1's menu test; the live check |
| 8 | **Ending.** `VerifierEnding.ShouldStop(link, from-card role, how satisfied)` in Core; `ChainsViewModel.Confirm` asks it and, when it says so, the shell begins that card's stop exactly as `StopCardCommand` does, and logs it (AC 13) | `src/Theseus.Core/Links/VerifierEnding.cs`, `src/Theseus.App/ViewModels/ChainsViewModel.cs`, `ShellViewModel.cs` | Step 1; the live check |
| 9 | Docs: a user page on verifying with holdouts, generic (no owner paths) | `docs/holdouts.md`, `docs/` index | Read against the code (P17) |
| 10 | Build and the full suite green | | `dotnet test Theseus.slnx` |

## Installing it (Nigel's step, not the code's)

The lock guard is installed by hand, not by the installer, and so is this. With Nigel's OK, after the code is merged
or on the pull request's build: copy `tools/holdout/holdout-guard.js` to the folder the personal guard lives in, point
the existing hook registration at it in place of the personal guard (keeping the old file as a backup), and set the
holdout folder in Theseus's Settings. Nothing in the owner's Claude configuration changes before he says so.

## What the live check covers, and what it cannot (P13)

On the pull request's build, installed by Nigel, with the new guard installed:
- **Covered:** Verify with holdouts… on close-step's card creates the linked verifier card (AC 9, 10); starting it,
  the session has `THESEUS_ROLE=verifier` and the guard lets it read the scenarios but not edit them, and lets it
  write only in `results/` (AC 2, 5, 6); an implementing session is still refused (AC 5); it runs the three scenarios,
  asks Nigel for the steps only he can do, and reports (AC 11); confirming the report stops and closes it (AC 13).
  This is also close-step's `stop-with-handoff` run, and the stage 0 changes' scenarios.
- **Not reachable live, resting on tests:** the guard's author role and the "no settings" case (AC 4, 5); AC 3.

## Risks

- **AC 0 fails**: environment does not reach hooks. Then D1 does not work as decided; stop and return to Nigel.
- **The verifier's working directory is the holdout clone**, so the session's own `CLAUDE.md` and memory lookups key on
  that folder. Its brief is in its prompt, so it does not depend on them. (Superseded in fix pass 2: it now starts in
  the checked card's folder.)
- **The guard runs on every tool call of every session.** Reading a small settings file each time adds a few
  milliseconds; measured in the tests' timings and reported if it is more.
- **A wrong guard locks sessions out of the holdout or lets them in.** Its tests cover every role by tool; the
  personal guard stays as a backup until Nigel retires it.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| plan | after the spec's approval | this commit's time | |
