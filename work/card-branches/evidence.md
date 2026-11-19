# card-branches: evidence

## Part 2: live check on a running board (2026-11-19, Nigel)

Board 2026.11.19.12835 (built from 5e0b882), started 08:48:13. Scratch feature folder in the implementer's scratch area
holding two local repos with no remotes, `scratch-api` and `scratch-ui`, both on `nigel/scratch-feature`; added by Nigel
to the extra directories of the scratch card "Stop check: hands over".

| Step | What Nigel saw | Result |
|---|---|---|
| 1-2. Feature folder added | "on nigel/scratch-feature · 2 repos"; the tooltip listed both repos and their branch | Pass (AC 1, 6, 9) |
| 3. `scratch-ui` switched to `nigel/other-branch` (08:50:47), then F5 | "branches differ" in amber; the tooltip listed each repo with its branch | Pass (AC 7, 10) |
| 4. A card with no extra directories | The branch line's tooltip shows the bare branch name, as before | Pass (AC 3, 12; the F1 defect, seen fixed live) |

Not exercised live: PR links across repos with several origins (the scratch repos have no remotes); part 1's tests cover
them. **Verdict, part 2: passes.**

---

## Part 1, attempt 2

Pipeline: `feature` (method kit v0), stage **verify, part 1 of 2, attempt 2** (re-verify of the fix round). Part 2 is
a live check on a running board by Nigel. Autonomy L0. Repo Theseus, branch `main`, commit `38ea687` (fix round on
top of `4054624`). Verifier: a fresh sub-agent started by the implementer. It did not write the code, but it was
briefed by the implementing session, so independence is partial.

Method: `evidence-part1-attempt1.md` and `git show 38ea687` read; the changed code read directly; the full suite run;
one throwaway probe outside the repo
(`<scratch>\verify-card-branches-2\probe`,
against a copy of the built `Theseus.Core.dll`, output in `...\verify-card-branches-2\probe-output.txt`). It parses
the old and new branch `TextBlock` bindings in WPF, calls the private `PullRequestResolver.OriginKey` by reflection,
and makes junctions and a symlink. The "Pass 2" section of `implementation.md` was read only after checks 1 to 3.

## Checks

| # | Check | Result | Evidence |
|---|---|---|---|
| 1 | Full suite, `dotnet test Theseus.slnx --nologo --logger "console;verbosity=normal"` | **PASS** | `Total tests: 1520`, `Passed: 1520`, `Test Run Successful.`, exit 0, `Total time: 1.6157 Minutes`. Logged 2026-11-19 06:47:02.137 (banner) to 06:49:10.354 (exit code) in the workstream log, lines 107447 to 108989. Built first time: no CS0009 or file-in-use rerun. 0 compiler warnings (`warning XX0000` pattern: 0 matches). All 12 new tests passed by name. **`ALinkToARepo_IsTheSameRepo` ran and passed (6 ms); it was not ignored.** |
| 2 | F1, F2, F3, F5, F6, F9, F10 | F1 fixed; F9 fixed; F2, F3, F5, F6 partly; F10 recorded, not changed | Findings table |
| 3 | Anything else changed | **PASS**, two small behaviour notes (N1, N2) | `git diff 4054624 38ea687 --numstat -- tests`: `1 1 BranchLinesTests.cs`, `141 0 CardBranchesFixTests.cs`, `28 0 CardItemRowNoLinkTests.cs`. No test removed; one assertion line changed (see F1). Source changes are only the six files named in the commit. |
| 4 | Pass 2 section of `implementation.md` | **Matches, with omissions** | Section below |

## Findings from attempt 1

