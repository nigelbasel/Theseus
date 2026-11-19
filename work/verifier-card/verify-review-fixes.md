# verifier-card: verify of the review fixes

Pipeline `feature` (method kit v0), stage **re-verify of review fixes**. Verifier: an independent sub-agent that did
not write the change or the review (partial independence: same model family, briefed by the coordinating session).
Change: `git show dae8d1a ec91d31` on `nigel/verifier-card` in the feature worktree (the attempt-2 should-fix items,
then the review's fixes). Read against `review.md` (findings and dispositions), `evidence-part1-attempt2.md`
(should-fix list) and `spec.md`.

**Holdout scenarios:** none run (P18); this covers build, tests, reading, probes and mutation only.

| | Clock time (measured) |
|---|---|
| Started | 19:45:08 |
| Finished | 20:05:55 |

## Results

### 1. Full suite, from the feature worktree

`dotnet test Theseus.slnx --nologo`, no filter: **1617 of 1617 passed**, 0 failed, 0 skipped (2 m 6 s). That run was
incremental, so it shows no compiler output; a clean build of the copy (below, restored with `-p:NuGetAudit=false`
because the audit could not reach the package feed) gave **0 warnings, 0 errors**. The worktree is clean after the run.

### 2. Review M1: can a verifier now close?

Traced in code, from a confirmed report to the close step:

1. `VerifyWithHoldouts` (`ShellViewModel.cs:779-822`) builds `VerifierCard.For(...)` and, after `Board.Place`, sets the
   created card's `Role = Verifier` and `StatusDocumentPath = verifier.HandoffPath`, then `Board.Persist`.
   `Persist` (`BoardViewModel.cs:920-955`) writes `StatusDocumentPath` into the sidecar for both a new and an existing
   entry, and `WorkstreamAssembler.cs:58` reads it back, so it survives a refresh and a restart of Theseus.
2. `VerifierCard.For` (`VerifierCard.cs:44-57`): `HandoffPath = <holdout>\results\<checked key>-<yyyyMMdd-HHmm>-handoff.md`,
   beside the report, strictly inside `results`.
3. Confirming the report: `ReportConfirmed` (`ShellViewModel.cs:828-840`) calls `BeginStop(CardSteps(card))`;
   `StepsFor` (`:1229-1249`) asks `StopSelection.ForWorkstream`, which names `HandoffPath.For(workstream)`
   (`StopSelection.cs:57`). `HandoffPath.For` returns `StatusDocumentPath` first when set (`HandoffPath.cs:26-29`), so
   the default stop prompt (`StopRequest.cs:18-21`) says "Write a handoff to `<holdout>\results\...-handoff.md`".
4. The guard lets a verifier Write there: probe `s3-abs-vwrite-results` (allow), and the existing results-write tests.
5. Close: `CloseFinishedSessions` (`ShellViewModel.cs:1430-1460`) reads the write time of `delivered.HandoffPath` (the
   same path) and `SessionClosing.ShouldClose` closes once it is newer than the request and the session has been idle
   20 s.

**So yes: the verifier can write the handoff it is asked for with the file tools, and the session is closed.** Two
conditions remain outside the fix: an owner-set custom stop prompt that asks for any other write is refused for a
verifier (the review's suggested docs line about owner-level write instructions was not added), and the card is
stopped only if it is live and its session named (`StopSelection`'s existing rules).

**Does a holdout path in `StatusDocumentPath` leak to the implementing card?**

- **`related` tool** (`McpHandler.cs:439-497`): for the checked card's session, the verifier is `other`, and
  `other.handoff` is now `<holdout>\results\...-handoff.md`. That is a **path only**, in the same folder as the link's
  `document` (the report path), which `related` already returned before this change. No tool returns the file's
  contents, and a no-role Read of it is refused by the guard (it names the holdout). No new information.
- **Card drawer**: `HandoffDestination` (`DetailDrawerViewModel.cs:288-290`) is the drawer's own card only; the path
  appears on the verifier's drawer, which only the owner sees. The checked card's drawer is unchanged.
- **Board snapshot**: in memory (`ShellViewModel.cs:1415-1418`, `BoardSnapshotSource`), served only through the MCP
  handler (`related`, and `SessionPathRule`, which now also refuses the verifier's handoff as anyone's brief or report:
  harmless). Nothing writes it to disk.
- **An improvement as a side effect**: before this, a verifier with no `StatusDocumentPath` got `HandoffPath.Discover`,
  which searches its extra directories (the holdout first) and their child folders for `HANDOFF*.md`, `NOTES.md` and the
  like. A notes file in the holdout could have become the verifier's handoff, its path shown through `related`, and its
  next start told to "Continue ... Read <that file>". The stored path short-circuits that.

**Verdict on M1: fixed.**

### 3. S3 and S5 in the guard, probed

`node` with JSON on stdin (including `cwd`), `THESEUS_SETTINGS` and `LOCALAPPDATA` both pointing inside the scratch copy
folder (`<scratch>\copy3\probe`); no real settings or board file touched. **275 probes, 273 as expected; the two others
explained below.**

**S3, holdout folder not a folder path.** For each of `holdout`, `C:\`, `C:`, `C:/`, `\`, `.\holdout`, `..\holdout`,
`D:\`, `  holdout  `, `C:holdout`: a no-role Read of `<repo>\tools\holdout\holdout-guard.js` is allowed, a no-role
`dotnet test` in `<repo>` is allowed, and a verifier's Write in the code is allowed (folder counts as unset). A UNC
share root (`\\server\share`) also counts as unset; a UNC folder under it still guards. Absolute folders still guard,
including with a trailing separator, forward slashes and `x\..\`. With a relative folder but a remote set, a fetch of
the remote is still refused (the remote is checked on its own).

Not as expected, one probe: `/c/holdout` (a Git Bash form) and `\holdout` count as absolute (`path.win32.isAbsolute`
accepts a rooted path with no drive) and resolve against the guard process's current drive: `/c/holdout` becomes
`<drive>:\c\holdout`, so a Read of the real `<holdout>\scenarios\a.md` is **allowed** while `<drive-c-folder>\holdout\a.md` is
refused. A silent wrong guard, from a setting the owner types; see consider C1.

**S5, Theseus's own files.** With no holdout, with a holdout, and with no settings file at all; for no role and
verifier: Write, Edit, MultiEdit (`file_path`) and NotebookEdit (`notebook_path`) of `<LOCALAPPDATA>\Theseus\settings.json`
and `board.json` are **refused**; Read, Grep of the folder, and `cat` in Bash are **allowed**. Also refused: upper case,
forward slashes, a relative `settings.json` with `cwd` in the data folder, `x\..\board.json`, and the Git Bash form
`/c/...`. Allowed: `board.json.bak`, `links.json` in the same folder, an author's write, and `echo {} > settings.json` in
Bash (the stated tripwire limit).

Not as expected, one probe: a Write of the file `THESEUS_SETTINGS` points at is allowed when it is not the default
path. `THESEUS_SETTINGS` is set only by the tests (`implementation.md:26`), so in normal use the protected file is the
one the guard reads. Recorded, no change needed.

**Ordinary sessions with no usable holdout** (folder empty, no settings file, unreadable JSON, a relative folder, a drive
root), no role and verifier, 14 shapes each (Read, Write, Edit, Bash including `/c/holdout` text, PowerShell, Grep with
no path, Glob of `C:\`, WebFetch, a Write of another file in Theseus's data folder, of `settings.json` in a repo, of the
guard's own source, and a Bash `ls` with `cwd` in a would-be holdout folder): **all 140 allowed.** The two refused
writes are the only change for sessions with no holdout.

### 4. Attempt-2 should-fix items

| Item | Result | Evidence |
|---|---|---|
| S7 stale text | **Fixed** | Settings hint (`SettingsWindow.xaml:144`), notice (`ShellViewModel.cs:787`, now "Tools > Options" and "reads the scenarios there"), summary (`ShellViewModel.cs:778`), `TheseusSettings.cs:88`, `plan.md` step 6 and risk note (marked superseded). No other stale "runs there" text in `src`, `tests`, `tools`, `docs` |
| S8 AC 6 mark | **Fixed** | `spec.md` AC 6 now says "Added in fix pass 2 (M1), awaiting Nigel's OK", and AC 11's added line has the same mark (attempt-2 C14 too) |
| S9 `Distinct` untested | **Fixed** | `ARepoNamedTwice_IsInReachOnce` (`VerifierCardTests.cs`); mutation m6 killed |
| C12 shell punctuation | **Fixed for punctuation** | Refused now: `>/c/...`, `>>`, `2>`, `</c/...`, `|/c/`, `;/c/`, `&&`, `&`, `<(cat /c/...)`. Still allowed, as recorded and not claimed: an escaped space `/c/My\ Holdout` and `/mnt/c/...`. Test cases added for `cat` and `>`; mutation m5 killed |

### 5. Mutation (P16)

The feature worktree copied without `bin`, `obj` and `.git` to `<scratch>\copy3\repo`; each guard removed in the copy
only, tests run, the file restored. First against the holdout and verifier-card tests, then the full suite for any that
survived.

| # | Mutation | Focused tests | Full suite |
|---|---|---|---|
| m1 | App: the action no longer sets `StatusDocumentPath = verifier.HandoffPath` | survived | survived (1617 of 1617) |
| m2 | Core: `HandoffPath` back to `HANDOFF-<key>.md` in the working directory | killed (`ItsHandoff_GoesInTheHoldoutsResults_WhereItMayWrite`) | |
| m3a | guard: absolute-path check removed (drive-root check kept) | survived | survived (1617 of 1617) |
| m3b | guard: drive-root check removed (absolute check kept) | killed (`AHoldoutFolderThatIsNotAFolderPath_IsTreatedAsUnset`) | |
| m3c | guard: both removed (folder used as typed, as before) | killed (3 tests) | |
| m4a | guard: own-files refusal removed | killed (`TheseusOwnSettingsAndBoard_CannotBeWrittenByASession`, 2 cases) | |
| m4b | guard: `board.json` dropped from the own files | killed (the `board.json` case) | |
| m4c | guard: own-files refusal only when a holdout is set (the early exit restored above it) | survived | survived (1617 of 1617) |
| m4d | guard: own-files refusal ignores `notebook_path` | survived | survived (1617 of 1617) |
| m4e | guard: own-files refusal compares the path without resolving it against `cwd` | survived | survived (1617 of 1617) |
| m5 | guard: `>` removed from `windowsPaths` | killed (`NoRole_IsRefusedAGitBashPathToTheHoldout`, `>` case) | |
| m6 | Core: `Distinct` removed from `VerifierCard.For` | killed (`ARepoNamedTwice_IsInReachOnce`) | |

Why the survivors live: **m1** is App code with no test of `VerifyWithHoldouts` (the same gap as attempt 2's mutation
16), so the M1 fix itself is untested end to end; only its Core half (m2) is pinned. **m3a**: the tests run node in the
test process's folder, so a relative `holdout` resolved there never matches the probed path; the test passes with or
without the check. **m4c**: every own-files test sets a holdout (`<holdout>`), so the case that changes AC 4 for every
session, a refusal with no holdout set, is not tested. **m4d** and **m4e**: no test uses `notebook_path` or a relative
path for the own files. The probes above show all four behave as written; the tests do not hold them.

### 6. Text check (P17)

| Text | Verdict |
|---|---|
| Card-menu tooltip (`MainWindow.xaml:400`) "... not offered for a verifier card or an archived one" | Right: matches `CanVerify` (`ShellViewModel.cs:774-775`) |
| Workstream-menu tooltip (`MainWindow.xaml:1218`) "Needs one card ticked and ..." | Right: `Chosen() is [var only] && CanVerify(only)` (`ShellViewModel.cs:125-126`) |
| `AGENTS.md:75-78` "The one exception is a verifier card (its session runs with `THESEUS_ROLE=verifier`): it reads them as its brief says, and writes only in the holdout's results folder" | Right, and placeholder-only (no paths) |
| `VerifierCard.cs:17-20` (`HandoffPath` param) | Right |
| `WorkstreamRole.cs:6-7` "Set by the board ... the holdout guard refuses a session's file-tool writes to the board file. A shell command could still edit that file" | Right, given the guard is installed |
| Guard header `holdout-guard.js:12-18` (shell rule, own files, S2 limit) | Right |
| Guard `holdout-guard.js:45-46` "not an absolute path, or is a whole drive" | Slightly over-claims: a rooted path with no drive (`\holdout`, `/c/holdout`) passes as absolute (C1) |
| Guard header `holdout-guard.js:4` "With no holdout set, it allows everything" | **Wrong since S5**: it refuses two writes (SF1) |
| `TheseusSettings.cs:88-90` "Empty: no holdout, and the guard allows everything" | **Wrong since S5** (and only with the remote empty too, review C11); the stray line break before "(verifier-card AC 4 ..." is still there (SF1) |
| `HoldoutGuardTests.cs:10` summary "With no holdout set, it allows everything" | **Wrong since S5** (SF1) |
| `spec.md:35` AC 4 "With none set, it allows everything" | **Changed in behaviour, not in text, and not marked awaiting Nigel's OK** (SF1) |
| `docs/holdouts.md:16-17` "cannot read, search or fetch anything in the holdout" | Over-claims: S2 was recorded as a known limit, and the review asked for this sentence to be corrected if so (SF2) |
| `docs/holdouts.md:17` "write only its report, in the holdout's `results` folder" | Now also its handoff (SF2) |
| `docs/holdouts.md` | Says nothing of the new refusal of Theseus's own files for every session (SF1) |
| `evidence-part1-attempt2.md` live step L1 "while Theseus has no holdout folder set, every tool call in a new session works as before (AC 4)" | No longer true for two writes; the owner's live check will see it (SF1) |

## Findings

### Must fix

None.

### Should fix

- **SF1. S5 changed AC 4 without saying so.** The own-files refusal runs before the "no holdout, allow everything" exit
  (`holdout-guard.js:142-155`), so every session on the machine, with or without a holdout, is now refused file-tool
  writes to Theseus's `settings.json` and `board.json`, including the owner's own sessions that edit the board by hand.
  That may well be wanted, but it is a new rule like AC 6's: `spec.md:35` (AC 4) still says "With none set, it allows
  everything", unmarked, and so do `holdout-guard.js:4`, `TheseusSettings.cs:88-90` and `HoldoutGuardTests.cs:10`;
  `docs/holdouts.md` does not mention it, and live step L1 states the opposite. Mark the AC 4 change as awaiting
  Nigel's OK, as S8 did for AC 6, and correct the four texts.
- **SF2. `docs/holdouts.md:16-17` not brought in line.** "cannot read, search or fetch anything in the holdout" stands
  although S2 was recorded as a known limit (the review asked for this sentence to be corrected in that case), and "write
  only its report" is now "its report and its handoff".
- **SF3. The new guards are partly untested** (P16): m1 (the M1 fix in App code), m3a (the absolute-path check), m4c
  (the refusal with no holdout set: the case SF1 is about), m4d (`notebook_path`) and m4e (relative paths) survive
  the full suite. Cheap fixes: own-files cases with an empty `holdoutFolder`, a NotebookEdit case and a relative case
  with `cwd` set; for m3a, start node with `WorkingDirectory` set so that a relative `holdout` would match the probed
  path. m1 needs an App-level test or the role and handoff assignment moved into Core (as `VerifierEnding` was).

### Consider

- **C1. A rooted folder with no drive counts as set but guards the wrong place.** `holdout-guard.js:47`:
  `path.win32.isAbsolute('/c/holdout')` and `('\holdout')` are true, and resolving them uses the guard process's current
  drive; `/c/holdout` guards `<drive>:\c\holdout` and lets a Read of `<holdout>\...` through. Treat a path with no
  drive letter or UNC prefix as unset (or translate `/x/...`), with a test.
- **C2. Verify with holdouts… accepts a folder the guard ignores.** `CanVerify` checks only that the setting is not
  blank, so with a relative or drive-root folder (now unset for the guard) a verifier card is created and its sessions,
  like every other, are unguarded. Settings could reject such a folder or its hint say so (the review's optional S3
  step); `VerifierCard.For` also uses the setting untrimmed where the guard trims it.
- **C3. Two verifications of one card in the same minute** share both the report and now the handoff path (the stem is
  `<key>-<yyyyMMdd-HHmm>`); `WorkstreamOverlap` would flag the shared handoff. Seconds in the stem, or the verifier's key,
  would separate them.
- **C4. A custom stop prompt or owner instruction that asks every session to write elsewhere** is refused for a
  verifier; the review suggested one line in `docs/holdouts.md` for it. Not added.
- **C5. The `THESEUS_SETTINGS` override file is not protected** when it is not the default path. Test-only; fine as is.
- C12's remaining forms (escaped space, `/mnt/c/`) are unchanged, as recorded.

## Record

- Everything this run wrote is inside `<scratch>\copy3` (the copy, probe scripts and results, mutation logs) and this
  file, with one exception: the worktree suite's console output went to `suite3.txt` in `<scratch>`, beside the copy
  folder rather than inside it. Not deleted (no deletes). The suite run also refreshed the worktree's ignored `bin` and
  `obj` output; `git status` is clean.
- Nothing committed, pushed or branched; no tracker, GitHub, settings, hooks, board or terminal touched.

## Verdict

**PASS, with three should-fix items.** M1 is fixed: a confirmed verifier report now asks for a handoff in the holdout's `results` folder, the
guard lets the verifier write it, and the close step closes the session; the stored path leaks nothing new to the
implementing card. S3 and S5 behave as intended under 275 probes, and S7, S8, S9 and C12 are fixed. No must-fix.
Should fix: SF1 (S5 silently changed AC 4 and four texts now say the opposite), SF2 (two docs sentences), SF3 (five
surviving mutations, including the M1 fix's App half and the no-holdout refusal).

## Disposition (implementer, overnight, 2026-11-19)

| Finding | Disposition |
|---|---|
| SF1: the own-files refusal changed AC 4 unmarked | **Fixed by keeping AC 4 as approved:** the refusal now runs only while a holdout is set, so with no holdout the guard refuses nothing at all. Test `WithNoHoldoutSet_EvenTheseusOwnFilesAreNotRefused` (it fails if the refusal moves back above the exit, mutation m4c) |
| SF2: docs/holdouts.md | **Fixed:** it says a verifier writes its report and its handoff in results, that own-file writes are refused while a holdout is set, that nothing is refused with none set, and states the two tripwire limits (a search rooted above the holdout; shell commands) |
| SF3: surviving mutations | m3a, m4d, m4e now have tests (`ARelativeHoldoutFolder_IsNotReadAsAFolderBesideTheGuard`, `TheseusOwnBoard_CannotBeWrittenAsANotebook`, `TheseusOwnBoard_CannotBeWrittenByARelativePath`). m1 (the App line setting the handoff path) stays untested App code, covered by the live check's stop-on-confirm step |
| Considers | Recorded: C1 a rooted folder with no drive letter; C2 an untrimmed setting; C3 two verifications in one minute share paths; C4, C5 |

Full suite 1621 of 1621.

> P19: the made-up probe paths in this record (a holdout at the root of drive C, a repository under C:\work) are written as `<holdout>` and `<repo>` (overnight decision 5, 2026-11-19).
