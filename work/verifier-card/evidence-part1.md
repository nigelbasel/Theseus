# verifier-card: evidence, part 1

Pipeline `feature` (method kit v0), stage **verify, part 1**. Verifier: an independent sub-agent session that did not
write the change (partial independence: same model family as the implementer, briefed by the coordinating session).
Change: `git diff origin/main...HEAD` on `nigel/verifier-card` in the feature worktree.

**Holdout scenarios:** this feature's live check is their first run (spec, "Holdout scenarios that apply"), so none is
run here. Verify cannot pass while they have not been run (P18); this part covers build, tests, reading and mutation.

| | Clock time (measured) |
|---|---|
| Started | 17:40:47 |
| Finished | 18:48:33 |

The coordinator reported a 10-minute stall "during the clean build". The clean build itself ran 17:43:48 to 17:43:54 and
succeeded (below). Later steps (the guard probes) began at 18:34:50; what happened between the two is not recorded by
any tool output this session holds, so it is written here as [not recorded].

## Results

| Check | Result |
|---|---|
| `dotnet test Theseus.slnx --nologo` (feature worktree, 17:41:13 to 17:43:21) | **Passed 1589 of 1589**, 0 failed, 0 skipped |
| `dotnet build Theseus.slnx --no-incremental --nologo` (17:43:48 to 17:43:54) | Build succeeded, **0 warnings, 0 errors** |
| Worktree after all runs | `git status --short` empty: nothing changed |

## Tests first

- `d1951b0` adds exactly six files, all under `tests/`: `HoldoutGuardTests`, `LaunchEnvironmentTests`,
  `VerifierCardTests`, `VerifierEndingTests`, `VerifyMenuTests`, `WorkstreamRolePersistenceTests`.
- `git diff d1951b0 HEAD -- tests/` is empty: no test changed after it was written.
- Built from `git archive d1951b0` in `<scratch>`: the test project **fails to build**, `CS0246` for `VerifierCard` and
  `WorkstreamRole`. Tests first holds.
- One small difference that `implementation.md` does not list: plan step 1 named `Domain/WorkstreamRoleTests.cs`, but the
  file is `ViewModels/WorkstreamRolePersistenceTests.cs`. See finding C1.

## Acceptance criteria