| # | Status | Evidence |
|---|---|---|
| F1 (defect) | **Fixed** | `BranchLines.cs:17-24`: no repos gives `BranchLine($"on {branch}", branch, false)`, and `("", "", false)` with no branch. WPF probe, same branch both sides: `WPF old Text=[on nigel/ledger] ToolTip=[nigel/ledger] (String)` (the `4054624~1` bindings) and `WPF new Text=[on nigel/ledger] ToolTip=[nigel/ledger] (String)` (the current `ToolTip="{Binding BranchTooltip}" Text="{Binding Branch}"`, `MainWindow.xaml:708-711`). Word for word the same text and tooltip. Visibility: both bind `Branch` through `NotEmptyToVisibility`; with no branch the old `Branch` was `string.Empty` (`4054624~1` `ShellViewModel.cs:1197`) and the new is `string.Empty`, so collapsed in both. Colour: `TextFillColorTertiaryBrush` unless `BranchesDiffer`, which is `false` here. **The changed test** (`BranchLinesTests.cs:40`, `"The branch checked out in this working directory: main"` to `"main"`) **is a correction, not a weakening**: the old expectation was the format string WPF never displayed, as the attempt-1 probe and this probe both show. It is still exact equality, on the value WPF does display. |
| F2 | **Partly fixed** | Once per refresh: `ShellViewModel.cs:1197-1200` calls `CardRepos.Read` once and passes the paths to `Resolve`; `PullRequestResolver.cs:31` uses `repos ??=`, so `Find` runs again only when no repos are passed. The only callers of `Resolve` in `src` are that line and the tests. `\\` and `//` paths are dropped in the `Where` at `CardRepos.cs:22`, before `RootsFrom` (the first file-system call: `GitRepository.RootOf`, `Directory.Exists`). Probe: two unreachable UNC paths in 1.0 ms (attempt 1: 2550.0 ms). Not covered: a mapped network drive letter is still read on every refresh (declared in Pass 2), and see N1. |
| F3 | **Partly fixed** | `CardRepos.cs:44-58` resolves the repo root itself with `ResolveLinkTarget`. Probe: repo + junction to the repo gives 1; repo + symlink to it gives 1. **Still twice** when the link is on a folder above the repo: a feature folder plus a junction to that feature folder gave `feature\ui | feature-junction\ui`, and so did `feature\ui` plus `feature-junction\ui` given directly. AC 1: "counted once, however many directories lead to it". Unlikely in the owner's layout, so still a gap, not a defect. The test uses a symlink, not the junction attempt 1 probed; the probe shows junctions work too. |
| F5 | **Partly fixed**; no over-merge seen for real hosting services | `PullRequestResolver.cs:37-40, 66-88`. Probe keys: `.git`, trailing `/`, case, `git@host:path` vs `https`, `ssh://git@host/...`, `http` vs `https`, `https://org@dev.azure.com/...` vs without user, and `https://user:token@github.com/...` all reduce to one key. **Not merged** (safe side, the old no-link result): an explicit port (`github.com:443/owner/app` vs `github.com/owner/app`; `ssh://git@github.com:22/...` vs `git@github.com:...`; `ssh://...:2222/...` vs `git@host:...`); Azure DevOps https `dev.azure.com/org/project/_git/repo` vs ssh `ssh.dev.azure.com/v3/org/project/repo`; `org.visualstudio.com` vs `dev.azure.com`; a password containing `@` (`ss@github.com/owner/app`). On-premises TFS with a collection keeps the collection, so `tfs.example.com/bym2020/project/_git/repo` and `.../othercollection/...` stay apart. **Could two different repos merge?** Only on a server whose paths are case-sensitive or where `repo` and `repo.git` are different repositories: `git@server:Repo.git`, `git@server:repo.git` and `git@server:repo` all gave `server/repo`. GitHub, Azure DevOps and TFS are case-insensitive, so not there. Different hosts, ports and paths never merge. The doc comment says the key is "without ... port separator"; it removes the scp `:` but keeps a port, which is the safe choice, but the wording is loose. |
| F6 | **Partly fixed** | (a) AC 4 now discriminated: `AWorkingDirectoryThatIsARepo_IsNotShown_WhenTheCardDeclaresOtherRepos` (working directory on `main`, declared repo on `user/feature`, tooltip must not contain `main`). (b) `AWorktreeWithARelativeGitDirectory_IsRead`. (c) `AFeatureFolder_ListsItsReposByName` uses `Is.EqualTo`, creating `zeta` before `alpha`. (e) corrected (F1). Still untested: `ShellViewModel.cs:1197-1200` wiring, `CardItemRows.PullRequests` passing `link.Note` (only `CardItemRow` is tested directly), F5's real-world forms beyond the four cases, and the XAML. |
| F9 | **Fixed** | `CardItemRows.cs:38` no longer puts `link.Note` in the title; it passes it as `whyNoLink`. `CardItemRow.cs:124`: second line is `Url ?? _whyNoLink ?? <Jira text>`. A reported title is kept (`APullRequestThatCouldBeInSeveralRepos_SaysSo_AndKeepsItsTitle`). **Jira rows behave as before**: `CardItemRows.cs:23` and `:45` call the 3-argument form, so `_whyNoLink` is null and the tooltip expression is the old one; test `AJiraIssueWithNoAddress_StillSaysWhereToSetIt`. See N2 for the menu text. |
| F10 | **Recorded, not changed** (as the brief allowed) | Behaviour unchanged at `PullRequestResolver.cs:50-53`; now listed in Pass 2. |

