# verifier-card: review

Pipeline `feature` (method kit v0), stage **review**, autonomy L0, run overnight under the owner's autonomy brief.
Reviewer: a fresh sub-agent started by the implementer, who did not write the change and is independent of both
verify attempts (partial independence: same model family, briefed by the implementing session). Code review only:
nothing was built, run or fixed.

Change: `git diff origin/main...HEAD -- src tests tools docs` on `nigel/verifier-card`, at `dae8d1a`. That commit
landed after verify part 1 attempt 2 (S7, S8, S9 and the C12 redirect change), so those edits are reviewed here but
have not been through a verifier.

| | Clock time (measured) |
|---|---|
| Started | 19:26:38 |
| Finished | 19:33:09 |

## Rubric

| Item | Verdict | Note |
|---|---|---|
| R1 board changes pass the person | Holds | No MCP tool added or pre-approved; the role is not in any tool or proposal |
| R2 a report is a claim | Holds | The stop follows `Confirm` only (`ChainsViewModel.cs:556-567`), never a report; `Satisfy` stops nothing |
| R3 nothing starts on its own | Holds | `VerifyWithHoldouts` writes a plan and links, starts nothing (`ShellViewModel.cs:779-817`); the hands-over link has no document, so the verifier is not held as Waiting |
| R4 trackers read only; no durations | Not touched | |
| R5-R8 locks | Not touched | |
| R9 nothing destructive at startup | Holds | `ReportConfirmed` runs only from a confirm press |
| R10 ask rather than duplicate a tab | Not touched | |
| R11 no start with nothing to read | Holds | Hands-over link created with no output (`ChainsViewModel.cs:269-279`), so `Inputs` excludes it |
| R12 state from a file | Holds | The guard reads `settings.json`, never Theseus over the network, so it works while Theseus is down |
| R13 paused/blocked marks | Not touched | |
| R14 briefs and reports relative to the spawner | Holds, with a doubt | The report goes to the holdout's `results`, not the spawner's brief folder. That is approved AC 10 (D3/D4), but no comment says it departs from R14; see C9 |
| R15 nothing secret to run or update | Holds | No token or credential added |
| R16 conventional Windows behaviour | Holds | Command in the card menu and the Workstream menu, access key unclashed, tooltip shown while disabled; see S4 on the tooltip's reason |
| R17 live state / setup dialogs | Holds | Settings fields are in the existing Options dialog with its Save |
| R18 cards do not grow | Not touched | |
| R19 lock colours exact | Holds | The verifier takes the checked card's colour (difference 1); a lock entry still matches exactly |

AGENTS.md: one type per file holds; no warnings (per the record, not re-run); public-repo rule holds in `src`, `tests`,
`tools`, `docs` (placeholders only, owner cited by name as the convention asks). Invariants "Nothing starts a session
on its own", "Theseus reads trackers and never writes" and "No secrets on the machine" hold.

## Findings

### Must fix

**M1. A verifier can never complete the stop that confirming its report starts, so it never closes, and the stop
prompt sends it to write into the checked card's repo.** (Spec "What changes": "the verifier card stops and closes on
its own"; AC 13; brief "Change nothing in the checked card's repos"; `docs/holdouts.md:32`.)
`ReportConfirmed` calls `BeginStop` (`ShellViewModel.cs:835`), whose request asks the session to write a handoff to
`HandoffPath.For(workstream)` (`StopSelection.cs:57`). A new verifier card has no `StatusDocumentPath`, so that is
`HANDOFF-<verifier key>.md` in its working directory (`HandoffPath.cs:31`), which after the AC 10 swap is **the checked
card's repo**. The guard refuses every verifier Write outside `results` (`holdout-guard.js:165-171`), so the handoff
cannot be written by the file tools; the close step needs a handoff written after the request
(`SessionClosing.cs:60`), so the session is never closed. If the verifier works round the refusal with a shell
redirect (not policed in the code), it writes a file that can carry scenario detail into the repo the implementing
sessions read and commit. This was true before the swap too (the handoff would have been in the holdout, outside
`results`), but the swap makes the fallback land in the checked code. The live check L9 would show it, but it is
decidable from the code now. Smallest fix: when `VerifyWithHoldouts` sets the role, also set the card's
`StatusDocumentPath` to a file under the holdout's `results` (for example `<report name>-handoff.md`), which
`HandoffPath.For` returns first and the guard already allows; one test in `VerifierCard` for the path. The same
refusal hits any owner-level instruction that has every session write a file (logs, memory): worth one line in
`docs/holdouts.md`.

