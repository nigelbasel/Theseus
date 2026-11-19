# verifier-card: evidence, part 1, attempt 2

Pipeline `feature` (method kit v0), stage **verify, part 1, attempt 2**. Verifier: an independent sub-agent session that
did not write the change (partial independence: same model family as the implementer, briefed by the coordinating
session). Change: `git diff origin/main...HEAD` on `nigel/verifier-card` in the feature worktree, at `b5812dd` (the fix
pass for attempt 1).

**Holdout scenarios:** none run. This feature's live check is their first run (spec, "Holdout scenarios that apply").
Verify cannot pass while they have not been run (P18); this part covers build, tests, reading, probes and mutation.

| | Clock time (measured) |
|---|---|
| Started | 19:05:33 |
| Finished | 19:21:51 |

P19 to P22 are not written in this branch's `method/README.md` (nor on `origin/main`); they are applied here as the
brief gives them: no machine paths in this file, and the live-check steps below are given to the owner as written.

## Results

| Check | Result |
|---|---|
| `dotnet test Theseus.slnx --nologo` (feature worktree, 19:05:40 to 19:08:19) | **Passed 1608 of 1608**, 0 failed, 0 skipped |
| `dotnet build Theseus.slnx --no-incremental --nologo` (19:09:16 to 19:09:26, 600 s timeout) | Build succeeded, **0 warnings, 0 errors**; no hang |
| Worktree after all runs | `git status --short` empty, apart from this file |

## Tests first, pass 2

- Pass 2 adds two test files and changes one existing one (`git diff d1951b0 b5812dd --stat -- tests/`):
  `Holdout/HoldoutGuardWorkingDirectoryTests.cs` (18 cases), `Abstractions/ProcessLauncherEnvironmentTests.cs` (1), and
  `Links/VerifierCardTests.cs`. `HoldoutGuardTests.cs` is unchanged since `d1951b0`.
- In the copy in `<scratch>`, with `tools/holdout/holdout-guard.js` put back to its version at `064b905` and every other
  file as at `b5812dd`, the two new classes ran **14 failed, 5 passed** (19:11:54 to 19:12:13). This matches
  `implementation.md`. The 14 failing: all 3 shell-inside-the-holdout verifier cases, all 3 no-role "from inside the
  holdout" cases (Read, Bash, Grep), all 3 write-outside-results routes, the Git Bash path, both remote forms, the
  `notexample.com` sibling (the old guard over-refused it) and the BOM. The 5 passing: the launcher test, the trailing
  separator, a verifier's shell in the code, a verifier's Grep from inside the holdout, and a verifier with no settings.
  Tests first holds.

## Attempt-1 findings, re-checked