| AC | How | Result | Evidence |
|---|---|---|---|
| 0 | Description | **Pass by description** | `implementation.md` step 0 says: two headless `claude -p` runs, with the hook passed only through `--settings` and the caller's `CLAUDE*` variables removed. The hook saw `probe` when the variable was set on the process and `<unset>` when it was not. That is a proper control and supports the claim. It was measured headless, not through Windows Terminal and the launcher; live steps L5 and L7 confirm that path |
| 1 | Test + reading | Pass | Role is on `WorkstreamSidecar` and carried by `WorkstreamAssembler` and by both branches of `BoardViewModel.Persist`. `SessionPlan` has no role field. Tests `Persist_KeepsAVerifiersRoleAcrossARefresh` and `Persist_OfAnotherChange_DoesNotDropTheRole`. The new-entry branch of `Persist` is not tested (mutation 6) |
| 2 | Test + reading | Pass (live L5, L7) | `RoleEnvironment.For`, the planner, and `ClaudeProcessRunner` (including its remint path, which uses `spec with`) are tested. `ProcessLauncher`'s loop is untested (mutation 2). `LaunchCoordinator` is the only launch path. The spec reaches the launcher as Web-default JSON, which handles `IReadOnlyDictionary`. A no-role launch sets nothing, but it also clears nothing it inherits (C7) |
| 3 | Reading | Pass | No `Role` or `WorkstreamRole` in `Mcp/` or `Proposals/`, and neither folder changed. The plan file has no role field. The only writer is `ShellViewModel.VerifyWithHoldouts` |
| 4 | Test + reading + probe | Pass | The guard is in the repo, with 23 tests. It holds no owner path (P19 holds: the only paths are `LOCALAPPDATA` and `USERPROFILE` fallbacks). The folder and remote are read from settings as `holdoutFolder` and `holdoutRemote`, which matches how `TheseusJson.Readable` names them (Web defaults: camelCase). With no settings file, a malformed file, a blank folder or a non-string folder, everything is allowed (probes) |
| 5 | Test + probe | **Partly** | Every role works by tool on the tested shapes. Gaps: the verifier's own working directory (M1); Git Bash `/c/...` paths (S2); a remote written with `.git` (S3); a verifier's `WebFetch` of the remote is refused although AC 5 allows reading (C2) |
| 6 | Test + probe | **Partly** | A Write, Edit, MultiEdit or NotebookEdit outside `results` is refused. Not refused: `results\..\scenarios\x`, a sibling folder `results old\`, and a file named `results` itself (S4) |
| 7 | Reading | **Not as written** | Settings fields and the hint are present. But both commands stay **enabled** while the folder is empty. `CanVerify` does not check the folder, and pressing the command shows a notice instead (M2) |
| 8 | Owner's step | Live (L1) | Installing the guard is the owner's step, by plan |
| 9 | Test + reading | Pass | `VerifyMenuTests` checks both menus. `CanVerify` refuses a verifier card and an archived card. The Workstream entry needs exactly one ticked card |
| 10 | Test + reading | Pass | `VerifierCardTests` covers the title, working directory, extra directories (with duplicates removed), report path and role. `ShellViewModel` writes the plan, places the card, sets the role, then `Chains.FollowedOn` adds hands-over (checked to verifier) and reports-back (verifier to checked, document = report path) |
| 11 | Test + text | Pass, one wording note | Five of the six lines match the spec word for word. The first line says "in its repos" where the spec says "in the checked card's repos" (C5) |
| 12 | Reading | Pass (live L4) | `VerifyWithHoldouts` calls no start. It uses the same flow as New and follow-on without `StartNow` |
| 13 | Test + reading | Pass (live L9) | `ChainsViewModel.Confirm` raises `ReportConfirmed` only when the confirm is not refused. `Satisfy` raises nothing. `_book.Confirm` has only this caller. `ShellViewModel.ReportConfirmed` stops only a live card that `VerifierEnding` approves, through `BeginStop(CardSteps(card))`, exactly as `StopCardCommand` does, and logs it. The App wiring has no test (planned: live check) |

### Every difference in implementation.md, judged (P3)

| # | Difference | Judgement |
|---|---|---|
| 1 | Verifier takes the checked card's colour | Acceptable; harmless |
| 2 | Placed in the checked card's column and row | Acceptable; same as a follow-on |
| 3 | `THESEUS_SETTINGS` override | Acceptable. It is read from the claude process's environment, which a session's commands cannot change |
| 4 | Verifier shell commands naming the holdout are refused even inside `results` | Acceptable and stricter. But see M1: a command that does not *name* the holdout is the real gap |
| 5 | MultiEdit added | Acceptable; it closes a hole |
| 6 | The lock guard refused a test filter | Acceptable; no test was renamed (checked: the tests are unchanged since `d1951b0`) |
| 7 | Stop-and-close depends on pull request #1 | Accepted as recorded, but `docs/holdouts.md:30` already states the closing (C4) |
| (not listed) | AC 7 "unavailable" became "enabled, with a notice" | Not recorded. See M2 |
| (not listed) | Plan's test file name and folder for the role tests | Not recorded. See C1 |

## Guard probes (inputs the tests do not cover)

I ran the guard with node, with JSON on stdin and `THESEUS_SETTINGS` pointing at settings files written in `<scratch>`.
The holdout folder was `C:\holdout` and the remote `example.com/owner/holdout`. "Ideal" is what the spec's words call
for; rows where the guard did something else are in bold.

| Input | Ideal | Guard |
|---|---|---|
| No role: Read `C:/holdout/...` (forward slashes); `c:\HOLDOUT\...`; the folder itself; doubled backslashes | deny | deny |
| No role: folder setting with a trailing `\`; setting `C:/Holdout/` | deny | deny |
| No role: Glob `pattern`, Grep `glob`, LS `path`, MCP tool `path`, Write into the holdout | deny | deny |
| No role: Bash `cd C:/holdout && cat scenarios/a.md` | deny | deny |
| **No role: Bash `cat /c/holdout/scenarios/a.md` (Git Bash form)** | deny | **allow** (S2) |
| No role: siblings `C:/holdout2/`, `C:/holdout.bak/`, `C:/holdout-notes/` | allow | allow |
| **No role: sibling `C:/holdout copy/`** | allow | **deny** (over-refuses: C9) |
| No role: WebFetch of the remote, in mixed case | deny | deny |
| **No role: `git clone https://example.com/owner/holdout.git`** | deny | **allow** (S3: `.` is treated as part of a name, so `.git` hides it) |
| **No role: `git clone git@example.com:owner/holdout`** | deny | **allow** (C3) |
| **No role: remote set as `https://...`, command `gh repo clone example.com/owner/holdout`** | deny | **allow** (C3) |
| **No role: Read relative `scenarios/a.md` with `cwd` the holdout** | deny | **allow** (the `cwd` field is ignored: M1) |
| **No role: Read `C:/work/../holdout/a.md`** | deny | **allow** (C8) |
| No role: settings missing, malformed, PascalCase key, number, blank | allow | allow |
| **No role: settings saved with a UTF-8 BOM** | deny | **allow** (fails open: C6. Theseus itself writes no BOM) |
| No role: stdin not JSON | allow | allow |
| No role: Edit of Theseus's own `settings.json` (could clear the holdout) | (policy) | allow (C6) |
| `THESEUS_ROLE=Verifier` (capital V) | treated as no role | deny (no role) |
| Verifier: LS and Read of the holdout | allow | allow |
| Verifier: MultiEdit a scenario | deny | deny |
| Verifier: NotebookEdit and Write in `results`, in forward slashes or upper case, or with a trailing-separator setting | allow | allow |
| Verifier: Write to `results-old\`, or to `C:/other/results/` | deny | deny |
| **Verifier: Write to `results old\` (a space)** | deny | **allow** (S4) |
| **Verifier: Write to `results\..\scenarios\a.md`** | deny | **allow** (S4) |
| **Verifier: Write a file named `C:\holdout\results`** | deny | **allow** (S4) |
| Verifier: Write relative `results/a.md` | (Write takes absolute paths) | deny |
| Verifier: Bash `ls C:/holdout` | deny | deny |
| **Verifier: Bash `rm scenarios/a.md` with `cwd` the holdout** | deny | **allow** (M1) |
| **Verifier: Bash `git commit -am x` with `cwd` the holdout** | deny | **allow** (M1) |
| **Verifier: WebFetch of the remote** | allow (AC 5: reading) | **deny** (C2) |
| Verifier, no settings: Write a code file | allow (AC 4) | allow |
| Verifier, remote-only settings: Write a code file | deny | deny |
| Author: Bash commit in the holdout; author plus verifier: Write a scenario | allow | allow |
| `THESEUS_HOLDOUT_AUTHOR=true` (not `1`) | not an author | deny |

## Text check (P17)

| Text | Source | Result |
|---|---|---|
| Menu `_Verify with holdouts…` (card menu and Workstream menu) | Spec AC 9 "Verify with holdouts…" | Match. Its access key V clashes with nothing in either menu |
| Card tooltip "Create a verifier card that runs the holdout scenarios this card's spec names, and reports only a verdict back" | Spec "What changes" | Matches the behaviour |
| Workstream tooltip "...for the one ticked card..." | Code: needs exactly one ticked card | Match |
| Notice "Set the holdout scenarios folder in Settings first: a verifier card runs there." | Window is titled **Options**, opened from **Tools > Options…**, section **Tools**, field "Holdout scenarios folder" | The field name matches. "Settings" follows existing app notices (Jira notice, drawer hint) but not the window's name (S5) |
| Notice "{title} cannot be verified: it is a verifier itself, or archived." | Code | Cannot be reached from the UI: `CanVerify` disables the command first |
| Notice "Created Verify: … Start it when you are ready." | AC 12 | Match |
| Settings labels and hint | AC 7 | Match. The hint's example form `host/owner/repo` is the only form the guard matches reliably (S3, C3) |
| Brief, six lines | AC 11 | Five are word for word. The first says "in its repos", the spec "in the checked card's repos" (C5) |
| `docs/holdouts.md` step 2 "In **Settings**" | UI: Tools > Options; other docs say "**Tools > Options**" | Mismatch (S5) |
| `docs/holdouts.md` "a session with no role cannot read, search or fetch anything in the holdout" | Guard | True for absolute Windows paths. Not true for Git Bash paths or `.git` URLs (S2, S3) |
| `docs/holdouts.md` "A verifier can read the scenarios and write only its report" | Guard | Not true for shell commands in its own working directory (M1) |
| `docs/holdouts.md` "it closes on its own once it has handed over" | Depends on pull request #1, not on this branch | C4 |
| `docs/holdouts.md` "Workstream ▸ Verify with holdouts…" | MainWindow.xaml | Match |
| `SettingsViewModel.cs` doc comments | Code | The Jira summary now sits above `HoldoutFolder`, which has two summaries, and `JiraBaseUrl` has none (S6) |

## Mutations (P16)

Each mutation was made in the copy in `<scratch>` only, and the copy's file was restored after each. The relevant tests
were run with `-p:NuGetAudit=false`, the copy only. Mutations that survived were then run against the full suite. At
the end, the copy's `src/` and `tools/` were identical to the worktree's.

| # | Mutation | Caught by |
|---|---|---|
| 0 | RoleEnvironment: verifier case removed | `AVerifiersLaunch_SetsTheRoleVariable` |
| 1 | Planner does not set Environment | `AVerifiersLaunch_SetsTheRoleVariable` |
| 2 | **ProcessLauncher: loop removed** | **Survives the full suite (1589 pass)** |
| 3 | ClaudeProcessRunner: pass-through removed | `TheLauncher_StartsClaudeWithTheSpecsVariables` |
| 4 | Assembler does not carry Role | both `Persist_*` role tests |
| 5 | Persist (existing entry) does not carry Role | both `Persist_*` role tests |
| 6 | **Persist (new entry) does not carry Role** | **Survives the full suite (1589 pass)**. In this flow `Place` always writes the entry first, so the branch is not reached here |
| 7 | VerifierEnding: kind condition removed | `ConfirmingAHandOverFromAVerifier_StopsNothing` |
| 8 | VerifierEnding: role condition removed | `ConfirmingAnyOtherCardsReport_StopsNothing` |
| 9 | VerifierCard: Distinct removed | `ItRunsInTheHoldoutWithTheCheckedCardsRepos` |
| 10 | VerifierCard: report path not in `results` | `ItReportsToADatedFileInTheHoldoutsResults` and the brief test |
| 11 | Guard: author bypass removed | `AnAuthor_MayChangeTheScenarios` |
| 12 | **Guard: no-settings allow removed** | **Survives (23 of 23)**. Only a no-role case is tested, and that case allows anyway. With the check removed, a verifier with no settings would be refused every write |
| 13 | Guard: verifier write rule removed | 5 tests |
| 14 | Guard: results check widened to the whole holdout | `AVerifier_MayNotChangeTheScenarios` (Edit, Write) |
| 15 | Guard: results check inverted | 7 tests |
| 16 | Guard: READING set emptied | 3 tests |
| 17 | Guard: READING without Glob and LS | the Glob case (LS has no test) |
| 18 | Guard: sibling boundary in `names()` removed | `NoRole_IsNotRefusedAnythingElse` (`holdout-notes`) |
| 19 | Guard: case folding removed | `NoRole_IsRefusedTheHoldout` (upper case) |
| 20 | **Guard: trailing-separator strip removed** | **Survives (23 of 23)** |
| 21 | Guard: backslash normalisation removed | 2 Bash cases |
| 22 | Guard: remote check removed | the WebFetch case |
| 23 | Guard: verifier shell refusal removed | `AVerifier_MayNotChangeTheScenarios` (Bash) |

The first attempts at 5 to 8 and 19 to 21 did not run: the find strings missed because of CRLF line endings and regex
escaping, and `kind == kind` failed to build with CS1718. All seven were rerun with corrected strings, and the results
above are from those reruns.

## Findings

### Must fix

- **M1. A verifier's shell commands can change the holdout, because the holdout is its working directory**
  (`tools/holdout/holdout-guard.js:97-98` and `:121`; spec "the verifier can read them but cannot change them", AC 5).
  The guard only checks whether a field's text *names* the holdout, and it ignores the hook input's `cwd`. A verifier
  card runs in the holdout clone (AC 10), so `rm scenarios/a.md`, `git commit -am x` and `sed -i` on a scenario all
  pass (probed). The spec's exemption covers shell commands "in the code repos", and the holdout is not one. This is
  the likeliest accident, not an evasion. Fix: treat a Bash or PowerShell call whose `cwd` is inside the holdout as
  naming it, and resolve relative paths against `cwd`.
- **M2. AC 7 is not met as written, and the difference is not recorded** (`ShellViewModel.cs:124-125`, `:770-771`,
  `:1365`; AC 7, plan step 7, P3). Both commands stay enabled while the folder is empty. `CanVerify` does not look at
  the folder, and pressing the command shows the notice instead. Either make the commands unavailable, with the
  reason shown, or record the difference in `implementation.md` for the owner to accept.

### Should fix

- **S1. Untested guards (P16)**: `ProcessLauncher`'s environment loop (`ProcessLauncher.cs:28-31`; no test uses the
  real launcher), the guard's no-settings allow for a verifier (`holdout-guard.js:87-89`), and the trailing-separator
  strip in `normalise` (`holdout-guard.js:42`). Add a verifier case with no settings and a trailing-separator case. The
  launcher loop rests on live step L5.
- **S2. Git Bash paths escape the guard** (`holdout-guard.js:41-43`; AC 5). `cat /c/holdout/...` is allowed for a
  no-role session. Bash on this kind of machine is Git Bash, so `/c/...` is a normal way to write a path. Map
  `/<drive>/` to `<drive>:/` when normalising.
- **S3. A remote URL ending `.git` escapes the guard** (`holdout-guard.js:54`; AC 5). `.` counts as a name character,
  so `example.com/owner/holdout.git` does not name `example.com/owner/holdout`. `.git` is the usual clone URL. Treat a
  following `.git` as a boundary.
- **S4. A verifier can write outside `results`** (`holdout-guard.js:113`; AC 6). Writes to `results\..\scenarios\x`,
  to a sibling `results old\` (any character outside `[a-z0-9_.-]` counts as a boundary), and to a file named `results`
  are all allowed. Resolve `..`, and require `/` straight after `results`.
- **S5. "Settings" where the UI says Tools > Options** (`docs/holdouts.md:10`; P17). The other docs say
  **Tools > Options**. The notice at `ShellViewModel.cs:783` uses "Settings" too, as older notices do; the doc should
  name the menu.
- **S6. A doc comment was misplaced** (`SettingsViewModel.cs:168-169`). The Jira summary now sits above
  `HoldoutFolder`, and `JiraBaseUrl` (`:183`) has none.

### Consider

- C1. Plan step 1 named `Domain/WorkstreamRoleTests.cs`, but the file is `ViewModels/WorkstreamRolePersistenceTests.cs`,
  and `implementation.md` does not say so (P3). Harmless, since the name follows the lock-guard rename note.
- C2. A verifier's WebFetch of the remote is refused (`holdout-guard.js:120-122`), although AC 5 allows reading through
  "any tool whose ... URL ... names the holdout". Implementation difference 4 narrows reading to the four file tools.
  The owner may accept this, since the verifier has the clone.
- C3. The remote matches only as a substring: an SSH form (`git@host:owner/repo`), or a setting entered with
  `https://`, misses the other forms. Could match on `owner/repo` after the host.