### Should fix

**S1. AGENTS.md tells the verifier not to look for the holdout scenarios.** (AC 10 changed text, AC 11.) The verifier
now starts in the checked card's folder, so it loads that repo's `CLAUDE.md`. For this repo that is `AGENTS.md:75-76`:
"Independent holdout scenarios judge changes to Theseus ... Do not look for them". The first real job is verifying this
repo's own pull request #1, so the verifier's first instruction file contradicts its brief. Add "unless your card is
a verifier (`THESEUS_ROLE=verifier`)" to that line, or a line in the brief; the brief change would need the owner's OK
with the other brief line. This is the concrete form of the risk the evidence noted generally ("the verifier now
loads the checked repo's `CLAUDE.md`").

**S2. A search rooted above the holdout reads into it.** (AC 5; `docs/holdouts.md:16` "cannot read, search or fetch
anything in the holdout".) `located` holds the search root (`holdout-guard.js:137-141`) and `under(root, holdout)` is
false when the root is an **ancestor** of the holdout, so a no-role `Grep` with `path` (or no path and `cwd`) set to a
folder that contains the clone searches the scenarios and returns their lines. That is an accident a cooperating
session makes, unlike the evasions recorded under C11/C12, whenever the clone sits under a folder a session searches.
Either refuse a Grep/Glob whose root contains the holdout (`under(holdout, root)`) for no role, with a message saying
to narrow the path, or record it as a known limit and correct the docs sentence. No probe in either evidence file
covers it.

**S3. A holdout folder that is not an absolute path makes the guard match far too much.** (The review question: the
guard must not lock a normal session out of its own work; AC 4, AC 7.) Settings accepts any text
(`SettingsViewModel.cs:399`), and the guard uses it as is (`holdout-guard.js:37`). A bare `holdout`, typed by mistake,
makes `names()` match every path with a `/holdout/` segment, including this repo's own `tools/holdout/holdout-guard.js`,
for every no-role session; a drive root or a parent of the code folders makes every shell command under it refused
(`holdout-guard.js:151`). Smallest fix: the guard treats a folder that is not `path.win32.isAbsolute` (or is a drive
root) as unset, with a test; optionally Settings says so in its hint.

**S4. The disabled tooltip gives one reason for three.** (AC 7 "says why".) `CanVerify` (`ShellViewModel.cs:774-775`)
also disables the item for a verifier card and an archived card, but both tooltips (`MainWindow.xaml:400`, `:1218`)
end "Needs the holdout scenarios folder set in Tools > Options". With the folder set, right-clicking a verifier card
shows a greyed item whose tooltip names a setting that is already set. Add "Not for a verifier card or an archived
one" to both, or bind the tooltip to the reason.

**S5. "Set only by the board, never by a session" claims more than holds.** (`WorkstreamRole.cs:6`; the review
question "no session can set its own role".) AC 3 holds as written: no proposal field and no MCP tool carries a role.
But the role lives in `board.json` in Theseus's data folder, which any no-role session can Edit (the guard does not
protect it, as C6 records for `settings.json`); giving its own card the verifier role would, at its next pressed
start, let it read the holdout. That is a deliberate act and the guard is a tripwire, so a known limit is acceptable,
but then the comment and `docs/holdouts.md` should say "set only by the board; a session that edits the board file can
change it, as it can the settings", and the limit should be in the spec's out-of-scope list beside C6. Cheaper still:
the guard refuses Write/Edit of `board.json` and `settings.json` in Theseus's data folder for every role but author.

### Consider

- **C1. Guard header omits its one rule for every role.** `holdout-guard.js:6-13` lists the rules by role but not the
  shell-inside-the-holdout refusal that binds no role and verifier alike (`:149-154`). The header is what a maintainer
  reads first; add one line.
- **C2. The write check reads twice for one idea.** `holdout-guard.js:167` computes `resolve(target, cwd)` twice and
  expresses "strictly inside results" as `!under(...) || equal`. A `strictlyUnder(target, folder)` helper (or one local
  variable) reads better. `names()` (`:59-80`) is clear enough with its comment.
- **C3. `ItIsAVerifier` cannot fail usefully** (`VerifierCardTests.cs:58-61`): it compares a constant with the value it
  is defined as. The behaviour that matters, the created card having the role, is set in App code
  (`ShellViewModel.cs:807`) and untested. Drop the test, or drop `VerifierCard.Role` and use `WorkstreamRole.Verifier`.
- **C4. Do the small types earn their files?** `VerifierCard` does. `VerifierEnding` does, narrowly, because it moves
  the AC 13 rule into tested Core code from untested App code. `VerifierBrief` is one method used only by
  `VerifierCard.For`; it could be a private method there, but keeping the spec-worded text in its own file is
  defensible. `RoleEnvironment` is small and fine; its `Variable` constant is not used by the tests, which hard-code
  `"THESEUS_ROLE"`.
- **C5. The two guard test classes duplicate their harness** (`Guard`, `FindNode`, `RepositoryRoot` in
  `HoldoutGuardTests.cs:100-151` and `HoldoutGuardWorkingDirectoryTests.cs:107-158`). One helper type would halve it.
  The second class also writes a settings file per call to the temp folder and never removes it.
- **C6. Stale fixture.** `LaunchEnvironmentTests.cs:50` builds its verifier with working directory `<holdout>`, the
  pre-swap layout. Harmless to the assertion; misleading to a reader.
- **C7. The brief test uses `Does.Contain`** (`VerifierCardTests.cs:40-55`), so an extra or reordered instruction in a
  "fixed brief" would pass. An equality on the whole text would hold AC 11 exactly.
- **C8. The log line precedes the outcome.** `ShellViewModel.cs:834` logs "Stopping ..." before `BeginStop`, which may
  only queue a paste (no hook installed) or find no steps. Fine for AC 13's "logs that it did"; "Asked ... to stop"
  would be exact. Note too that, as Stop does, a confirm with no hook installed copies to the clipboard and moves focus.
- **C9. Say where R14 is departed from.** The `VerifierCard` summary could name R14 and say the report goes to the
  holdout by D3/D4 so its details stay private, so a later reviewer does not raise it.
- **C10. No author card, and the docs' last line.** Theseus can give a card only no role or verifier, so a holdout
  authoring session launched from the board can run no shell command in the holdout (AC 6 changed text) unless Theseus
  itself inherited `THESEUS_HOLDOUT_AUTHOR=1`. `docs/holdouts.md:34` "Commit and push the `results` files yourself"
  is right only if done outside a Claude session or in one started with that variable; say so. Also `:17-18` "No
  session runs a shell command inside the holdout folder" reads as including the author; "no session but an author's".
- **C11. `TheseusSettings.cs:86-89`**: "Empty: no holdout, and the guard allows everything" is true only when the remote
  is empty too, and the comment has a stray line break before its citation.
- **C12. The verifier's transcript sits with the implementer's.** With the swap, Claude Code keys the verifier's
  transcript (which holds every scenario it read) and project memory by the checked card's folder, beside the
  implementing sessions' own. The guard refuses the verifier's memory Write, and no implementing session reads
  transcripts in normal work, so this is a record item, not a fix: add it to the AC 10 change's costs for the owner.
- **C13. C7 of the evidence understates its likelihood here.** A verifier's shell children inherit
  `THESEUS_ROLE=verifier`; a scenario that runs a built Theseus from the checked folder would make every card that
  Theseus launches a verifier, since a no-role launch clears nothing. For this repo's scenarios that is plausible,
  not remote.

## AC 6, AC 10 and the brief line changed in fix pass 2

- **AC 6, last sentence (binds a no-role session in the holdout):** sound. The guard cannot tell reading commands from
  changing ones, and an implementing session never has a reason to stand in the holdout. The cost falls on the owner's
  own sessions, which already need `THESEUS_HOLDOUT_AUTHOR=1` to edit scenarios; C10 is the gap it leaves.
- **AC 10 (working directory the checked card's, holdout as an extra directory):** sound, and needed for the shell rule
  as the attempt-2 evidence argues. It brings three costs the owner should weigh with it: M1's handoff landing in the
  checked repo, S1's instruction-file conflict, and C12. M1 and S1 are fixable without reversing the swap.
- **Brief line "Read the scenarios with the file tools; run builds and tests from the checked card's folder, never from
  inside the holdout":** sound and true to the guard (`holdout-guard.js:149-154`).

## Summary

Not acceptable as is, for one must-fix: confirming a verifier's report starts a stop the verifier cannot finish,
because its handoff path is outside `results` (in the checked repo, after the swap) and the guard refuses the write,
so the card never closes and the docs' promise does not hold (M1). The rest is sound: the role cannot be set through
any proposal or MCP tool, nothing starts without a press, only a confirm on a verifier's reports-back link stops
anything, and the guard's path logic is maintainable. Five should-fix items: the repo's own instruction file tells the
verifier not to look for holdouts (S1), a search rooted above the holdout reads into it (S2), a non-absolute holdout
folder makes the guard lock normal sessions out of unrelated paths (S3), the disabled tooltip gives the wrong reason
for verifier and archived cards (S4), and the "never by a session" claim about the role needs either the board file
guarded or the limit written down (S5). The AC 6, AC 10 and brief-line changes are sound, subject to M1 and S1.

## What was done with each finding (overnight, under Nigel's autonomy brief, 2026-11-19)

| Finding | Disposition |
|---|---|
| M1 (must fix): a verifier could never close | **Fixed.** `VerifierCard.HandoffPath` is in the holdout's results folder (`<report stem>-handoff.md`), and the action sets it as the card's handoff document (`StatusDocumentPath`), so the stop a confirmed report starts asks for a handoff where the guard lets a verifier write. Test `ItsHandoff_GoesInTheHoldoutsResults_WhereItMayWrite` |
| S1: AGENTS.md told a verifier not to look for the holdouts | **Fixed:** AGENTS.md names the verifier card as the one exception |
| S2: a search rooted above the holdout can return scenario lines | **Recorded as a known limit** (overnight decision 4): refusing searches of every parent folder would refuse ordinary broad searches. Stated in the guard's header |
| S3: a holdout folder that is not an absolute path, or is a drive root | **Fixed:** treated as unset. Tests in `HoldoutGuardSettingsTests` |
| S4: tooltips named only the folder as the reason it is unavailable | **Fixed:** both tooltips say it is not offered for a verifier or archived card, and the Workstream one that one card must be ticked |
| S5: a session could give its own card the role by editing the board file | **Partly fixed:** the guard refuses file-tool writes to Theseus's `settings.json` and `board.json` (tests); a shell command could still edit them, stated in `WorkstreamRole`'s comment as a tripwire limit |
| Considers | The guard's header now states the shell rule. The rest recorded as they are, including the inherited `THESEUS_ROLE` (C7) for a review by Nigel |

Full suite 1617 of 1617. The review fixes and dae8d1a still need a re-verify.

> P19: the made-up probe paths in this record (a holdout at the root of drive C, a repository under C:\work) are written as `<holdout>` and `<repo>` (overnight decision 5, 2026-11-19).