| Finding | Status | Evidence |
|---|---|---|
| **M1** verifier shell commands in the holdout | **Fixed** for the routes a careless session would take | `holdout-guard.js:151-154` refuses any Bash or PowerShell call whose `cwd` is the holdout or inside it, for every role but author; path fields resolve against `cwd` (`:84-87`, `:137-138`); Grep and Glob with no path act on `cwd` (`:139-141`). Probes a20, a36, a37, n11, n16-n18, n40, n49 below. Mutations G1 to G3 caught. Still open: a command that reaches the holdout by a **relative** path from the code (n01, n02, n02b-d, n34, x19). See C11 |
| **M2** action enabled with no folder | **Fixed in code** (App code, no test: mutation 16 survives, as planned in P13) | `CanVerify` now requires `_holdoutFolder` (`ShellViewModel.cs:774-775`), read on every `Refresh` (`:630`); saving Settings calls `Refresh` (`SettingsViewModel(settings, Refresh)`, `:172`), and `App.xaml.cs:148` refreshes at start. Both menu items set `ToolTipService.ShowOnDisabled`, and the tooltip names Tools > Options. Live step L2 |
| **S1** untested guards | **Fixed**, apart from mutation 6 (left, with a reason) | `ProcessLauncherEnvironmentTests` runs the real launcher; mutation 15 (the launcher loop removed) is caught. The no-settings verifier case and the trailing separator have tests. Mutation 6 (`Persist`'s new-entry branch) still survives; the reason given (the flow never reaches it) is true and I accept it |
| **S2** Git Bash `/c/...` | **Fixed** for the usual forms | a13, n54, n56, x02, x16-x18 deny. Not caught: `>/c/...` with no space (x01), `</c/...` (n59), `/mnt/c/...` (n57), an escaped space `/c/My\ Holdout` (n05d). See C12 |
| **S3** `.git` remote | **Fixed** | a17, x10, x11, n30 deny; `holdout.github.io` and `holdout-old` allowed (n31, n32) |
| **S4** writes outside results | **Fixed** | a31, a32, a33 deny; `results\sub\` and `results\..\results\` allowed (n23, n24) |
| **S5** "Settings" in the docs | **Fixed in the docs**, but see S7 | `docs/holdouts.md:10` says Tools > Options. The notice at `ShellViewModel.cs:787` still says "in Settings", and now also says a verifier "runs there", which is no longer true |
| **S6** misplaced doc comment | **Fixed** | `SettingsViewModel.cs:168` and `:182` |
| C1 test file name | Recorded as is | Harmless |
| C2 verifier WebFetch of the remote refused | **Unchanged** (a38, n10 deny) | Implementation difference 4; the owner's call |
| C3 remote matched as a substring | **Partly fixed** | With the setting in the `host/owner/repo` form the hint asks for, SSH (`git@host:owner/repo`) and `host:owner/repo` are refused (a18, x12). With the setting entered as `https://...`, `gh repo clone host/owner/repo` (a19) and the SSH form (n29) still pass. `gh api repos/owner/repo` passes in any form (n28) |
| C4 docs promised closing | **Fixed** | `docs/holdouts.md:31` now says "it closes once it has handed over and gone quiet" (still needs pull request #1; implementation difference 7) |
| C5 brief "its repos" | **Fixed** | `VerifierBrief.cs:16` |
| C6 BOM fails open; Theseus `settings.json` unprotected | BOM **fixed** (a23); `settings.json` edit still allowed (a25) | Policy; tripwire rule |
| C7 inherited `THESEUS_ROLE` not cleared | Unchanged | As recorded |
| C8 `..` route to the holdout | **Fixed** for path fields (a21, x15) | Not in shell text: C11 |
| C9 `holdout copy` refused | Unchanged (a15) | Safe |
| C10 links made when the card is null; `Trim()` on null | Unchanged | As recorded |

### The two existing tests changed (P12)

| Test | Before | After | Judgement |
|---|---|---|---|
| `ItRunsInTheHoldoutWithTheCheckedCardsRepos` renamed `ItRunsInTheCheckedCardsFolder_WithTheHoldoutAndItsOtherReposInReach` | working directory the holdout; extra directories the checked card's two | working directory the checked card's; extra directories the holdout, then the checked card's other one (its own working directory, listed again in the fixture, dropped) | **Correction**, following the changed AC 10. The assertions are still exact (`Is.EqualTo` on both). If the owner does not accept the AC 10 change, this test goes back with it |
| The brief test | asserted "in its repos" | asserts the spec's "in the checked card's repos", and the new line | **Correction**: it had asserted the implementer's own wording instead of the spec's (the P17 case). The added line is not in AC 11's list (C14) |

## Is the AC 10 change needed to close M1?

**Yes, given the rule chosen, and the rule is the right one.** The fix closes M1 by refusing every shell command whose
working directory is inside the holdout. The tool input gives the guard only the command text and the `cwd`; it cannot
tell `cat` from `rm`. If the verifier still started in the holdout, every shell command it ran would be refused,
including the `cd` it would need to leave (that command also runs with `cwd` in the holdout), so it could build and
test nothing. The only ways to keep the approved directories were weaker: let a verifier's shell run in the holdout and
try to tell reading commands from changing ones, or allow a command that begins `cd <code> &&`. Both are guesswork on
command text, which is the kind of check M1 showed fails. Starting the verifier in the checked card's folder, with the
holdout as an extra directory, makes the rule simple and lets the verifier build. The cost: the verifier now loads the
checked repo's `CLAUDE.md` and memory, not the holdout's (the reverse of the plan's risk note), and any scenario that
needs a script run from inside the holdout cannot be run by it (C15).

The change is still **the owner's to accept**: AC 10 was approved as written. AC 6 also gained a sentence in pass 2 ("No
shell command runs with its working directory inside the holdout, for any role but author"), and that one is **not**
marked as awaiting the owner's OK (S8). The new rule also applies to a no-role session, so an owner's session working in
the holdout without `THESEUS_HOLDOUT_AUTHOR=1` can run no shell command there.

## Acceptance criteria

AC 10 is judged against its changed text.

| AC | How | Result | Evidence |
|---|---|---|---|
| 0 | Description | Pass by description | Unchanged since attempt 1; headless measurement with a control. Live steps L5 and L7 confirm it through the launcher |
| 1 | Test + reading | Pass | Unchanged. Mutation 6 survives (new-entry branch not reached by this flow) |
| 2 | Test + reading | Pass (live L5, L7) | The launcher loop is now tested with the real `ProcessLauncher` (mutation 15 caught) |
| 3 | Reading | Pass | Unchanged; no role in `Mcp/` or `Proposals/` |
| 4 | Test + probe | Pass | No owner path in the guard (P19). No settings, malformed, PascalCase, a number, blank: allow (a22a-e). A verifier with no settings may write code (a39, now tested) |
| 5 | Test + probe | **Pass, with known limits** | Every role by tool on the probed shapes, including Git Bash paths and `.git`/SSH remotes. Limits: relative-path shell routes (C11), punctuation before `/c/` (C12), an `https://` setting (C3), a verifier's WebFetch refused (C2) |
| 6 | Test + probe | Pass | Shell inside the holdout refused for no role and verifier, allowed for author (a41a). Writes strictly inside `results` (a29-a34, n23, n24, n52, n53). The added sentence is not marked as awaiting the owner (S8) |
| 7 | Reading | **Pass in code** (live L2) | Unavailable while the folder is not set, both menus; the reason is in the tooltip, shown while disabled. App code untested (mutation 16 survives, planned in P13) |
| 8 | Owner's step | Live (L1) | As planned |
| 9 | Test + reading | Pass | `VerifyMenuTests` unchanged and passing; `CanVerify` adds the folder check to both commands |
| 10 | Test + reading (changed text) | **Pass against the changed text**; awaiting the owner's OK | `VerifierCard.For`: working directory the checked card's; extra directories the holdout first, then the checked card's extra directories with its own working directory removed, duplicates removed. Report path and links unchanged. Mutations 12 to 14 caught; mutation 17 (`Distinct` removed) survives the full suite (S9) |
| 11 | Test + text | Pass, one addition | The six spec lines are word for word, and "the checked card's repos" now matches. Pass 2 adds a seventh instruction ("Read the scenarios with the file tools; run builds and tests from the checked card's folder, never from inside the holdout."), which is right for the new directories but is not in AC 11's list (C14) |
| 12 | Reading | Pass (live L4) | Unchanged |
| 13 | Test + reading | Pass (live L9) | Unchanged |

## Text check (P17), changed user-facing strings and `docs/holdouts.md`

| Text | Source checked | Result |
|---|---|---|
| Card menu tooltip, "...reports only a verdict back. Needs the holdout scenarios folder set in Tools > Options" | Window opened by Tools > Options…, field "Holdout scenarios folder" | Match |
| Workstream menu tooltip, the same ending | Same | Match |
| Notice "Set the holdout scenarios folder in Settings first: a verifier card runs there." (`ShellViewModel.cs:787`) | AC 10 changed; menu name | **Wrong twice**: the verifier no longer runs in the holdout folder, and the menu is Tools > Options. Reachable only if the folder is cleared outside Theseus after the last refresh (S7) |
| Settings hint "...a verifier card runs in the folder..." (`SettingsWindow.xaml:144`) | AC 10 changed | **Wrong**: it runs in the checked card's folder (S7) |
| Brief line 3, "in the checked card's repos" | AC 11 | Match |
| Brief new line, "Read the scenarios with the file tools; run builds and tests from the checked card's folder, never from inside the holdout." | Guard `:151-154`, `:174-175` | True to the guard. Not in AC 11 (C14) |
| Guard message "No shell command runs inside the holdout folder. Read the scenarios with the file tools, and run builds and tests from the code's own folder." | Guard `:151` | True. Shown to no-role sessions too, where "read the scenarios" does not apply to them; harmless, since they are refused the file tools as well |
| `docs/holdouts.md:10` "In **Tools > Options**" | MainWindow menu | Match |
| `docs/holdouts.md:16-19` "...cannot read, search or fetch anything in the holdout. A verifier can read the scenarios and write only its report... No session runs a shell command inside the holdout folder." | Guard and probes | True for absolute paths, Git Bash paths and `host/owner/repo` remotes. Not for relative shell routes (C11) or an `https://` setting (C3); the page already calls the guard "a tripwire against accidents, not a wall" |
| `docs/holdouts.md:24-26` "It runs in the checked card's folder, so it can build and run the change, with the holdout folder in reach for reading the scenarios... The action is unavailable until the holdout folder is set." | `VerifierCard.For`, `CanVerify` | Match |
| `docs/holdouts.md:31` "...the same way Stop does; it closes once it has handed over and gone quiet." | Pull request #1, not on this branch | True only once pull request #1 is merged (implementation difference 7) |
| Doc comments: `ShellViewModel.cs:778` "a follow-on in the holdout clone with the checked card's repos in reach"; `TheseusSettings.cs:88` "Verify with holdouts… runs a verifier there" | AC 10 changed | **Stale** (S7) |
| `plan.md` step 6 "cwd the holdout folder" and its risk "The verifier's working directory is the holdout clone" | AC 10 changed | Stale; the plan is not updated for the swap (S7) |

## Guard probes

The guard ran under node, with JSON on stdin carrying a top-level `cwd`, and `THESEUS_SETTINGS` pointing at settings
files written in the copy in `<scratch>`. The holdout folder is `C:\holdout` (or `C:\My Holdout`, or a UNC share, where
said), the remote `example.com/owner/holdout`, the code `C:\work\repo`. The full outputs are in the copy's `probe`
folder. "Careless?" says whether a cooperating but careless session would plausibly do it.

### The attempt-1 table, rerun

| # | Input | Ideal | Attempt 1 | Now |
|---|---|---|---|---|
| a01-a06 | No role: Read by forward slashes, case, the folder itself, doubled backslashes, trailing-separator and forward-slash settings | deny | deny | deny |
| a07-a11 | No role: Glob pattern, Grep glob and path, LS, MCP tool path, Write | deny | deny | deny |
| a12 | No role: `cd C:/holdout && cat scenarios/a.md` | deny | deny | deny |
| a13 | No role: `cat /c/holdout/scenarios/a.md` | deny | allow | **deny** |
| a14 | No role: siblings `holdout2`, `holdout.bak`, `holdout-notes` | allow | allow | allow |
| a15 | No role: sibling `holdout copy` | allow | deny | deny (C9, safe) |
| a16 | No role: WebFetch of the remote, mixed case | deny | deny | deny |
| a17 | No role: `git clone https://example.com/owner/holdout.git` | deny | allow | **deny** |
| a18 | No role: `git clone git@example.com:owner/holdout` | deny | allow | **deny** |
| a19 | No role: setting `https://...`, `gh repo clone example.com/owner/holdout` | deny | allow | **allow** (C3) |
| a20 | No role: Read relative `scenarios/a.md`, `cwd` the holdout | deny | allow | **deny** |
| a21 | No role: Read `C:/work/../holdout/a.md` | deny | allow | **deny** |
| a22 | No role: settings missing, malformed, PascalCase, number, blank | allow | allow | allow |
| a23 | No role: settings with a BOM | deny | allow | **deny** |
| a24 | stdin not JSON | allow | allow | allow |
| a25 | No role: Edit Theseus's own `settings.json` | (policy) | allow | allow (C6) |
| a26 | `THESEUS_ROLE=Verifier` | as no role | deny | deny |
| a27 | Verifier: LS, Read the holdout | allow | allow | allow |
| a28 | Verifier: MultiEdit a scenario | deny | deny | deny |
| a29 | Verifier: NotebookEdit, Write in `results` (upper case, trailing setting) | allow | allow | allow |
| a30 | Verifier: Write `results-old\`, `C:/other/results/` | deny | deny | deny |
| a31 | Verifier: Write `results old\` | deny | allow | **deny** |
| a32 | Verifier: Write `results\..\scenarios\a.md` | deny | allow | **deny** |
| a33 | Verifier: Write a file named `results` | deny | allow | **deny** |
| a34 | Verifier: Write relative `results/a.md`, `cwd` the code / the holdout | deny / allow | deny / - | deny / allow |
| a35 | Verifier: `ls C:/holdout` | deny | deny | deny |
| a36 | Verifier: `rm scenarios/a.md`, `cwd` the holdout | deny | allow | **deny** |
| a37 | Verifier: `git commit -am x`, `cwd` the holdout | deny | allow | **deny** |
| a38 | Verifier: WebFetch of the remote | allow (AC 5) | deny | deny (C2) |
| a39 | Verifier, no settings: Write code | allow | allow | allow |
| a40 | Verifier, remote-only settings: Write code | deny | deny | deny |
| a41 | Author: commit with `cwd` the holdout; author and verifier: Write a scenario | allow | allow | allow |
| a42 | `THESEUS_HOLDOUT_AUTHOR=true` | not an author | deny | deny |

### New routes

| # | Input | Ideal | Guard | Careless? |
|---|---|---|---|---|
| n01 | No role, `cwd` `C:\work`: `cd ../holdout && cat scenarios/a.md` | deny | **allow** | Unlikely: a no-role session has no reason to know where the holdout is |
| n02 | Verifier, `cwd` the code: `cd ../../holdout && rm scenarios/a.md` (the cd inside one command line) | deny | **allow** | Unlikely but possible: the verifier is given the holdout as an absolute extra directory and an absolute report path, so it would write the absolute path (refused, a12, x06). A relative route needs it to work out where the holdout sits relative to the code |
| n02b-d | Verifier: `cd ../../holdout`; `git -C ../../holdout commit -am x`; PowerShell `Set-Location ..\..\holdout; Remove-Item ...` | deny | **allow** | As n02. After a bare `cd`, later commands are refused if Claude Code reports the new directory as `cwd` [unverified: the guard relies on it] |
| n34, x19 | Verifier `cp report.md ../../holdout/results/`; no role `cat ./../../holdout/a.md` | deny | **allow** | As n02 |
| x06, n27 | Verifier: `cd "C:\holdout" && git status`; `cat C:/holdout/scenarios/a.md` | deny | deny | The likely form |
| x07, n26, x09, x14 | Verifier in the code: `git status`, `dotnet test`, `echo $THESEUS_ROLE`, Grep with no path | allow | allow | |
| x08 | Verifier: `mkdir -p C:/holdout/results` | deny (difference 4) | deny | Write creates the folder |
| n03 | No role: `\\?\C:\holdout\...` | deny | deny | |
| n04 | No role: `\\localhost\C$\holdout\...` | deny | **allow** | No |
| n05 | No role: 8.3 short name `C:\MYHOLD~1\...` for `C:\My Holdout` | deny | **allow** | No; only if a tool printed the short name |
| n05b-c | No role: `C:\My Holdout\...`; `cat "/c/My Holdout/..."` | deny | deny | |
| n05d | No role: `cat /c/My\ Holdout/scenarios/a.md` (escaped space) | deny | **allow** | Possible if the holdout path has a space: the backslash is read as a separator (C12) |
| n06, n07a-c | Mixed slashes; double-quoted, single-quoted, doubled backslashes | deny | deny | |
| n08a-c, n08e-f | PowerShell `Get-Content -Path`, `powershell -Command "..."`, `Set-Location ...; gc`, `cmd /c type`, `powershell.exe -NoProfile -Command "Set-Location 'C:/holdout'; git status"` | deny | deny | |
| n08d | PowerShell `gc "$env:SystemDrive\holdout\a.md"` | deny | **allow** | No (an evasion) |
| n09, x16, x17 | `git -C C:/holdout log`; `git -C /c/holdout log`; `(cd /c/holdout; ls)` | deny | deny | |
| n10 | Verifier: WebFetch of a raw scenario on the remote | allow (AC 5) | deny | C2 |
| n11, n16-n18, n49 | Shell with `cwd` `C:\holdout\results`, `C:\holdout\`, `c:\HOLDOUT\scenarios`, PowerShell `Get-ChildItem` | deny | deny, except **n16** (`cwd` written `/c/holdout`: allow) | n16: no, Claude Code gives a Windows `cwd` |
| n12, n13 | Grep `path` `../../holdout` from the code: verifier / no role | allow / deny | allow / deny | |
| n14 | No role: Glob `pattern` `../holdout/**/*.md` from `C:\work` | deny | **allow** | Unlikely, as n01 |
| n14b | No role: Glob `path` `../holdout` | deny | deny | |
| n15, n35 | No `cwd` field: relative Read; Bash `ls` | - | allow | Claude Code sends `cwd`; without it a relative path resolves against the guard process's own directory |
| n19 | No role: NotebookEdit in the holdout | deny | deny | |
| n20, n41 | `cat C:/hold""out/a.md`; `cat ~/../../holdout/a.md` | deny | **allow** | No (evasions) |
| n21, n22 | Drive-relative `C:holdout\a.md`; trailing dot `C:\holdout.\a.md` | deny | **allow** | No |
| n28 | No role: `gh api repos/owner/holdout/contents/scenarios` | deny | **allow** | Unlikely: needs the owner and repo name (C3) |
| n29 | Setting `https://...`: `git clone git@example.com:owner/holdout` | deny | **allow** | Only with the setting in the wrong form (C3) |
| n30, x10-x12 | `ssh://git@.../holdout.git`; `git ls-remote https://.../holdout.git` (and with `/`); `example.com:owner/holdout.git` | deny | deny | |
| n31-n33 | `holdout-old`, `holdout.github.io` remotes / `C:/holdout.git/` folder | allow | allow, allow, deny | n33 over-refuses; harmless |
| n36, n39, n50 | Verifier with `cwd` the holdout: relative Read, Glob, absolute Read | allow | allow | |
| n38 | No role, `cwd` the holdout: Read a code file by absolute path | allow | allow | |
| n42-n45 | UNC holdout `\\server\share\holdout`: verifier write to `C:/holdout/results`; no-role Read; verifier write to its `results`; shell with `cwd` the share | deny, deny, allow, deny | as ideal | |
| n46 | Shell with `cwd` `C:/holdout/../work` | allow | allow | |
| n47 | `Task` whose prompt names the holdout | (its sub-agent's own calls are checked) | allow | |
| n48 | WebSearch whose query is the remote | - | allow | `query` is not a field the guard reads; a search engine cannot see a private repo |
| x01 | No role: `echo x >/c/holdout/scenarios/a.md` (no space after `>`) | deny | **allow** | Possible: a write, and `>file` is a common form (C12) |
| n59, n57 | `cat </c/holdout/a.md`; `/mnt/c/holdout` | deny | **allow** | Unlikely; `/mnt/c` is WSL, not Git Bash |
| x02, x03, n58, n60, n55 | `> /c/...`, `--to-file=/c/...`, `ls\|cat /c/...`, `x=/c/...`, `$(cygpath C:/holdout)` | deny | deny | |
| x04, x05 | Shell with `cwd` the sibling `C:\holdout-notes`: naming the holdout / not | deny / allow | deny / allow | |

## Mutations (P16)

Each was made in the copy in `<scratch>` only, from a saved original, and the original put back after it. Guard
mutations ran the two guard classes (`Theseus.Tests.Holdout`, 41 cases) with `--no-build`, since the tests read the
guard from the copy at run time. C# mutations were rebuilt with `-p:NuGetAudit=false` and ran their class; a survivor
was then run against the full solution. At the end the copy's mutated files were identical to the worktree's.

| # | Guard pass 2 adds, removed | Caught by |
|---|---|---|
| G1 | The shell-in-holdout refusal | `AVerifier_RunsNoShellCommandInsideTheHoldout` (3), `NoRole_IsRefusedAnythingDoneFromInsideTheHoldout` (Bash) |
| G2 | `cwd` resolution in `resolve()` | `NoRole_IsRefusedAnythingDoneFromInsideTheHoldout` (Read) |
| G3 | The search-from-`cwd` location | `NoRole_IsRefusedAnythingDoneFromInsideTheHoldout` (Grep) |
| G4 | `windowsPaths` | `NoRole_IsRefusedAGitBashPathToTheHoldout` |
| G5 | `.git` after a name in `names()` | `NoRole_IsRefusedTheRemoteWrittenAnotherWay` (both) |
| G6 | `:` read as `/` in `names()` | `NoRole_IsRefusedTheRemoteWrittenAnotherWay` (SSH) |
| G7 | The before-boundary in `names()` | `NoRole_IsNotRefusedARemoteThatOnlyEndsTheSame` |
| G8 | `under()`'s `/` after the folder (equality kept) | `NoRole_IsNotRefusedAnythingElse` (`holdout-notes`), `AVerifier_CannotWriteOutsideResultsByAnotherRoute` (`results old`) |
| G9 | `under()`'s equality (only `folder/` counts) | the 3 verifier shell cases, the no-role Bash and Grep from inside |
| G10 | `under()` both (plain `startsWith`) | as 8 |
| G11 | The equals-`results` check on a verifier's write | `AVerifier_CannotWriteOutsideResultsByAnotherRoute` (`results`) |
| G12 | The BOM strip | `ASettingsFileSavedWithAByteOrderMark_StillProtectsTheHoldout` |

| # | Other pass 2 guards, removed | Caught by |
|---|---|---|
| 12 | `VerifierCard.For` working directory back to the holdout | `ItRunsInTheCheckedCardsFolder_WithTheHoldoutAndItsOtherReposInReach` |
| 13 | `VerifierCard.For` extra directories starting from the checked card's folder again | the same |
| 14 | `VerifierCard.For` without the `Where` that drops the checked card's own folder | the same |
| 15 | `ProcessLauncher`'s environment loop (attempt 1's mutation 2) | `AVariableInTheRequest_IsSetOnTheChild` |
| 16 | **`CanVerify`'s folder check** | **Survives the full suite (1608 pass).** App code with no tests, as `implementation.md` plans (P13); live step L2 covers it |
| 17 | **`VerifierCard.For` without `Distinct`** | **Survives the full suite (1608 pass).** The fixture has no duplicate left once the checked card's own folder is dropped. Attempt 1's mutation 9 was caught by the old fixture; the swap took that coverage away |

Attempt 1's mutation 6 (`Persist`'s new-entry branch) was not rerun; nothing on that path changed.

## Findings

### Must fix

None.

### Should fix

- **S7. Text left behind by the swap (P17).** Wrong for a user: the Settings hint "a verifier card runs in the folder"
  (`SettingsWindow.xaml:144`) and the notice "Set the holdout scenarios folder in Settings first: a verifier card runs
  there." (`ShellViewModel.cs:787`, which also says Settings for Tools > Options). Stale for a reader: the summary at
  `ShellViewModel.cs:778`, `TheseusSettings.cs:88`, and `plan.md` step 6 and its risk note.
- **S8. The AC 6 sentence added in pass 2 is not marked as awaiting the owner's OK** (`spec.md`, AC 6), unlike the AC 10
  change. It is a new rule that also binds no-role sessions (an owner's session in the holdout without
  `THESEUS_HOLDOUT_AUTHOR=1` can run no shell command there), so it needs the same mark and the same OK.
- **S9. `Distinct` in `VerifierCard.For` is untested again** (mutation 17). Put a duplicate in the fixture's extra
  directories (for example the holdout, or `other` twice) so the test asserts it is dropped.

### Consider

- C11. Shell commands that reach the holdout by a **relative** path from the code are not refused (n01, n02, n02b-d, n14,
  n34, x19). For a verifier, the likely forms use the absolute paths it is given and are refused, so this is a narrow
  tripwire gap. A cheap narrowing: in a shell command, resolve tokens containing `..` against `cwd` and check them as
  paths. Also unconfirmed: whether Claude Code's hook `cwd` follows a `cd` that persists between Bash calls; the guard
  relies on it after n02b [unverified].
- C12. `windowsPaths` only starts a Git Bash path after whitespace, a quote, `=` or `(`. Add shell punctuation (`>`, `<`,
  `|`, `;`, `&`): `echo x >/c/holdout/...` (x01) is a write that passes. Separately, `normalise` turns every backslash
  into `/`, so a Bash-escaped space (`/c/My\ Holdout`, n05d) no longer matches a holdout path with a space.
- C13. With the remote set as `https://host/owner/repo`, the SSH and `gh` forms pass (a19, n29); `owner/repo` alone
  (`gh api repos/owner/repo`, n28) passes in any form. The hint asks for `host/owner/repo`, and L3 says so.
- C14. The brief now has seven instructions, not AC 11's six. The added line is right for the new directories; add it
  to AC 11 alongside the AC 10 change, for the owner's OK.
- C15. A scenario that needs a script run from inside the holdout cannot be run by the verifier, for any reason, since
  every shell command there is refused. It can copy nothing out either (its writes go only to `results`). If a holdout
  scenario ever ships a script, this needs a decision.
- Unchanged from attempt 1, recorded as they are: C2, C6 (`settings.json` edit), C7, C9, C10.

## Live-check steps for the owner

Do these on the pull request's build, only when the owner says so. `<holdout>` is the holdout clone's full path,
`<remote>` its address in `host/owner/repo` form, `<checked>` the card being verified (close-step's card, per the
plan), and `<checked folder>` that card's working directory. Each step was checked against the code at `b5812dd`.

1. **L1 Install the guard (owner's step, AC 8).** Copy `tools/holdout/holdout-guard.js` from the pull request into the
   folder the lock guard is installed in. In the Claude Code user settings, point the existing holdout hook's
   registration (PreToolUse, every tool) at `node "<that folder>/holdout-guard.js"`, and keep the personal guard's file
   as a backup. Hooks are read when a session starts, so only sessions started after this use it. *Should produce:*
   while Theseus has no holdout folder set, every tool call in a new session works as before (AC 4). Note the new rule:
   once the folder is set, a session of yours whose working directory is inside `<holdout>` and that was not started
   with `THESEUS_HOLDOUT_AUTHOR=1` is refused **every** shell command there.
2. **L2 Folder not set (AC 7).** Before setting anything, right-click any card. *Should produce:* **Verify with
   holdouts…** is greyed out, and hovering it shows the tooltip ending "Needs the holdout scenarios folder set in Tools >
   Options". Tick exactly one card and open the **Workstream** menu: the same item is greyed out, with the same ending.
3. **L3 Set the holdout (AC 7).** Open **Tools > Options…**, then the **Tools** section. Set **Holdout scenarios
   folder** to `<holdout>` and **Holdout remote address** to `<remote>` in `host/owner/repo` form (no `https://`; see
   C13). Press **Save**. *Should produce:* the window closes; Theseus's `settings.json` (Tools > Open data folder)
   contains `"holdoutFolder"` and `"holdoutRemote"` with those values; right-clicking a card now shows **Verify with
   holdouts…** enabled. (The hint under the fields still says a verifier card "runs in the folder": S7.)
4. **L4 Verify with holdouts… (AC 9, 10, 12).** Right-click `<checked>` and choose **Verify with holdouts…**.
   *Should produce:*
   - a card titled **Verify: <checked title>** in the same column and row, in the same colour, and the notice
     "Created Verify: <checked title>. Start it when you are ready.";
   - nothing started;
   - in the Chains panel, a hands-over link from `<checked>` to the new card, and a reports-back link from the new card
     to `<checked>` whose document is `<holdout>\results\<checked key>-<yyyyMMdd-HHmm>.md`, with the creation time;
   - in its Properties, working directory `<checked folder>`, and extra directories `<holdout>` first, then
     `<checked>`'s other extra directories (its own working directory not repeated, no duplicates). This is the changed
     AC 10, awaiting your OK.

   Then tick only the verifier card. *Should produce:* **Workstream > Verify with holdouts…** and the card's own menu
   item are both greyed out. Tick only `<checked>`: the Workstream entry is enabled.
5. **L5 Start the verifier (AC 2, 11).** Press Start on the verifier card. *Should produce:* the session's first prompt
   is the brief: "You are a verifier for <checked title>.", a blank line, then seven instructions, the second "Read the
   scenarios with the file tools; run builds and tests from the checked card's folder, never from inside the holdout."
   and the fifth naming the report path from L4. In its tab, ask it to run `echo "$THESEUS_ROLE"` in Bash. *Should
   produce:* `verifier`. The command runs in `<checked folder>` and names no holdout path, so the guard allows it.
6. **L6 The guard, seen from the verifier (AC 5, 6).** Ask the verifier, one at a time:
   - Read a scenario file in `<holdout>` by its full path. *Should produce:* allowed.
   - Edit that scenario. *Should produce:* refused with "A verifier writes only its report, in the holdout's results
     folder. It changes no scenario and nothing in the code it checks."
   - Run `ls "<holdout>"` in Bash. *Should produce:* refused with "A verifier reads the holdout scenarios with the file
     tools, and does not run shell commands on them."
   - Run `git status` in Bash, in its own folder. *Should produce:* allowed, showing `<checked folder>`'s status.
   - Write a file in `<checked folder>`. *Should produce:* refused with the write message above.
   - Write the report file under `<holdout>\results\`. *Should produce:* allowed; the folder is created if missing.

   Known gap (C11): a shell command that reaches the holdout by a relative path from `<checked folder>` is not refused.
   Do not use one as a check.
7. **L7 The guard, seen from an implementing session (AC 2, 5).** In any card with no role, ask the session to:
   - run `echo "$THESEUS_ROLE"`. *Should produce:* an empty line;
   - Read a scenario by its full Windows path. *Should produce:* refused with "This touches the holdout scenarios.
     Implementing sessions must not read or change them: they are the independent check on your work. Do not try
     another route to the same files. If a scenario needs changing or explaining, tell the owner.";
   - run `cat "<holdout as a Git Bash path, /c/...>/<a scenario>"` in Bash (quoted, so a space in the path is kept).
     *Should produce:* refused with the same message (S2 fixed);
   - WebFetch `https://<remote>`, then run `git ls-remote https://<remote>.git` in Bash. *Should produce:* both refused
     with the same message (S3 fixed).
8. **L8 The report (AC 11).** Let the verifier run the three scenarios and answer its questions in its tab. *Should
   produce:* the details in the report file in `<holdout>\results\`; a report on the reports-back link whose summary
   gives, for each scenario, only its name, pass or fail and one line; and the verifier's tab saying the report waits
   for your confirmation. Any build or test it runs, it runs in `<checked folder>`.
9. **L9 Stop on confirm (AC 13).** On the board, **confirm** the report on the reports-back link. *Should produce:*
   - the verifier card starts its stop exactly as pressing Stop would, with the stop prompt sent to its session;
   - the Theseus log (Tools > Open logs folder) has the line "Stopping <verifier key>: its report was confirmed, so its
     job is done";
   - no other card stops.

   Closing on its own needs pull request #1 (implementation difference 7).
10. **L10 (optional) Satisfied by hand.** On a second verifier's reports-back link, mark it satisfied by hand instead
    of confirming. *Should produce:* nothing stops.

Not reachable live, resting on tests: the author role, the no-settings case, and a shell command run with its working
directory inside the holdout (no card is set up to start there).

## Verdict, part 1, attempt 2

**PASS for part 1** (build, the full suite 1608 of 1608, zero warnings, tests first, reading, probes and mutation). M1
and M2 are closed. Should fix: S7 (text the swap left wrong), S8 (the AC 6 addition needs the owner's mark and OK, as AC
10's does) and S9 (`Distinct` untested again). Surviving mutations: 16 (`CanVerify`'s folder check, App code, planned
for the live check) and 17 (`Distinct`); attempt 1's mutation 6 stands as recorded. The AC 10 and AC 6 changes still
need the owner's OK, and verify as a whole stays open until the holdout scenarios are run (P18).