- C4. `docs/holdouts.md:30` says the verifier "closes on its own once it has handed over". That needs pull request #1,
  which is not on this branch (implementation difference 7).
- C5. The brief's first line says "in its repos" where the spec says "in the checked card's repos"
  (`VerifierBrief.cs:16`). "Its" could be read as the change's.
- C6. The guard fails open on a settings file saved with a BOM, and it does not protect Theseus's own `settings.json`.
  A no-role session that edits that file clears the holdout. Both are accidents only; the tripwire rule applies.
- C7. A no-role launch adds nothing, but it also does not remove a `THESEUS_ROLE` that Theseus itself inherited
  (`ProcessLauncher.cs:28`). That is only possible if Theseus is started from a verifier's shell.
- C8. A `..` route to the holdout (`C:/work/../holdout/a.md`) is not refused for a no-role session.
- C9. A sibling whose name continues with a space (`C:/holdout copy/`) is refused. That is safe, but surprising.
- C10. `VerifyWithHoldouts` makes the links even when `Board.Card(key)` is null after `Place`. The card would then have
  no role, and the guard would refuse it everything: safe, but with no notice, unlike the New flow.
  `SettingsViewModel` save calls `HoldoutFolder.Trim()`, which throws if the file holds `"holdoutFolder": null`.