## New findings

| # | Severity | Finding |
|---|---|---|
| N1 | note | **A local path written with the long-path prefix `\\?\` is now skipped.** `IsNetwork` treats any `\\` start as remote: probe `CardRepos.Find([@"\\?\" + api])` gave 0 repos for a real repo. Unlikely in `addDirs`; such a card behaves as one with no repos there. |
| N2 | note | **An untitled PR that could be in several repos no longer says so in the menu.** Before the fix `Display` was `PR 42   in one of api, ui`; now `Title` is null, so `Display` is `PR 42` and the repos are only in the tooltip (`No title yet...` then `in one of api, ui`). This is what AC 11 asks for ("its tooltip says which repositories it could be in"), so not a defect; Nigel may want to see it in the live check. |
| N3 | note | Three inline `//` comments added (`BranchLines.cs:19-20`, `ShellViewModel.cs:1196`, `PullRequestResolver.cs:35`). The file already had such comments (`PullRequestResolver.cs:42-43`), so this follows the repo's existing style. |
| N4 | note | Feature-folder order is now `OrdinalIgnoreCase` sort (`CardRepos.cs:74-75`) rather than enumeration order; on NTFS these were nearly always the same. |

No defect found.

## Pass 2 section of `implementation.md`

| Claim | Holds? |
|---|---|
| F1: bare branch, as before | Holds (WPF probe). |
| F2: found once per refresh; `\\server` skipped without being touched | Holds. Omits that `\\?\` local paths are skipped too (N1). |
| F3: counted once by real location, "so a link or junction to it does not add a second" | Holds for a link on the repo itself; not for a link on a folder above it (F3 above), which is not listed under "not fixed". |
| F5: compared without scheme, user, `.git`, trailing slash or case; `host:path` as `host/path` | Holds. The commit's "ssh or https" holds for `git@host:` and `ssh://` without a port; ports, Azure DevOps ssh vs https, and the case-sensitive-server over-merge are not mentioned. |
| F6: three tests added (AC 4, relative gitdir, ordered feature folder; now sorted) | Holds. |
| F9: says why on the second line, keeps the title; Jira rows still point to Tools > Options | Holds. N2 (menu text) is not mentioned. |
| F10: recorded | Holds. |
| 10 + 2 new tests; one existing test changed; 1520 of 1520; 0 warnings | Holds (counts, the single changed assertion, 1520/1520, 0 warnings). "They failed to build before the change" was not checked. |
| Not fixed: mapped network drive, F7, F4 | Holds, and these match attempt 1. Not listed though left open: F3 parent-folder links, F5 port and ADO-ssh forms, F6(f) App wiring tests, F8 (a note, nothing to fix). |

## Verdict (part 1)

**PASS** for part 1. The suite passes (1520/1520, 0 warnings). F1, the only defect, is fixed: a card with no repos
now shows word for word what the board rendered before (text, tooltip and visibility, confirmed in WPF), and the one
changed test corrects a wrong expectation. F9 is fixed and Jira rows are unchanged. F2, F3, F5 and F6 are mostly
fixed; what remains errs on the safe side (a second count, or no link) and is listed above for Nigel to accept or
fix. Part 2, the live check, should hover the branch line on a card with no repos and on a multi-repo card, check the
amber, and hover an unlinkable PR row (N2).

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| verify part 1, attempt 2 | 2026-11-19 06:46:48 | 2026-11-19 06:50:58 | Suite run 06:47:02.137 to 06:49:10.354 (logged); probe run finished 06:50:36. Evidence written after the finish time recorded here; writing time [not recorded]. |
