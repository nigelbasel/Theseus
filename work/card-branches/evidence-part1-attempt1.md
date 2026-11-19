# card-branches: evidence

Pipeline: `feature` (method kit v0), stage **verify, part 1 of 2** (code and tests; part 2 is a live check on a
running board by Nigel), autonomy L0. Card git-22, branch `main`, repo Theseus, commit `4054624`.
Verifier: a fresh sub-agent started by the implementer. It did not write the code, but it was briefed by the
implementing session, so independence is partial.

Method: `spec.md` read first; the code at `4054624` read directly; the full suite run; two throwaway probes run
outside the repo (`...\scratchpad\verify-card-branches\probe` against a copy of the built `Theseus.Core.dll`, and
`...\scratchpad\verify-card-branches\wpfprobe`, which parses the branch `TextBlock` exactly as it stood at
`4054624~1`). `implementation.md` and `plan.md` were read only after checks 1 to 4.

## Checks

| # | Check | Result | Evidence |
|---|---|---|---|
| 1 | Full suite, `dotnet test Theseus.slnx --nologo --logger "console;verbosity=normal"` | **PASS** | `Total tests: 1508`, `Passed: 1508`, `Test Run Successful.`, exit 0, `Total time: 1.5691 Minutes`. Run 2026-11-18 22:06:21.876 to 22:08:24.751, every line in `C:\Users\nigel\.claude\logs\git-22.log` (lines 102809 to 104339). No compiler warnings in the output. Built first time; no CS0009 or file-in-use rerun was needed. All 17 new tests appear as passed by name. |
| 2 | Each AC 1 to 13 | **AC 3 not met** (tooltip text changed), others met or left to the live check | Table below |
| 3 | Tests test the spec's points; existing tests unchanged | **PASS with gaps** | `git show 4054624 --stat`: only the three new test files under `tests/`; no existing test changed. Gaps in finding F6. |
| 4 | Edge cases (30 subfolders, drive root, whole `E:\`, unreadable HEAD, and others) | **Mixed**: no defect, three gaps | Probe output in findings F2 to F5 and F8 |
| 5 | `implementation.md` / `plan.md` claims | **Mostly match**; two claims do not hold | Section below |

## Acceptance criteria

| AC | Verdict | Evidence |
|---|---|---|
| 1 Repos from extra directories, in order; repo a directory is in; else immediate subfolders that are repos; each once | **Met, with gaps** | `CardRepos.cs:70-89, 95-112`; `GitRepository.cs:10-47`. Probed: a worktree whose `.git` has a relative `gitdir` is found and its branch read (`user/rel`); paths differing only by case or by a trailing `\` or `/` are counted once (key is trimmed and `OrdinalIgnoreCase`, `CardRepos.cs:75,81`). A feature folder that is itself inside a repo contributes the enclosing repo, not the repos beneath it: this is what AC 1 literally says ("inside a git repository contributes that repository"). Gaps: the same repo reached through a junction counts twice (F3); the displayed name keeps the case and trailing separator of the first path given (F7). Order within a feature folder is the file system's enumeration order (alphabetical on NTFS in the probe). |
| 2 Missing directory or no repo: nothing, no error | **Met** | `GitRepository.cs:21-24, 32-36`; `CardRepos.cs:104-111`. Probed: missing folder, missing drive `Q:\nothing`, relative `foo`, invalid characters, an unreachable UNC path: all gave 0 repos and no exception. Test `AMissingFolder_OrOneWithNoRepos_ContributesNothing`. |
| 3 No repos: exactly as today, worded as today | **Not met (tooltip)**; text and visibility met | Text: old `Text="{Binding Branch, StringFormat='on {0}'}"` gives `on main`; new `BranchLines.cs:139-141` gives `on main`. Visibility: old and new both bind `Branch` through `NotEmptyToVisibility` (`NotEmptyToVisibilityConverter.cs:11-12`); no branch gives empty text, so collapsed in both. Colour: `TextFillColorTertiaryBrush` in both. **Tooltip differs** (F1): at `4054624~1` the tooltip was `ToolTip="{Binding Branch, StringFormat='The branch checked out in this working directory: {0}'}"`. `ToolTip` is typed `object`, and WPF applies `StringFormat` only to string targets, so the tooltip actually shown was the bare branch. The WPF probe parsing that exact element printed `Old Text = [on nigel/ledger]`, `Old ToolTip = [nigel/ledger] (String)`. Now the tooltip is `The branch checked out in this working directory: nigel/ledger` (`BranchLines.cs:140`). The test `NoReposDeclared_ReadsTheWorkingDirectoryAsBefore` (`BranchLinesTests.cs:150`) asserts the format string, not what was shown, so it passes on a changed tooltip. |
| 4 At least one repo: their branches, not the working directory's | **Met** (by reading) | `BranchLines.cs:137-160`: the working directory is used only when `repos.Count == 0`. Not discriminated by a test (F6). |
| 5 One repo: "on <branch>", tooltip names repo and branch | **Met** | `BranchLines.cs:144-149`; test `OneRepo_SaysItsBranch_AndNamesItInTheTooltip`. An unreadable HEAD gives `on branch not readable` (F4). |
| 6 Several, same branch: "on <branch> · N repos", tooltip lists each | **Met** | `BranchLines.cs:151-158`; test `SeveralReposOnOneBranch_SayTheBranchAndHowMany`; probe with 10 repos: `on user/many · 10 repos`. |
| 7 Several, different: marked, tooltip lists each, no action | **Met in Core; colour left to the live check** | `BranchLines.cs:159`; `MainWindow.xaml` DataTrigger on `BranchesDiffer` sets `#D68910`, the amber already used for the flag text. No action code anywhere in the diff. |
| 8 Detached shown as today and counts as differing | **Met** | `BranchLines.cs:154`; text comes from the unchanged `GitBranchReader.Parse` (`detached at <7 chars>`). Probe: two repos detached at the same commit give `branches differ`, which follows AC 8 literally. A single detached repo gives `on detached at 1a2b3c4`, the same wording as today. |
| 9 Line never grows; list in tooltip | **Met** | The text is one of three short forms; the list is only in `Tooltip` (`BranchLines.cs:151`). `MaxWidth="220"` and `TextTrimming` are kept. |
| 10 Read at each refresh, from files, no git | **Met, with a gap** | Called from `ShellViewModel.SyncPullRequests` (`ShellViewModel.cs:1197`), which runs in `Refresh` (`:631`) and on `workItems.Changed` (`App.xaml.cs:121`), both on the UI thread. No `Process` in any new Core file; only `Directory.Exists`, `File.Exists`, `EnumerateDirectories` (one level) and `File.ReadAllText(HEAD)`. Gap F2: an unreachable network path blocked for 2550.0 ms in the probe, now on every refresh, and twice per refresh when the prompt names a PR. |
| 11 One origin: link as today; several: no link, tooltip names repos | **Met, with gaps** | `PullRequestResolver.cs:203-226`; tests `ReposWithOneOriginBetweenThem_LinkAsBefore`, `ReposWithSeveralOrigins_GiveNoLink_AndSayWhichReposItCouldBeIn`, `ACardThatDeclaresNoRepos_ResolvesAsBefore`. The "no repos" path (`:228-232`) is the old code unchanged (compared with `4054624~1`). A repo with no origin among others is ignored when counting origins (probe: `e` without origin plus `f` links to `f`). Origins differing only in case are counted once. Gaps: F5 (the same origin written differently counts as two), F9 (the tooltip's second line). Note F10 (one origin now wins over the working directory's). |
| 12 Launch and directories unchanged; no-repos card unchanged; rest of the card unchanged | **Not met for the tooltip (F1)**; otherwise met | The diff touches no launcher file (`git show 4054624 --stat`). A no-repos card differs only in the tooltip (F1). The PR row change (`CardItemRows.cs:38`) only falls back to `Note`, which is null on the no-repos path. |
| 13 `docs/cards.md` describes the branch line | **Met** | `docs/cards.md:49` describes the one-repo, several-agree and differ forms and the tooltip. It does not mention the PR note or "branch not readable". |

## Findings

| # | Severity | Finding |
|---|---|---|
| F1 | **defect** (low impact) | **A card with no repos has a different tooltip.** Before, it showed the bare branch (`nigel/ledger`), because `StringFormat` is ignored on the `object`-typed `ToolTip`. Now it shows `The branch checked out in this working directory: nigel/ledger`. This breaks AC 3 ("worded as today") and AC 12 ("everything about a card that declares no repos"). The new wording is arguably what was always intended, but the spec says unchanged. The no-repos test asserts the new text, so it cannot catch this. Fix one way or the other: either make the no-repos tooltip the bare branch, or Nigel accepts the new wording and the spec records it. Proof: the WPF probe's output under AC 3. |
| F2 | gap | **A slow or unreachable extra directory now blocks the UI thread on every refresh.** Before, `addDirs` were touched only when the prompt named a PR. Now every card's `addDirs` are walked on every refresh (`CardRepos.Read`). The probe took 2550.0 ms on an unreachable UNC path. `CardRepos.Find` also runs twice per card when the prompt names a PR (`PullRequestResolver.cs:203` and `ShellViewModel.cs:1197`). A missing local drive (`Q:\`) cost 0.0 ms; a disconnected mapped network drive was not tested ([not recorded]). |
| F3 | gap | **The same repo reached through a junction is counted twice.** With a real path and a junction (`mklink /J`) to it, the card showed `on main · 2 repos`, with both listed. AC 1 says "counted once, however many directories lead to it". This is unlikely in the owner's layout, so it is not a defect. |
| F4 | note | **An unreadable HEAD reads "on branch not readable"** for a single repo. Probed with HEAD locked, HEAD as a directory, and a worktree whose `gitdir` points nowhere. Among several repos it gives `branches differ`. The spec does not cover this; `implementation.md` declares it. A card with no repos still shows nothing in this case, as before. |
| F5 | gap | **One origin written two ways counts as two origins, so the link is lost.** `https://github.com/Owner/App.git` with `https://github.com/owner/app` gave no link. So did a trailing `/`, and an `https` URL with the `git@github.com:` URL for the same repo. Each time the note was `in one of …`. This errs on the safe side (no wrong link), but AC 11's "one origin between them" is not recognised. Comparing the built PR URLs, or a normalised origin, would fix it. |
| F6 | gap | **Wrong code could pass the tests in places.** (a) AC 4 is not discriminated: every repo test uses a working directory with no repo, so code that preferred the working directory would pass. (b) No test of a worktree with a relative `gitdir`; the probe shows it works. (c) The feature-folder test uses `Is.EquivalentTo`, so order is not tested. (d) The one-origin PR test also passes on the old code, which "as today" allows. (e) The no-repos tooltip test encodes the wrong baseline (F1). (f) There are no tests for the App wiring (`ShellViewModel.cs:1197-1200`, `CardItemRows.cs:38`) or the XAML, as `implementation.md` says. |
| F7 | note | **The displayed name and path follow the first spelling given.** Upper case first gave `Name=[API]`; a trailing separator first gave `Path=[…\api\]`. Cosmetic. |
| F8 | note | **Drive roots are shallow and fast.** `C:\` took 3.9 ms (0 repos). `E:\` took 1.9 ms: only top-level folders are checked, and one top-level folder that is a repository was reported. `E:` (drive-relative) took 0.6 ms and resolved through the drive's current directory. 30 subfolders (10 repos, 20 plain with nested folders) took 23.5 ms on the first read; nested folders are not entered. |
| F9 | gap | **The unlinked PR's tooltip has a misleading second line.** The note goes into the row's title (`CardItemRows.cs:38`), so the menu shows `PR 42   in one of api, ui`. The tooltip's second line, for a row with no URL, is `Set your Jira address in Tools > Options to open it from here.` (`CardItemRow.cs:36`), which is wrong for a PR. That text was there before, but this feature makes it far more common. Once a session reports a title, the note disappears, and with it the "could be in" information that AC 11 asks the tooltip to give. |
| F10 | note | **One origin now wins over the working directory's.** For a card with one repo in `addDirs`, PR links now go to that repo's origin even when the working directory has a different one. The probe gave `owner/app` where the old code gave `owner/other`. This follows AC 4's intent, but it changes "as today" for such cards, and `implementation.md` does not list it. |

## Plan and implementation claims

| Claim | Holds? |
|---|---|
| 17 new tests, failing to build before the code; 1508 of 1508; 0 warnings; no existing test file changed | 17, 1508/1508, no warnings and no existing test changed all confirmed. "Failing to build before the code" was not checked. |
| Differences: PR note as the row's title, not an XAML tooltip fallback | Holds (`CardItemRows.cs:38`; no PR XAML change). Its side effect F9 is not mentioned. |
| Differences: amber is the literal `#D68910` | Holds; it matches the flag text on the same card (`MainWindow.xaml:528`). |
| Differences: "branch not readable", counted as differing among several; a single one not flagged | Holds (probe, F4). |
| `GitBranchReader` not changed | Holds (not in the diff). |
| Plan: "for a card with no repos the text and tooltip must come out identical to today's (AC 3, tested)" | **Does not hold** for the tooltip (F1); the test checks the format string, not what was rendered. |
| Implementation: "Cards that declare none ... do no extra work beyond an empty enumeration" | Holds for cards with an empty `addDirs`. Cards whose `addDirs` are not repos now walk them every refresh (F2). The "84 of 87 on the owner's board" figure was not checked: the board files were out of bounds. |
| "Every difference from the plan" | Incomplete. Not listed: F10 (one origin now wins over the working directory's); dedupe also trims trailing separators (harmless); the plan's "the theme's caution colour" was dropped (this one is listed). |

## Verdict (part 1)

**FAIL** for part 1. The suite passes (1508/1508), and AC 1, 2, 4 to 11 and 13 are met, or left to the live check
where marked. But **F1 is a defect against AC 3 and AC 12**: a card with no repos now has a different tooltip. It is
a one-line fix, or Nigel can accept the new wording and record it in the spec. Once F1 is settled, nothing else here
blocks. F2, F3, F5, F6 and F9 are gaps to fix or accept. Part 2, the live check on a running board, should also
hover the branch line on a card with no repos to confirm F1, and check the amber.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| verify part 1 | 2026-11-18 22:06:11 | 2026-11-18 22:11:43 | Suite run 22:06:21.876 to 22:08:24.751 (logged); probe data folders stamped 22:09:03 to 22:09:06 |