## Live-check steps for the owner

Do these on the pull request's build. Do not run them until the owner says so. `<holdout>` is the holdout clone,
`<remote>` its address, and `<checked>` the card being verified (close-step's card, per the plan).

1. **L1 Install the guard (owner's step, AC 8).** Copy `tools/holdout/holdout-guard.js` from the pull request into the
   folder the lock guard is installed in (`~/.claude/bin`). In the Claude Code user settings, point the existing
   holdout hook's registration (PreToolUse, every tool) at `node "<that folder>/holdout-guard.js"`, and keep the
   personal guard's file as a backup. Hooks are read when a session starts, so only sessions started after this use
   it. *Should produce:* while Theseus has no holdout folder set, every tool call in a new session works as before
   (AC 4).
2. **L2 Set the holdout (AC 7).** Open Tools > Options…, then the **Tools** section. Set **Holdout scenarios folder** to
   `<holdout>` and **Holdout remote address** to `<remote>` in `host/owner/repo` form (no `https://`, no `.git`; see S3
   and C3). Press **Save**. *Should produce:* Theseus's `settings.json` (Tools > Open data folder) contains
   `"holdoutFolder"` and `"holdoutRemote"` with those values.
3. **L3 (optional, before L2) Folder not set.** Right-click any card and choose Verify with holdouts…. *Code produces:*
   the item is **enabled**, and pressing it shows "Set the holdout scenarios folder in Settings first: a verifier card
   runs there." and creates nothing. AC 7 says "unavailable" (M2).
4. **L4 Verify with holdouts… (AC 9, 10, 12).** Right-click `<checked>` and choose **Verify with holdouts…**.
   *Should produce:*
   - a card titled **Verify: <checked title>** in the same column and row, in the same colour, and the notice
     "Created Verify: <checked title>. Start it when you are ready.";
   - nothing started;
   - in the Chains panel, a hands-over link from `<checked>` to the new card, and a reports-back link from the new
     card to `<checked>` whose document is `<holdout>\results\<checked key>-<yyyyMMdd-HHmm>.md`, with the creation
     time;
   - in its Properties, working directory `<holdout>`, and extra directories `<checked>`'s working directory plus
     its extra directories.

   Then tick only the verifier card. *Should produce:* **Workstream > Verify with holdouts…** and the card's own menu
   item are both greyed out. Tick only `<checked>`: the Workstream entry is enabled.
5. **L5 Start the verifier (AC 2, 11).** Press Start on the verifier card. *Should produce:* the session's first prompt
   contains the six brief lines, the fifth naming the report path from L4. In its tab, ask it to run
   `echo "$THESEUS_ROLE"` in Bash. It should print `verifier`. The command names no holdout path, so the guard
   allows it.
6. **L6 The guard, seen from the verifier (AC 5, 6).** Ask the verifier, one at a time:
   - Read a scenario file by its full path. *Should produce:* allowed.
   - Edit that scenario. *Should produce:* refused with "A verifier writes only its report, in the holdout's results
     folder. It changes no scenario and nothing in the code it checks."
   - Run `ls "<holdout>"` in Bash. *Should produce:* refused with "A verifier reads the holdout scenarios with the
     file tools, and does not run shell commands on them."
   - Write a file in `<checked>`'s worktree. *Should produce:* refused with the write message above.
   - Write the report file under `<holdout>\results\`. *Should produce:* allowed.

   Known gap (M1): `git status` with no path, run in its own working directory, *will be allowed*. Do not ask it to
   change anything that way.
7. **L7 The guard, seen from an implementing session (AC 2, 5).** In any card with no role, ask the session to:
   - run `echo "$THESEUS_ROLE"`. *Should produce:* an empty line;
   - Read a scenario by its full Windows path. *Should produce:* refused with "This touches the holdout scenarios.
     Implementing sessions must not read or change them: they are the independent check on your work. Do not try
     another route to the same files. If a scenario needs changing or explaining, tell the owner.";
   - WebFetch `https://<remote>`. *Should produce:* refused with the same message.

   (Known gaps S2 and S3: the `/c/...` and `.git` forms are allowed. Do not use them as checks of the guard.)
8. **L8 The report (AC 11).** Let the verifier run the three scenarios and answer its questions in its tab. *Should
   produce:* the details are in the report file in `<holdout>\results\`, and a report on the reports-back link whose
   summary gives, for each scenario, only its name, pass or fail and one line. The verifier's tab says the report
   waits for your confirmation.
9. **L9 Stop on confirm (AC 13).** On the board, **confirm** the report on the reports-back link. *Should produce:*
   - the verifier card starts its stop exactly as pressing Stop would, with the stop prompt sent to its session;
   - the Theseus log (Tools > Open logs folder) has the line "Stopping <verifier key>: its report was confirmed, so
     its job is done";
   - no other card stops.

   Closing on its own needs pull request #1 (implementation difference 7).
10. **L10 (optional) Satisfied by hand.** On a second verifier's reports-back link, mark it satisfied by hand instead
    of confirming. *Should produce:* nothing stops.

## Verdict, part 1

**FAIL.** Build, the full suite (1589 of 1589), zero warnings, tests first and most criteria pass. M1 (a verifier's
shell commands can change the holdout it runs in) and M2 (AC 7's "unavailable" is not met, and the difference is not
recorded) block. Mutations 2, 6, 12 and 20 survive. The holdout scenarios are still to be run, in the live check.
